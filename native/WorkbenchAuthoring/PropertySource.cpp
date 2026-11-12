#include "PropertySource.h"
#include "SourceText.h"
#include "SourceIdentity.h"
#include <QCoreApplication>
#include <QCryptographicHash>
#include <QMetaObject>
#include <QStringDecoder>
#include <QThread>
#include <QTreeWidget>
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>

namespace rst {
namespace {
// Workbench 1.9.0.37 only. Evidence and limitations are recorded in
// docs/workbench-authoring-extension.md, entity-reference transaction section.
template<class T> T function(quintptr image, quintptr rva)
{
    return reinterpret_cast<T>(image + rva);
}
template<class T> T member(const void *object, size_t offset)
{
    return *reinterpret_cast<const T *>(static_cast<const char *>(object) + offset);
}
struct SourcePath {
    void *root = nullptr;
    void *items = nullptr;
    unsigned capacity = 0;
    unsigned count = 0;
};
static_assert(sizeof(SourcePath) == 24);
using Free = void (*)(void *, int);
struct ScopedPath {
    SourcePath path;
    quintptr image;
    ~ScopedPath()
    {
        // Native path clear destroys member strings, but retains allocation.
        function<void (*)(SourcePath *)>(image, 0x1b33510)(&path);
        function<Free>(image, 0x1e95e70)(path.items, 0);
    }
};
struct ScopedString {
    char *text = nullptr;
    quintptr image;
    ~ScopedString()
    {
        if (text && InterlockedDecrement(reinterpret_cast<volatile LONG *>(text - 4)) == 0)
            function<Free>(image, 0x1e95e70)(text - 12, 0);
    }
};

QString metadataText(const char *text)
{
    if (!text) return {};
    size_t length = 0;
    while (length < 1024 && text[length]) ++length;
    if (!length || length == 1024) return {};
    QStringDecoder decoder(QStringDecoder::Utf8);
    const QString result = decoder(QByteArrayView(text, qsizetype(length)));
    return decoder.hasError() ? QString() : result;
}

PropertyDestination captureDestination(SourcePath path, const void *leaf, int arrayIndex, quintptr image)
{
    PropertyDestination result;
    result.state = QStringLiteral("destination_unresolved");
    if (!path.root || path.count > 64 || path.count > path.capacity || (path.count && !path.items))
        return result;
    // Generated IEntitySource.GetID's exact runtime type guard and ID member.
    if (!function<bool (*)(void *, unsigned)>(image, 0x1fe9240)(path.root, 0x71a6fe80))
        return result;
    const auto id = member<quint64>(path.root, 0x28);
    if (!id) return result;
    result.entityIdHex = QString::number(id, 16).toUpper();
    QByteArray identities;
    const auto rootAddress = reinterpret_cast<quintptr>(path.root);
    identities.append(reinterpret_cast<const char *>(&rootAddress), sizeof(rootAddress));
    const auto resolve = function<void *(*)(SourcePath *, bool)>(image, 0x1b33bf0);
    for (unsigned i = 0; i < path.count; ++i) {
        SourcePath prefix = path; // Borrowed; never clear or free this copy.
        prefix.count = i;
        auto *parent = resolve(&prefix, true);
        if (!parent) return result;
        const auto *schema = member<const void *>(parent, 0x18);
        const auto *entry = static_cast<const char *>(path.items) + size_t(i) * 24;
        const auto variable = member<unsigned>(entry, 0);
        if (!schema || variable >= member<unsigned>(schema, 0x34)) return result;
        const auto *table = member<const char *>(schema, 0x28);
        if (!table) return result;
        const auto *descriptor = table + size_t(variable) * 0x58;
        const auto storageIndex = member<unsigned short>(descriptor, 4);
        if (storageIndex != variable) return result;
        const auto type = member<unsigned>(descriptor, 0);
        if (type != 0xd && type != 0x8d) return result;
        const auto key = metadataText(member<const char *>(descriptor, 8));
        if (key.isEmpty()) return result;
        prefix.count = i + 1;
        auto *child = resolve(&prefix, true);
        if (!child) return result;
        const auto childAddress = reinterpret_cast<quintptr>(child);
        identities.append(reinterpret_cast<const char *>(&childAddress), sizeof(childAddress));
        // Copy the opaque native object-ID value used by path lookup. Do not
        // interpret it as GetName(), which is empty for unnamed containers.
        const auto objectIdentity = member<quint64>(child, 8);
        identities.append(reinterpret_cast<const char *>(&objectIdentity), sizeof(objectIdentity));
        int index = -1;
        if (type == 0x8d) {
            const auto vtable = member<const quintptr *>(parent, 0);
            const auto *storage = reinterpret_cast<const void *(*)(void *, unsigned)>(vtable[0x128 / sizeof(quintptr)])(parent, variable);
            if (!storage) storage = member<const void *>(descriptor, 0x10);
            if (!storage) return result;
            const auto count = member<unsigned>(storage, 12);
            if (!count || count > member<unsigned>(storage, 8) || !member<const void *>(storage, 0)) return result;
            // Native object-ID lookup returns both the child and its current index.
            auto *indexed = function<void *(*)(const void *, const void *, int *)>(image, 0x1ad3b20)(storage, entry + 8, &index);
            if (indexed != child || index < 0 || unsigned(index) >= count) return result;
        }
        const auto *childSchema = member<const void *>(child, 0x18);
        if (!childSchema) return result;
        const auto className = metadataText(member<const char *>(childSchema, 0x20));
        if (className.isEmpty()) return result;
        result.path.append({key, index, className});
    }
    auto *container = resolve(&path, true);
    if (!container) return result;
    const auto *schema = member<const void *>(container, 0x18);
    if (!schema) return result;
    result.key = metadataText(member<const char *>(leaf, 8));
    result.className = metadataText(member<const char *>(schema, 0x20));
    if (result.key.isEmpty() || result.className.isEmpty()) return result;
    result.arrayIndex = arrayIndex;
    result.leafIdentity = sourceIdentity(container);
    result.bindingFingerprint = QCryptographicHash::hash(identities, QCryptographicHash::Sha256);
    result.state = QStringLiteral("captured");
    return result;
}
}

PropertySourceValue readPropertySource(QObject *property, quintptr image, PropertyDestination *destination)
{
    if (destination) *destination = {};
    const auto *application = QCoreApplication::instance();
    if (!image || !application || QThread::currentThread() != application->thread())
        return {QStringLiteral("wrong_thread_or_unvalidated_host"), {}};
    if (!property || property->thread() != application->thread() ||
        member<quintptr>(property, 0) != image + 0x2e18ab0)
        return {QStringLiteral("unsupported_property"), {}};

    auto *grid = function<QObject *(*)(QObject *)>(image, 0x4b1410)(property);
    const auto *gridType = reinterpret_cast<const QMetaObject *>(image + 0x2e2c420);
    grid = gridType->cast(grid);
    if (!grid) return {QStringLiteral("unsupported_grid"), {}};
    auto *sourceProperty = property;
    int arrayIndex = -1;
    int rowCount = -1;
    auto *descriptor = function<const void *(*)(QObject *, QObject *)>(image, 0x318ca0)(grid, property);
    if (!descriptor) {
        // Individual StringArrayProperty children have no descriptor. Native
        // 140319ae0 forwards their writes to the descriptor-bearing parent.
        // 1404a3fd0 establishes QTreeWidgetItem at +0x10; 1404c2cc0 builds
        // one child per string in its array iteration order. Refuse a sorted
        // tree: sibling position alone would not prove the source index there.
        auto *parent = function<QObject *(*)(QObject *, bool)>(image, 0x4b1120)(property, false);
        if (!parent || member<quintptr>(parent, 0) != image + 0x2e1a5b0)
            return {QStringLiteral("property_descriptor_missing"), {}};
        auto *parentItem = reinterpret_cast<QTreeWidgetItem *>(reinterpret_cast<char *>(parent) + 0x10);
        auto *childItem = reinterpret_cast<QTreeWidgetItem *>(reinterpret_cast<char *>(property) + 0x10);
        auto *tree = parentItem->treeWidget();
        if (!tree || tree->isSortingEnabled())
            return {QStringLiteral("array_row_order_unverified"), {}};
        arrayIndex = parentItem->indexOfChild(childItem);
        rowCount = parentItem->childCount();
        if (arrayIndex < 0 || arrayIndex >= rowCount)
            return {QStringLiteral("array_row_missing"), {}};
        sourceProperty = parent;
        descriptor = function<const void *(*)(QObject *, QObject *)>(image, 0x318ca0)(grid, parent);
    }
    if (!descriptor) return {QStringLiteral("property_descriptor_missing"), {}};

    ScopedPath source{{}, image};
    function<void *(*)(QObject *, QObject *, bool, SourcePath *)>(image, 0x318670)
        (grid, sourceProperty, false, &source.path);
    if (!source.path.root) return {QStringLiteral("source_path_missing"), {}};
    if (source.path.count > 64 || source.path.count > source.path.capacity ||
        (source.path.count && !source.path.items))
        return {QStringLiteral("source_path_invalid"), {}};
    // Effective lookup, including inherited object members. This is observation,
    // not evidence of a local override or permission to edit the returned source.
    auto *container = function<void *(*)(SourcePath *, bool)>(image, 0x1b33bf0)(&source.path, true);
    if (!container) return {QStringLiteral("source_path_unresolved"), {}};
    const auto *schema = member<const void *>(container, 0x18);
    const auto index = member<unsigned short>(descriptor, 4);
    if (!schema || index >= member<unsigned>(schema, 0x34))
        return {QStringLiteral("source_schema_changed"), {}};
    const auto *descriptors = member<const char *>(schema, 0x28);
    if (!descriptors || member<unsigned>(descriptors + size_t(index) * 0x58, 0) != member<unsigned>(descriptor, 0))
        return {QStringLiteral("source_schema_changed"), {}};

    if (destination && arrayIndex < 0)
        *destination = captureDestination(source.path, descriptors + size_t(index) * 0x58, arrayIndex, image);
    const auto table = member<const quintptr *>(container, 0);
    // Match SetVariableValueAction's proven serializer precondition. The path
    // may resolve an inherited object, but a missing local field on that object
    // is not an observed empty value. Effective/default serialization remains
    // unverified and must not be used to confirm a write.
    const auto *localValue = reinterpret_cast<const void *(*)(void *, unsigned)>(table[0x130 / sizeof(quintptr)])(container, index);
    if (!localValue)
        return {QStringLiteral("no_local_value"), {}};
    if (arrayIndex >= 0) {
        if (!(member<unsigned>(descriptor, 0) & 0x80))
            return {QStringLiteral("array_type_changed"), {}};
        // +0x130 establishes presence only. The native StringArrayProperty
        // initializer obtains typed storage through +0x128 (query
        // 9ab259e0d4bd46fc99c8d8dbb65aecad), not the presence result.
        const auto *storage = reinterpret_cast<const void *(*)(void *, unsigned)>(table[0x128 / sizeof(quintptr)])(container, index);
        if (!storage) return {QStringLiteral("array_storage_missing"), {}};
        const auto count = member<unsigned>(storage, 12);
        const auto capacity = member<unsigned>(storage, 8);
        const auto *items = member<const char *const *>(storage, 0);
        if (!items || count > capacity || count != unsigned(rowCount) || unsigned(arrayIndex) >= count)
            return {QStringLiteral("array_storage_changed"), {}};
        // Borrow synchronously; do not decrement the container's string refs.
        // Native 1404c2cc0 explicitly presents a null string entry as empty.
        // Copy every entry before exposing a captured destination so a future
        // array transaction can validate/preserve all neighboring values.
        if (count > 4096) return {QStringLiteral("array_too_large"), {}};
        QStringList values;
        unsigned totalBytes = 0;
        for (unsigned i = 0; i < count; ++i) {
            const auto *text = items[i];
            const auto length = text ? member<unsigned>(text - 8, 0) : 0;
            if (length > 65536 - totalBytes) return {QStringLiteral("value_too_large"), {}};
            totalBytes += length;
            QString decoded;
            if (!decodeSourceText(QByteArrayView(text, length), decoded))
                return {QStringLiteral("invalid_utf8"), {}};
            values.append(decoded);
        }
        if (destination) {
            *destination = captureDestination(source.path, descriptors + size_t(index) * 0x58, arrayIndex, image);
            destination->arrayValues = values;
        }
        return {QStringLiteral("observed"), values[arrayIndex], arrayIndex};
    }
    ScopedString value{nullptr, image};
    reinterpret_cast<void (*)(void *, unsigned, char **)>(table[0x110 / sizeof(quintptr)])
        (container, index, &value.text);
    if (!value.text) return {QStringLiteral("null_serialized_value"), {}};
    const auto length = member<unsigned>(value.text - 8, 0);
    if (length > 65536) return {QStringLiteral("value_too_large"), {}};
    QString decoded;
    if (!decodeSourceText(QByteArrayView(value.text, length), decoded))
        return {QStringLiteral("invalid_utf8"), {}};
    return {QStringLiteral("observed"), decoded};
}
}
