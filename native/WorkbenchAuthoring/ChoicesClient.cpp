#include "ChoicesClient.h"
#include <QHostAddress>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QPointer>
#include <QRegularExpression>
#include <QSet>
#include <QStringConverter>
#include <QTcpSocket>
#include <QTimer>
#include <QUuid>
#include <QtEndian>
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <winsock2.h>
#include <windows.h>
#include <iphlpapi.h>
#include <vector>

namespace rst::detail {
bool ownsListener(quint16 port)
{
    ULONG size = 0;
    if (GetExtendedTcpTable(nullptr, &size, FALSE, AF_INET, TCP_TABLE_OWNER_PID_LISTENER, 0) != ERROR_INSUFFICIENT_BUFFER)
        return false;
    std::vector<unsigned char> storage(size);
    if (GetExtendedTcpTable(storage.data(), &size, FALSE, AF_INET, TCP_TABLE_OWNER_PID_LISTENER, 0) != NO_ERROR)
        return false;
    const auto *table = reinterpret_cast<const MIB_TCPTABLE_OWNER_PID *>(storage.data());
    for (DWORD i = 0; i < table->dwNumEntries; ++i) {
        const auto &row = table->table[i];
        if (qFromBigEndian(quint16(row.dwLocalPort)) == port &&
            (row.dwLocalAddr == 0 || row.dwLocalAddr == 0x0100007f) && row.dwOwningPid == GetCurrentProcessId()) return true;
    }
    return false;
}

class ChoicesRequest final : public QObject
{
public:
    ChoicesRequest(QObject *parent, quint16 selectedPort, QString build, QString selectedProvider,
        ChoicesClient::Completed callback, ChoiceKind selectedKind,
        QJsonObject edit = {}, ChoicesClient::EditCompleted editCallback = {})
        : QObject(parent), port(selectedPort), expectedBuild(std::move(build)), provider(std::move(selectedProvider)), kind(selectedKind),
          completed(std::move(callback)), editRequest(std::move(edit)), editCompleted(std::move(editCallback))
    {
        if (editCompleted) receipt = editRequest.value("token").toString();
        socket = new QTcpSocket(this);
        socket->setReadBufferSize(131081);
        deadline = new QTimer(this);
        deadline->setSingleShot(true);
        connect(deadline, &QTimer::timeout, this, [this] { fail("Choice request timed out"); });
        connect(socket, &QTcpSocket::errorOccurred, this, [this] { fail(socket->errorString()); });
        connect(socket, &QTcpSocket::disconnected, this, [this] {
            const QPointer<ChoicesRequest> guard(this);
            receive();
            if (guard && !finished) fail("Choice connection closed before a complete reply");
        });
        connect(socket, &QTcpSocket::readyRead, this, [this] { receive(); });
        connect(socket, &QTcpSocket::connected, this, [this] {
            if (!ownsListener(port)) { fail("NET API listener is not owned by this Workbench"); return; }
            QJsonObject request{{"APIFunc", "RST_EditorChoices"}, {"provider", provider}, {"token", receipt}};
            if (kind == ChoiceKind::Entities) request.insert("kind", "entities");
            if (editCompleted) {
                request = editRequest;
                request.insert("APIFunc", editRequest.value("operation") == "set_array_string" ? "RST_StringArrayEdit" : "RST_EntityReferenceCommit");
                request.insert("adapterBuild", expectedBuild);
            }
            const auto encoded = QJsonDocument(request).toJson(QJsonDocument::Compact);
            if (encoded.size() > 65536) { fail("Request exceeds 64 KiB"); return; }
            QByteArray message;
            appendInt(message, 1);
            for (const auto &part : {QByteArray("ReforgerAuthoring"), QByteArray("JsonRPC"), encoded}) {
                appendInt(message, quint32(part.size()));
                message += part;
            }
            if (socket->write(message) != message.size()) fail("Cannot queue complete choice request");
        });
        QTimer::singleShot(0, this, [this] { start(); });
    }

private:
    quint16 port;
    QString expectedBuild, provider;
    ChoiceKind kind;
    QString receipt = QUuid::createUuid().toString(QUuid::Id128);
    ChoicesClient::Completed completed;
    QJsonObject editRequest;
    ChoicesClient::EditCompleted editCompleted;
    QTcpSocket *socket;
    QTimer *deadline;
    QByteArray input;
    QList<QByteArray> frames;
    bool finished = false;

