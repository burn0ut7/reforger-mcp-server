#include "ChoicesClient.h"
#include <QCoreApplication>
#include <QEventLoop>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QTcpServer>
#include <QTcpSocket>
#include <QTimer>
#include <QtEndian>
#include <cstdio>
#include <memory>

static void require(bool condition, const char *name)
{
    if (!condition) { std::fprintf(stderr, "FAIL %s\n", name); std::exit(1); }
    std::printf("PASS %s\n", name);
}

static QByteArray frame(const QByteArray &data)
{
    QByteArray result(4, '\0');
    qToLittleEndian(quint32(data.size()), result.data());
    return result + data;
}

int main(int argc, char **argv)
{
    QCoreApplication app(argc, argv);
    const QString build(64, QChar('A'));
    for (int scenario = 0; scenario < 23; ++scenario) {
        auto lifetime = std::make_unique<QObject>();
        QTcpServer server;
        require(server.listen(QHostAddress::LocalHost, 0), "Open same-process test listener");
        QEventLoop loop;
        QTimer timeout;
        timeout.setSingleShot(true);
        QObject::connect(&timeout, &QTimer::timeout, &loop, &QEventLoop::quit);
        bool gotRequest = false;
        QObject::connect(&server, &QTcpServer::newConnection, lifetime.get(), [&] {
            auto *peer = server.nextPendingConnection();
            auto bytes = std::make_shared<QByteArray>();
            QObject::connect(peer, &QTcpSocket::readyRead, lifetime.get(), [&, peer, bytes] {
                *bytes += peer->readAll();
                if (gotRequest || bytes->size() < 4) return;
                require(qFromLittleEndian<quint32>(bytes->constData()) == 1, "NET API protocol version");
                qsizetype offset = 4;
                QList<QByteArray> parts;
                while (parts.size() < 3 && bytes->size() >= offset + 4) {
                    const auto length = qFromLittleEndian<quint32>(bytes->constData() + offset);
                    if (bytes->size() < offset + 4 + length) return;
                    parts.append(bytes->mid(offset + 4, length));
                    offset += 4 + length;
                }
                if (parts.size() != 3) return;
                gotRequest = true;
                const auto request = QJsonDocument::fromJson(parts[2]).object();
                if (scenario < 11)
                    require(parts[1] == "JsonRPC" && request.value("APIFunc") == "RST_EditorChoices" &&
                        request.value("provider") == "TEST_Choices", "Calls the existing script choice owner");
                QJsonObject reply{{"adapterBuild", scenario == 1 ? QString(64, QChar('B')) : build},
                    {"token", request.value("token")}, {"state", scenario == 2 ? "provider_unavailable" : "ready"},
                    {"world", "$test:world.ent"}, {"values", QJsonArray{"USSR", "US", "US", QString::fromUtf8("Français")}}};
                if (scenario >= 5 && scenario < 11) {
                    require(request.value("kind") == "entities", "Explicit entity identity request");
                    reply.insert("kind", "entities");
                    reply.insert("entityIds", QJsonArray{"opaque-id-1", "opaque-id-2"});
                    reply.insert("entityNames", QJsonArray{"", ""});
                    reply.insert("entityLabels", QJsonArray{"Same label", "Same label"});
                    if (scenario == 6) reply.insert("entityNames", QJsonArray{""});
                    if (scenario == 7) reply.insert("entityIds", QJsonArray{"same-id", "same-id"});
                    if (scenario == 8) reply.remove("kind");
                    if (scenario == 9) reply.insert("entityLabels", QJsonArray{QString(QChar(0)), "Other"});
                    if (scenario == 10) {
                        reply.remove("entityIds");
                        reply.remove("entityNames");
                        reply.remove("entityLabels");
                    }
                }
                if (scenario >= 11) {
                    require(parts[1] == "JsonRPC" && request.value("APIFunc") == (scenario == 22 ? "RST_StringArrayEdit" : "RST_EntityReferenceCommit") &&
                        request.value("token") == "test-receipt" && request.value("adapterBuild") == build,
                        "Calls edit owner with original preparation receipt and validated build");
                    reply.insert("state", scenario == 11 || scenario == 19 ? "prepared" : "committed");
                    reply.insert("name", "GeneratedEntity1");
                    if (scenario >= 19) {
                        require(request.value("operation") == (scenario == 22 ? "set_array_string" : "prepare_array") &&
                            request.value("expectedArray").toArray() == QJsonArray{" leading ", "a,b", "", QString::fromUtf8("Équipe"), ""},
                            "Array preparation preserves literal values and trailing empty entries");
                        if (scenario == 21) { reply.insert("state", "refused"); reply.insert("reason", "Array contents changed"); }
                    }
                    if (scenario == 14) reply.insert("token", "wrong-receipt");
                    if (scenario == 16) { reply.insert("state", "refused"); reply.insert("reason", "Destination changed"); }
                    if (scenario == 17) { reply.insert("state", "finalization_failed"); reply.insert("reason", "Inspect before retrying"); }
                }
                auto response = frame("Ok") + frame(QJsonDocument(reply).toJson(QJsonDocument::Compact));
                if (scenario == 3) {
                    response = QByteArray(4, '\0');
                    qToLittleEndian(quint32(65537), response.data());
                }
                if (scenario == 4 || scenario == 15) {
                    peer->write(response.left(7));
                    peer->disconnectFromHost();
                    return;
                }
                if (scenario == 18) {
                    peer->write(response);
                    peer->disconnectFromHost();
                    return;
                }
                // Deliberately split both frame headers and the JSON body.
                peer->write(response.left(1));
                QTimer::singleShot(5, peer, [peer, response] { peer->write(response.mid(1, 7)); });
                QTimer::singleShot(10, peer, [peer, response] { peer->write(response.mid(8)); });
            });
        });
        bool completed = false;
        rst::ChoicesReply observed;
        rst::EntityEditReply editObserved;
        if (scenario < 11) {
            rst::ChoicesClient::fetch(lifetime.get(), server.serverPort(), build, "TEST_Choices", [&](rst::ChoicesReply reply) {
                observed = std::move(reply); completed = true; loop.quit();
            }, scenario >= 5 ? rst::ChoiceKind::Entities : rst::ChoiceKind::Values);
        } else {
            rst::ChoicesClient::edit(lifetime.get(), server.serverPort(), build,
                {{"operation", scenario == 22 ? "set_array_string" : (scenario >= 19 ? "prepare_array" : (scenario == 11 || scenario == 13 ? "prepare_scalar" : "commit_scalar"))},
                 {"expectedArray", QJsonArray{" leading ", "a,b", "", QString::fromUtf8("Équipe"), ""}},
                 {"token", "test-receipt"}, {"APIFunc", "must-be-replaced"}, {"adapterBuild", "must-be-replaced"}},
                [&](rst::EntityEditReply reply) {
                    editObserved = std::move(reply); completed = true;
                    if (scenario == 18) lifetime.reset();
                    loop.quit();
                });
        }
        timeout.start(3000);
        loop.exec();
        require(gotRequest && completed, "Asynchronous request completed without blocking its server");
        if (scenario == 0) require(observed.state == "ready" && observed.world == "$test:world.ent" &&
            observed.values == QStringList{QString::fromUtf8("Français"), "US", "USSR"}, "Fragmented UTF-8 response sorted and deduplicated");
        if (scenario == 0) require(observed.duplicateValues == QStringList{"US"},
            "Deduplication retains duplicate-source evidence");
        if (scenario == 1) require(observed.state == "unavailable" && observed.error.contains("identity"), "Reject wrong adapter build");
        if (scenario == 2) require(observed.state == "provider_unavailable", "Preserve unsupported provider state");
        if (scenario == 3) require(observed.state == "unavailable" && observed.error.contains("64 KiB"), "Reject oversized frame");
        if (scenario == 4) require(observed.state == "unavailable", "Reject incomplete response on disconnect");
        if (scenario == 5) require(observed.state == "ready" && observed.entities.size() == 2 &&
            observed.entities[0].id == "opaque-id-1" && observed.entities[1].id == "opaque-id-2" &&
            observed.entities[0].name.isEmpty() && observed.values.isEmpty(), "Keep distinct unnamed entities with identical labels");
        if (scenario >= 6 && scenario <= 9) require(observed.state == "unavailable" && observed.entities.isEmpty(),
            "Reject malformed entity metadata without partial choices");
        if (scenario == 10) require(observed.state == "ready" && observed.entities.isEmpty(),
            "Accept omitted empty entity arrays");
        if (scenario == 11) require(editObserved.state == "prepared", "Accept preparation response");
        if (scenario == 12) require(editObserved.state == "committed" && editObserved.name == "GeneratedEntity1", "Accept committed name");
        if (scenario == 13) require(editObserved.state == "unavailable" && editObserved.error.contains("phase"), "Reject mutation result for read-only preparation");
        if (scenario == 14) require(editObserved.state == "unavailable" && editObserved.error.contains("receipt"), "Reject edit receipt mismatch");
        if (scenario == 15) require(editObserved.state == "unavailable", "Incomplete edit reply remains uncertain without retry");
        if (scenario == 16) require(editObserved.state == "refused" && editObserved.reason == "Destination changed", "Preserve refusal reason");
        if (scenario == 17) require(editObserved.state == "finalization_failed" && editObserved.reason == "Inspect before retrying", "Preserve uncertain finalization outcome");
        if (scenario == 18) require(!lifetime && editObserved.state == "committed", "Completion may destroy request lifetime");
        if (scenario == 19) require(editObserved.state == "prepared" && editObserved.name == "GeneratedEntity1", "Array preparation returns proposed name without commit");
        if (scenario == 20) require(editObserved.state == "unavailable" && editObserved.error.contains("phase"), "Reject mutation result for array preparation");
        if (scenario == 21) require(editObserved.state == "refused" && editObserved.reason == "Array contents changed", "Preserve array preparation refusal");
        if (scenario == 22) require(editObserved.state == "committed", "Direct string-array edit reaches its owning handler");
    }
    {
        QObject lifetime;
        QTcpServer server;
        require(server.listen(QHostAddress::LocalHost, 0), "Open cancellation test listener");
        bool completed = false;
        auto *request = rst::ChoicesClient::edit(&lifetime, server.serverPort(), build,
            {{"operation", "prepare_scalar"}, {"token", "cancel-before-dispatch"}},
            [&](rst::EntityEditReply) { completed = true; });
        delete request;
        QCoreApplication::processEvents();
        require(!completed && !server.hasPendingConnections(), "Cancellation before dispatch sends no edit and invokes no callback");
    }
    return 0;
}
