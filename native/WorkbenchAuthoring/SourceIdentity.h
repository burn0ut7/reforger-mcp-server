#pragma once
#include <QCryptographicHash>
#include <cstring>

namespace rst {
// Only call for an already resolved live container. The result is an opaque,
// process-local comparison value, never a pointer to dereference later.
inline QByteArray sourceIdentity(const void *container)
{
    QByteArray bytes;
    const auto address = reinterpret_cast<quintptr>(container);
    quint64 identity = 0;
    std::memcpy(&identity, static_cast<const char *>(container) + 8, sizeof(identity));
    bytes.append(reinterpret_cast<const char *>(&address), sizeof(address));
    bytes.append(reinterpret_cast<const char *>(&identity), sizeof(identity));
    return QCryptographicHash::hash(bytes, QCryptographicHash::Sha256);
}
}
