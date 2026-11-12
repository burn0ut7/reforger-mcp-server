#include "PropertyEditors.h"
#include "Compatibility.h"
#include "EditableChoices.h"
#include <QCoreApplication>
#include <QLineEdit>
#include <QPointer>
#include <QThread>
#include <QTimer>
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>

namespace rst::detail {
// Workbench 1.9.0.37, exact executable/Qt hashes checked by Compatibility.
// Ghidra evidence: 6d0f332acf30463bbb2988d90d096d54 (creator references),
// 5c4b1ac37c51448f8e6ce52780eebd4f (editor layout),
// 5dfcf04120d94ee3bf9ba548145d6d95 (enum container),
// 577312fcf62443e69c5c78411800cb95 (native value readback).
constexpr quintptr stringPropertyVtable = 0x2e18ab0;
constexpr quintptr createEditorSlot = 0x2e18c08;
constexpr quintptr createEditorFunction = 0x4adaf0;
constexpr quintptr stringComboVtable = 0x2e66758;
using CreateEditor = QWidget *(*)(QObject *, QWidget *);
CreateEditor original = nullptr;
QPointer<PropertyEditors> owner;
quintptr image = 0;

struct EnumValue { const char *label; const char *description; const char *value; };
static_assert(sizeof(EnumValue) == 24);

QString provider(QObject *property, bool &entities)
{
    const auto *bytes = reinterpret_cast<const char *>(property);
    if (*reinterpret_cast<const quintptr *>(bytes) != image + stringPropertyVtable ||
        *reinterpret_cast<const int *>(bytes + 0xf0) != 1) return {};
    const int count = *reinterpret_cast<const int *>(bytes + 0x104);
    const auto *items = *reinterpret_cast<const EnumValue *const *>(bytes + 0xf8);
    if (count <= 0 || count > 65536 || !items || !items[0].description) return {};
    constexpr char prefix[] = "rst-choices:";
    const char *text = items[0].description;
    for (size_t i = 0; i < sizeof(prefix) - 1; ++i)
        if (text[i] != prefix[i]) return {};
    text += sizeof(prefix) - 1;
    if ((text[0] != '1' && text[0] != '2') || text[1] != ':') return {};
    entities = text[0] == '2';
    text += 2;
    int length = 0;
    for (; length < 256 && text[length]; ++length) {
        const char c = text[length];
        if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
            (c >= '0' && c <= '9' && length > 0) || c == '_')) return {};
    }
    if (length == 0 || length == 256) return {};
    return QString::fromLatin1(text, length);
}
}

namespace rst {
PropertyEditors::PropertyEditors(QObject *parent, Created callback)
    : QObject(parent), created(std::move(callback)) {}

QString PropertyEditors::install()
{
    auto *application = QCoreApplication::instance();
    if (!application || QThread::currentThread() != application->thread() || thread() != application->thread())
        return QStringLiteral("Property hook requires the application thread");
    if (detail::original) return QStringLiteral("Property hook is already installed; restart to change it");
    if (!created) return QStringLiteral("Property editor consumer is missing");
    const auto reason = incompatibility(inspectHost());
    if (!reason.isEmpty()) return reason;
    detail::image = reinterpret_cast<quintptr>(GetModuleHandleW(nullptr));
    auto **slot = reinterpret_cast<void **>(detail::image + detail::createEditorSlot);
    auto *expected = reinterpret_cast<void *>(detail::image + detail::createEditorFunction);
    if (*slot != expected) return QStringLiteral("StringProperty creator slot differs from the supported profile");
    HMODULE pinned = nullptr;
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN,
            reinterpret_cast<LPCWSTR>(&createEditor), &pinned))
        return QStringLiteral("Cannot pin the property hook DLL for the process lifetime");
    DWORD protection = 0;
    if (!VirtualProtect(slot, sizeof(void *), PAGE_READWRITE, &protection))
        return QStringLiteral("Cannot write the StringProperty creator slot");
    detail::original = reinterpret_cast<detail::CreateEditor>(expected);
    detail::owner = this;
    auto *replacement = reinterpret_cast<void *>(&createEditor);
    const auto previous = InterlockedCompareExchangePointer(slot, replacement, expected);
    DWORD ignored = 0;
    const bool restored = VirtualProtect(slot, sizeof(void *), protection, &ignored) != FALSE;
    if (previous != expected) {
        detail::owner = nullptr;
        detail::original = nullptr;
        return QStringLiteral("StringProperty creator slot changed during installation");
    }
    if (!restored) {
        // Slot remains writable after the failed protection call. Restore the
        // original owner, and keep DLL code resident even on this error path.
        InterlockedCompareExchangePointer(slot, expected, replacement);
        VirtualProtect(slot, sizeof(void *), protection, &ignored);
        detail::owner = nullptr;
        return QStringLiteral("Property hook rolled back after protection restoration failed");
    }
    return {};
}