    static void appendInt(QByteArray &bytes, quint32 number)
    {
        char encoded[4];
        qToLittleEndian(number, encoded);
        bytes.append(encoded, 4);
    }
    void start()
    {
        static const QRegularExpression identifier(QStringLiteral("^[A-Za-z_][A-Za-z0-9_]{0,95}$"));
        static const QRegularExpression hash(QStringLiteral("^[A-Fa-f0-9]{64}$"));
        if (!port || (!editCompleted && !identifier.match(provider).hasMatch()) || !hash.match(expectedBuild).hasMatch()) {
            fail("Invalid provider, port or adapter build"); return;
        }
        if (editCompleted) {
            const auto operation = editRequest.value("operation").toString();
            static const QRegularExpression token(QStringLiteral("^[A-Za-z0-9_-]{1,64}$"));
            if ((operation != "prepare_scalar" && operation != "prepare_array" && operation != "commit_scalar" && operation != "set_array_string") ||
                !token.match(receipt).hasMatch() || QJsonDocument(editRequest).toJson(QJsonDocument::Compact).size() > 65536) {
                fail("Invalid entity edit operation, token or request size"); return;
            }
        }
        if (!ownsListener(port)) { fail("NET API listener is not owned by this Workbench"); return; }
        deadline->start(15000);
        socket->connectToHost(QHostAddress::LocalHost, port);
    }
    void fail(const QString &error)
    {
        if (editCompleted) {
            finishEdit({QStringLiteral("unavailable"), {}, {}, error});
            return;
        }
        finish({"unavailable", {}, {}, error, {}, {}});
    }
    void finishEdit(EntityEditReply reply)
    {
        if (finished) return;
        finished = true;
        deadline->stop();
        socket->abort();
        auto callback = std::move(editCompleted);
        deleteLater();
        if (callback) callback(std::move(reply));
    }
    void finish(ChoicesReply reply)
    {
        if (finished) return;
        finished = true;
        deadline->stop();
        socket->abort();
        auto callback = std::move(completed);
        deleteLater();
        if (callback) callback(std::move(reply));
    }
    void receive()
    {
        if (finished) return;
        input += socket->readAll();
        if (input.size() > 131080) { fail("Choice response exceeds its frame bounds"); return; }
        while (frames.size() < 2 && input.size() >= 4) {
            const auto length = qFromLittleEndian<quint32>(input.constData());
            if (length > 65536) { fail("Choice response frame exceeds 64 KiB"); return; }
            if (input.size() < qsizetype(length) + 4) return;
            frames.append(input.mid(4, length));
            input.remove(0, qsizetype(length) + 4);
        }
        if (frames.size() != 2) return;
        if (!input.isEmpty() || frames[0] != "Ok") { fail("NET API rejected the choice request or returned extra data"); return; }
        QStringDecoder decoder(QStringDecoder::Utf8);
        const QString decoded = decoder.decode(frames[1]);
        if (decoder.hasError()) { fail("Choice reply contains invalid UTF-8"); return; }
        QJsonParseError parseError;
        const auto document = QJsonDocument::fromJson(decoded.toUtf8(), &parseError);
        const auto body = document.object();
        if (parseError.error != QJsonParseError::NoError || !document.isObject() ||
            body.value("adapterBuild").toString() != expectedBuild || body.value("token").toString() != receipt) {
            fail("Choice adapter identity or receipt mismatch"); return;
        }
        const auto state = body.value("state").toString();
        if (editCompleted) {
            const QStringList states{"prepared", "refused", "committed", "unchanged", "rolled_back", "rollback_failed", "finalization_failed"};
            if (!states.contains(state) ||
                (body.contains("name") && !body.value("name").isString()) ||
                (body.contains("reason") && !body.value("reason").isString()) ||
                body.value("name").toString().contains(QChar(0)) || body.value("reason").toString().contains(QChar(0))) {
                fail("Malformed entity edit reply; inspect before retrying"); return;
            }
            const auto operation = editRequest.value("operation").toString();
            const bool preparing = operation == "prepare_scalar" || operation == "prepare_array";
            if ((preparing && state != "prepared" && state != "refused") || (!preparing && state == "prepared")) {
                fail("Entity edit phase mismatch; inspect before retrying"); return;
            }
            finishEdit({state, body.value("name").toString(), body.value("reason").toString(), {}});
            return;
        }
        if (state == "world_unavailable" || state == "provider_unavailable") { finish({state, {}, {}, {}, {}, {}}); return; }
        if (state != "ready" || !body.value("world").isString() ||
            (body.contains("values") && !body.value("values").isArray())) { fail("Malformed choice reply"); return; }
        if (kind == ChoiceKind::Entities) {
            if (body.value("kind").toString() != "entities") { fail("Entity choice kind mismatch"); return; }
            for (const auto &key : {"entityIds", "entityNames", "entityLabels"}) {
                if (body.contains(key) && !body.value(key).isArray()) { fail("Malformed entity choices"); return; }
            }
            const auto ids = body.value("entityIds").toArray();
            const auto names = body.value("entityNames").toArray();
            const auto labels = body.value("entityLabels").toArray();
            if (ids.size() != names.size() || ids.size() != labels.size()) { fail("Entity choice lengths differ"); return; }
            QVector<EntityChoice> entities;
            QSet<QString> seenIds;
            for (qsizetype i = 0; i < ids.size(); ++i) {
                if (!ids[i].isString() || !names[i].isString() || !labels[i].isString() ||
                    ids[i].toString().isEmpty() || ids[i].toString().contains(QChar(0)) ||
                    names[i].toString().contains(QChar(0)) || labels[i].toString().contains(QChar(0)) ||
                    seenIds.contains(ids[i].toString())) { fail("Invalid or duplicate entity identity"); return; }
                seenIds.insert(ids[i].toString());
                entities.append({ids[i].toString(), names[i].toString(), labels[i].toString()});
            }
            // IDs are opaque until the owning commit path revalidates them.
            // Never deduplicate by label/name: unnamed entities share those.
            finish({state, body.value("world").toString(), {}, {}, {}, entities});
            return;
        }
        QStringList values;
        QSet<QString> seen;
        QSet<QString> duplicates;
        // JsonApiStruct omits an empty registered array.
        for (const auto &entry : body.value("values").toArray()) {
            if (!entry.isString() || entry.toString().contains(QChar(0))) { fail("Invalid choice value"); return; }
            const auto value = entry.toString();
            if (!seen.contains(value)) { seen.insert(value); values.append(value); }
            else if (!value.isEmpty()) duplicates.insert(value);
        }
        values.sort(Qt::CaseSensitive);
        QStringList duplicateValues = duplicates.values();
        duplicateValues.sort(Qt::CaseSensitive);
        finish({state, body.value("world").toString(), values, {}, duplicateValues, {}});
    }
};
}

namespace rst {
QObject *ChoicesClient::fetch(QObject *lifetime, quint16 port, const QString &adapterBuild,
    const QString &provider, Completed completed, ChoiceKind kind)
{
    return new detail::ChoicesRequest(lifetime, port, adapterBuild, provider, std::move(completed), kind);
}
QObject *ChoicesClient::edit(QObject *lifetime, quint16 port, const QString &adapterBuild,
    QJsonObject request, EditCompleted completed)
{
    return new detail::ChoicesRequest(lifetime, port, adapterBuild, {}, {}, ChoiceKind::Values,
        std::move(request), std::move(completed));
}
}
