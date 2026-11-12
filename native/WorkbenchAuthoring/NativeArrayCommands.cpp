#include "NativeArrayCommands.h"
#include "Compatibility.h"
#include "SourceIdentity.h"
#include "SourceText.h"
#include <QCoreApplication>
#include <QHash>
#include <QDebug>
#include <QMutex>
#include <QMutexLocker>
#include <QRegularExpression>
#include <QThread>
#include <QUuid>
#include <memory>
#include <array>
#include <algorithm>
#include <vector>
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>

namespace rst {
namespace {
// Exact 1.9.0.37 owners; see the array encoding evidence in the delivery record.
constexpr quintptr undoSlot = 0x32c1e38, redoSlot = 0x32c1e40, destroySlot = 0x32c1e80;
constexpr quintptr undoRva = 0x1b92f60, redoRva = 0x1b929f0, destroyRva = 0x1b92460;
using Action = void (*)(void *);
using Destroy = void *(*)(void *, quintptr);
template<class T> T member(const void *object, size_t offset)
{
    T value;
    std::memcpy(&value, static_cast<const char *>(object) + offset, sizeof(value));
    return value;
}
template<class T> T method(void *object, size_t offset)
{
    return reinterpret_cast<T>(member<const quintptr *>(object, 0)[offset / sizeof(quintptr)]);
}
struct Phase {
    bool adopted = false, completed = false;
    QString error;
};
struct Plan {
    QByteArray identity;
    QString key, className;
    QStringList before, after;
    QByteArray applyWire, rollbackWire;
    Phase apply, rollback;
};
struct OwnedAction {
    std::shared_ptr<Plan> plan;
    QStringList before, after;
    bool beforeLocal = false;
    bool adopted = false;
    bool refused = false;
    bool rollback = false;
};
struct Pending {
    std::shared_ptr<Plan> plan;
    bool rollback = false;
};
struct ArrayView { char **items; unsigned capacity; unsigned count; };
static_assert(sizeof(ArrayView) == 16);
struct NativeOwners {
    Action undo = nullptr, redo = nullptr;
    Destroy destroy = nullptr;
    void (*assignString)(char **, const char *, unsigned) = nullptr;
    void *(*copyArray)(void *, const ArrayView *) = nullptr;
    void (*free)(void *, int) = nullptr;
};
struct Registry {
    QMutex mutex;
    quintptr image = 0;
    DWORD thread = 0;
    bool installed = false;
    bool attempted = false;
    NativeOwners owners;
    // Process-unique ownership remains recognizable after receipt release and
    // history disposal, without retaining every old receipt or its arrays.
    const QByteArray wirePrefix = QByteArray("@RST_ARRAY_") + QUuid::createUuid().toString(QUuid::Id128).toLatin1() + '_';
    QHash<QString, std::shared_ptr<Plan>> receipts;
    QHash<QByteArray, Pending> pending;
    QHash<void *, OwnedAction> commands;
};
// Hooks can be invoked during application teardown. Workbench owns command
// disposal; keep the registry itself alive until the process is gone.
Registry &registry() { static auto *value = new Registry; return *value; }
QString registerPlan(const QString &receipt, const std::shared_ptr<Plan> &plan)
{
    auto &state = registry();
    QMutexLocker lock(&state.mutex);
    if (state.receipts.contains(receipt) || state.receipts.size() >= 64)
        return QStringLiteral("Typed array receipt is already active or awaiting inspection");
    const auto nonce = state.wirePrefix + QUuid::createUuid().toString(QUuid::Id128).toLatin1();
    plan->applyWire = nonce + "_APPLY";
    plan->rollbackWire = nonce + "_ROLLBACK";
    state.receipts.insert(receipt, plan);
    state.pending.insert(plan->applyWire, {plan, false});
    state.pending.insert(plan->rollbackWire, {plan, true});
    return {};
}
QString metadata(const char *text)
{
    if (!text) return {};
    size_t length = 0;
    while (length < 1024 && text[length]) ++length;
    QString value;
    if (!length || length == 1024 || !decodeSourceText(QByteArrayView(text, qsizetype(length)), value)) return {};
    return value;
}
bool matchesField(void *source, unsigned index, const Plan &plan)
{
    if (!source || sourceIdentity(source) != plan.identity) return false;
    const auto *schema = member<const void *>(source, 0x18);
    if (!schema || index >= member<unsigned>(schema, 0x34)) return false;
    const auto *table = member<const char *>(schema, 0x28);
    if (!table) return false;
    const auto *descriptor = table + size_t(index) * 0x58;
    return member<unsigned>(descriptor, 0) == 0x8a && member<unsigned short>(descriptor, 4) == index &&
        metadata(member<const char *>(descriptor, 8)) == plan.key &&
        metadata(member<const char *>(schema, 0x20)) == plan.className;
}
bool readArray(void *source, unsigned index, QStringList &values, bool &local)
{
    values.clear();
    local = method<const void *(*)(void *, unsigned)>(source, 0x130)(source, index) != nullptr;
    const auto *storage = method<const void *(*)(void *, unsigned)>(source, 0x128)(source, index);
    if (!storage) return false;
    const auto count = member<unsigned>(storage, 12);
    const auto *items = member<const char *const *>(storage, 0);
    if (count > 4096 || count > member<unsigned>(storage, 8) || (count && !items)) return false;
    unsigned total = 0;
    for (unsigned i = 0; i < count; ++i) {
        const auto *text = items[i];
        const unsigned length = text ? member<unsigned>(text - 8, 0) : 0;
        if (length > 65536 - total) return false;
        total += length;
        QString value;
        if (!decodeSourceText(QByteArrayView(text, length), value)) return false;
        values.append(value);
    }
    return true;
}
struct OwnedStrings {
    std::vector<char *> items;
    ~OwnedStrings()
    {
        for (auto *text : items)
            if (text && InterlockedDecrement(reinterpret_cast<volatile LONG *>(text - 4)) == 0)
                registry().owners.free(text - 12, 0);
    }
};
bool writeArray(void *source, unsigned index, const QStringList &values, bool local)
{
    // Construct owned strings before changing source presence. The engine copy
    // owner retains them; releasing these temporaries cannot invalidate it.
    OwnedStrings strings;
    strings.items.resize(size_t(values.size()), nullptr);
    if (local) for (qsizetype i = 0; i < values.size(); ++i) {
        const auto bytes = values[i].toUtf8();
        registry().owners.assignString(&strings.items[size_t(i)], bytes.constData(), unsigned(bytes.size()));
    }
    if (!local) {
        if (method<const void *(*)(void *, unsigned)>(source, 0x130)(source, index))
            method<void (*)(void *, unsigned)>(source, 0xf8)(source, index);
        return true;
    }
    auto *storage = method<void *(*)(void *, unsigned)>(source, 0x120)(source, index);
    if (!storage) return false;
    const ArrayView view{strings.items.data(), unsigned(strings.items.size()), unsigned(strings.items.size())};
    registry().owners.copyArray(storage, &view);
    return true;
}
void run(void *action, bool undo)
{
    auto &state = registry();
    OwnedAction owned;
    bool found = false;
    bool replay = false;
    {
        QMutexLocker lock(&state.mutex);
        const auto known = state.commands.constFind(action);
        if (known != state.commands.cend()) {
            owned = *known;
            found = true;
        } else if (!undo) {
            const auto *text = member<const char *>(action, 0x20);
            const auto length = text ? member<unsigned>(text - 8, 0) : 0;
            if (text && length >= unsigned(state.wirePrefix.size()) &&
                std::memcmp(text, state.wirePrefix.constData(), size_t(state.wirePrefix.size())) == 0) {
                const auto pending = length < 128 ? state.pending.find(QByteArray(text, length)) : state.pending.end();
                if (pending != state.pending.end()) {
                    owned.plan = pending->plan;
                    owned.rollback = pending->rollback;
                    owned.before = pending->rollback ? owned.plan->after : owned.plan->before;
                    owned.after = pending->rollback ? owned.plan->before : owned.plan->after;
                    // Even a refused registered command must never fall back
                    // to interpreting its wire token as an authored value.
                    state.pending.erase(pending);
                    state.commands.insert(action, owned);
                    found = true;
                } else {
                    // A consumed/released token is still extension-owned.
                    // Make this distinct native command inert in both history
                    // directions; never feed the token to the text parser.
                    owned.refused = true;
                    state.commands.insert(action, owned);
                    found = replay = true;
                }
            }
        }
    }
    if (!found) {
        (undo ? state.owners.undo : state.owners.redo)(action);
        return;
    }
    if (owned.refused) {
        if (replay) qWarning() << "Workbench authoring: refused stale or duplicate typed array command";
        return;
    }
    auto *source = member<void *>(action, 8);
    const auto index = member<unsigned short>(action, 0x18);
    const bool firstApplication = !owned.adopted;
    QString error;
    QStringList current;
    bool currentLocal = false;
    if (GetCurrentThreadId() != state.thread || !matchesField(source, index, *owned.plan))
        error = QStringLiteral("Typed array command source or thread changed");
    else if (!readArray(source, index, current, currentLocal))
        error = QStringLiteral("Cannot read typed array command source");
    else {
        if (!owned.adopted) {
            if (undo || current != owned.before || !currentLocal)
                error = QStringLiteral("Typed array preparation no longer matches its local source");
            else {
                owned.beforeLocal = currentLocal;
                owned.adopted = true;
                QMutexLocker lock(&state.mutex);
                (owned.rollback ? owned.plan->rollback : owned.plan->apply).adopted = true;
                state.commands[action] = owned;
            }
        }
        if (error.isEmpty()) {
            const auto &expected = undo ? owned.after : owned.before;
            const auto &desired = undo ? owned.before : owned.after;
            const bool expectedLocal = undo ? true : owned.beforeLocal;
            const bool desiredLocal = undo ? owned.beforeLocal : true;
            if (current != expected || currentLocal != expectedLocal)
                error = QStringLiteral("Typed array contents or local presence changed before Undo/Redo");
            else if (!writeArray(source, index, desired, desiredLocal) ||
                !readArray(source, index, current, currentLocal) || current != desired || currentLocal != desiredLocal) {
                const bool restored = writeArray(source, index, expected, expectedLocal) &&
                    readArray(source, index, current, currentLocal) && current == expected && currentLocal == expectedLocal;
                error = restored ? QStringLiteral("Typed array write failed; previous state restored") :
                    QStringLiteral("Typed array write and restoration failed; inspect before continuing");
                // A failed first application is a failed command, not a latent
                // authoring request that a later Redo may unexpectedly apply.
                if (firstApplication) owned.refused = true;
            } else if (firstApplication) {
                QMutexLocker lock(&state.mutex);
                (owned.rollback ? owned.plan->rollback : owned.plan->apply).completed = true;
            }
        }
    }
    if (!error.isEmpty()) {
        QMutexLocker lock(&state.mutex);
        (owned.rollback ? owned.plan->rollback : owned.plan->apply).error = error;
        if (!owned.adopted || owned.refused) {
            owned.refused = true;
            state.commands[action] = owned;
        }
    }
    if (!error.isEmpty()) qWarning().noquote() << "Workbench authoring:" << error;
}
void undoAction(void *action) { run(action, true); }
void redoAction(void *action) { run(action, false); }
void *destroyCommand(void *command, quintptr flags)
{
    auto &state = registry();
    {
        QMutexLocker lock(&state.mutex);
        state.commands.remove(static_cast<char *>(command) + 0x28);
    }
    return state.owners.destroy(command, flags);
}
struct Hook { quintptr slot, owner; void *replacement; };
const Hook hooks[] = {
    {undoSlot, undoRva, reinterpret_cast<void *>(&undoAction)},
    {redoSlot, redoRva, reinterpret_cast<void *>(&redoAction)},
    {destroySlot, destroyRva, reinterpret_cast<void *>(&destroyCommand)}
};
struct SlotPatch { void **slot; void *expected; void *replacement; };
using Protect = BOOL (WINAPI *)(LPVOID, SIZE_T, DWORD, PDWORD);
using Exchange = void *(*)(void **, void *, void *);
void *exchangeSlot(void **slot, void *replacement, void *expected)
{
    return InterlockedCompareExchangePointer(slot, replacement, expected);
}
bool patchSlots(const std::array<SlotPatch, 3> &patches, Protect protect, Exchange exchange)
{
    auto first = reinterpret_cast<quintptr>(patches[0].slot);
    auto last = first;
    for (const auto &patch : patches) {
        if (*patch.slot != patch.expected) return false;
        first = std::min(first, reinterpret_cast<quintptr>(patch.slot));
        last = std::max(last, reinterpret_cast<quintptr>(patch.slot));
    }
    // All three exact-profile slots occupy the same .rdata page. Changing its
    // protection once also makes rollback independent of intermediate state.
    if (first / 4096 != (last + sizeof(void *) - 1) / 4096) return false;
    const auto size = SIZE_T(last - first + sizeof(void *));
    auto *address = reinterpret_cast<void *>(first);
    DWORD original = 0;
    if (!protect(address, size, PAGE_READWRITE, &original)) return false;
    size_t installed = 0;
    for (const auto &patch : patches) {
        if (exchange(patch.slot, patch.replacement, patch.expected) != patch.expected) break;
        ++installed;
    }
    DWORD ignored = 0;
    if (installed == patches.size() && protect(address, size, original, &ignored)) return true;
    // A failed VirtualProtect leaves the previous (writable) protection in
    // force. Revert only slots still owned by this attempt, then restore it.
    for (size_t i = 0; i < installed; ++i)
        exchange(patches[i].slot, patches[i].expected, patches[i].replacement);
    protect(address, size, original, &ignored);
    return false;
}
}

QString NativeArrayCommands::install()
{
    auto &state = registry();
    const auto *app = QCoreApplication::instance();
    if (!app || QThread::currentThread() != app->thread()) return QStringLiteral("Array hook requires the application thread");
    if (state.installed) return available() ? QString() : QStringLiteral("Array command owner changed");
    if (state.attempted) return QStringLiteral("Array hook installation previously failed; restart before retrying");
    const auto error = incompatibility(inspectHost());
    if (!error.isEmpty()) return error;
    state.image = reinterpret_cast<quintptr>(GetModuleHandleW(nullptr));
    state.owners = {
        reinterpret_cast<Action>(state.image + undoRva), reinterpret_cast<Action>(state.image + redoRva),
        reinterpret_cast<Destroy>(state.image + destroyRva),
        reinterpret_cast<decltype(NativeOwners::assignString)>(state.image + 0x1e8cc0),
        reinterpret_cast<decltype(NativeOwners::copyArray)>(state.image + 0x20aa80),
        reinterpret_cast<decltype(NativeOwners::free)>(state.image + 0x1e95e70)
    };
    for (const auto &hook : hooks)
        if (member<quintptr>(reinterpret_cast<void *>(state.image + hook.slot), 0) != state.image + hook.owner)
            return QStringLiteral("Array command slot differs from the supported profile");
    HMODULE pinned = nullptr;
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN,
        reinterpret_cast<LPCWSTR>(&redoAction), &pinned)) return QStringLiteral("Cannot pin the array command owner");
    state.thread = GetCurrentThreadId();
    state.attempted = true;
    std::array<SlotPatch, 3> patches;
    for (size_t i = 0; i < patches.size(); ++i)
        patches[i] = {reinterpret_cast<void **>(state.image + hooks[i].slot),
            reinterpret_cast<void *>(state.image + hooks[i].owner), hooks[i].replacement};
    if (!patchSlots(patches, &VirtualProtect, &exchangeSlot))
        return QStringLiteral("Array command hook installation failed; restart before retrying");
    state.installed = true;
    return {};
}
bool NativeArrayCommands::available()
{
    auto &state = registry();
    if (!state.installed || GetCurrentThreadId() != state.thread) return false;
    for (const auto &hook : hooks)
        if (member<void *>(reinterpret_cast<void *>(state.image + hook.slot), 0) != hook.replacement) return false;
    return true;
}
QString NativeArrayCommands::wireValue(const QString &receipt, bool rollback)
{
    auto &state = registry();
    QMutexLocker lock(&state.mutex);
    const auto plan = state.receipts.value(receipt);
    return plan ? QString::fromLatin1(rollback ? plan->rollbackWire : plan->applyWire) : QString();
}
QString NativeArrayCommands::prepare(const QString &receipt, const PropertyDestination &destination, const QString &replacement)
{
    static const QRegularExpression token(QStringLiteral("^[A-Za-z0-9_-]{1,64}$"));
    if (!available()) return QStringLiteral("Typed array command owner is unavailable");
    if (!token.match(receipt).hasMatch() || destination.state != "captured" || destination.leafIdentity.isEmpty() ||
        destination.arrayIndex < 0 || destination.arrayIndex >= destination.arrayValues.size() || replacement.contains(QChar(0)))
        return QStringLiteral("Invalid typed array preparation");
    auto plan = std::make_shared<Plan>();
    plan->identity = destination.leafIdentity;
    plan->key = destination.key;
    plan->className = destination.className;
    plan->before = destination.arrayValues;
    plan->after = plan->before;
    plan->after[destination.arrayIndex] = replacement;
    if (plan->after.size() > 4096) return QStringLiteral("Typed array exceeds the entry limit");
    for (const auto &values : {plan->before, plan->after}) {
        qsizetype bytes = 0;
        for (const auto &value : values) {
            bytes += value.toUtf8().size();
            if (bytes > 65536) return QStringLiteral("Typed array exceeds the text limit");
        }
    }
    return registerPlan(receipt, plan);
}
QString NativeArrayCommands::outcome(const QString &receipt, bool rollback)
{
    auto &state = registry();
    QMutexLocker lock(&state.mutex);
    const auto plan = state.receipts.value(receipt);
    if (!plan) return QStringLiteral("Typed array receipt is absent");
    const auto &phase = rollback ? plan->rollback : plan->apply;
    if (!phase.error.isEmpty()) return phase.error;
    if (!phase.adopted) return QStringLiteral("Typed array command was not adopted");
    return phase.completed ? QString() : QStringLiteral("Typed array command did not complete");
}
void NativeArrayCommands::release(const QString &receipt)
{
    auto &state = registry();
    QMutexLocker lock(&state.mutex);
    const auto plan = state.receipts.value(receipt);
    if (!plan) return;
    state.pending.remove(plan->applyWire);
    state.pending.remove(plan->rollbackWire);
    state.receipts.remove(receipt);
}
}