QWidget *PropertyEditors::createEditor(QObject *property, QWidget *parent)
{
    auto *widget = detail::original(property, parent);
    if (!detail::owner || !widget) return widget;
    bool entities = false;
    const auto provider = detail::provider(property, entities);
    if (provider.isEmpty()) return widget;
    // Creator stores the editor owner at property+0xa0 and returns its widget.
    // Getter +0x58 is 14034dd40, returning owner+0x18 (query
    // 1e78a0878c384362980e242e156b4831). Preserve the original widget return.
    auto *editor = *reinterpret_cast<QObject **>(reinterpret_cast<char *>(property) + 0xa0);
    if (!editor || *reinterpret_cast<const quintptr *>(editor) != detail::image + detail::stringComboVtable)
        return widget;
    auto *combo = *reinterpret_cast<QComboBox **>(reinterpret_cast<char *>(editor) + 0x18);
    if (combo != widget || !combo->isEditable()) return widget;
    const QPointer<QObject> guardedProperty(property);
    const QPointer<QComboBox> guardedCombo(combo);
    // The caller initializes the editor value after createEditor returns.
    QTimer::singleShot(0, editor, [guardedProperty, guardedCombo, editor, provider, entities] {
        if (detail::owner && guardedProperty && guardedCombo)
            detail::owner->created(guardedProperty, editor, guardedCombo, provider, entities);
    });
    return widget;
}

PropertySourceValue PropertyEditors::sourceValue(QObject *property)
{
    if (!detail::owner || !detail::original)
        return {QStringLiteral("property_hook_unavailable"), {}};
    return readPropertySource(property, detail::image);
}
PropertyDestination PropertyEditors::destination(QObject *property)
{
    PropertyDestination destination;
    if (!detail::owner || !detail::original) {
        destination.state = QStringLiteral("property_hook_unavailable");
        return destination;
    }
    const auto source = readPropertySource(property, detail::image, &destination);
    if (destination.state.isEmpty()) destination.state = source.state;
    return destination;
}
QString PropertyEditors::routeSelection(QObject *editor, QComboBox *combo,
    std::function<void(int)> activated, std::function<void()> finished)
{
    if (!detail::owner || !editor || !combo || !combo->lineEdit() ||
        QThread::currentThread() != detail::owner->thread() ||
        *reinterpret_cast<const quintptr *>(editor) != detail::image + detail::stringComboVtable ||
        *reinterpret_cast<QComboBox **>(reinterpret_cast<char *>(editor) + 0x18) != combo)
        return QStringLiteral("Unsupported native selection owner");
    // Exact constructor connections: 140710e60, query 5c4b1ac... . Replace
    // these two owner-bound connections, then disable Qt's implicit promotion
    // of typed labels below. Explicit popup/completer activation is retained.
    if (!QObject::disconnect(combo, &QComboBox::activated, editor, nullptr))
        return QStringLiteral("Native activation connection missing");
    if (!QObject::disconnect(combo->lineEdit(), &QLineEdit::editingFinished, editor, nullptr)) {
        QObject::connect(combo, &QComboBox::activated, editor, [editor] { nativeActivate(editor); });
        return QStringLiteral("Native text connection missing");
    }
    if (!EditableChoices::preserveLiteralFinish(combo)) {
        QObject::connect(combo, &QComboBox::activated, editor, [editor] { nativeActivate(editor); });
        QObject::connect(combo->lineEdit(), &QLineEdit::editingFinished, editor, [editor] { nativeFinish(editor); });
        return QStringLiteral("Qt literal-input connection unavailable");
    }
    QObject::connect(combo, &QComboBox::activated, editor, std::move(activated));
    QObject::connect(combo->lineEdit(), &QLineEdit::editingFinished, editor, std::move(finished));
    return {};
}
void PropertyEditors::nativeActivate(QObject *editor)
{
    // Native owning slots ignore the activated index and read current widget.
    if (editor && detail::owner)
        reinterpret_cast<void (*)(QObject *)>(detail::image + 0x715510)(editor);
}
void PropertyEditors::nativeFinish(QObject *editor)
{
    if (editor && detail::owner)
        reinterpret_cast<void (*)(QObject *)>(detail::image + 0x7157b0)(editor);
}
}
