#include <QCoreApplication>
#include <QFile>
#include <QGenericPlugin>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QLibrary>
#include <QPluginLoader>
#include <QTemporaryDir>
#include <QTextStream>
#include <thread>

int main(int argc, char **argv)
{
    QCoreApplication application(argc, argv);
    if (argc != 2 && argc != 3) return 1;
    QTemporaryDir directory;
    if (!directory.isValid()) return 2;
    QLibrary library(QString::fromLocal8Bit(argv[1]));
    library.setLoadHints(QLibrary::PreventUnloadHint);
    const auto entry = reinterpret_cast<int (*)(const wchar_t *)>(library.resolve("RST_WorkbenchExtensionStart_v1"));
    if (!entry) { QTextStream(stderr) << library.errorString() << '\n'; return 3; }
    const auto reportPath = directory.filePath("refusal.jsonl").toStdWString();
    if (argc == 3 && QByteArray(argv[2]) == "config") {
        const auto configPath = directory.filePath("choices.json");
        QFile config(configPath);
        if (!config.open(QIODevice::WriteOnly)) return 16;
        config.write(QJsonDocument(QJsonObject{{"schema", 1}, {"mode", "choices"},
            {"reportPath", QString::fromStdWString(reportPath)}, {"port", 9841},
            {"adapterBuild", QString(64, QChar('A'))}}).toJson());
        config.close();
        const auto specification = (QStringLiteral("@") + configPath).toStdWString();
        if (entry(specification.c_str()) != -1) return 17;
        QFile report(QString::fromStdWString(reportPath));
        if (!report.open(QIODevice::ReadOnly)) return 18;
        const auto result = QJsonDocument::fromJson(report.readAll().trimmed()).object();
        if (result.value("state") != "incompatible") return 19;
        QTextStream(stdout) << "PASS: explicit choice configuration still refuses an incompatible host\n";
        return 0;
    }
    if (argc == 3) {
        QPluginLoader loader(QString::fromLocal8Bit(argv[1]));
        if (!loader.metaData().value("MetaData").toObject().value("Keys").toArray().contains("rstauthoring")) return 10;
        auto *plugin = qobject_cast<QGenericPlugin *>(loader.instance());
        if (!plugin || plugin->create("unknown", QString::fromStdWString(reportPath))) return 11;
        if (!plugin->create("rstauthoring", QString::fromStdWString(reportPath))) return 12;
        QCoreApplication::processEvents();
        QFile report(QString::fromStdWString(reportPath));
        if (!report.open(QIODevice::ReadOnly)) return 13;
        const auto result = QJsonDocument::fromJson(report.readAll().trimmed()).object();
        if (result.value("state").toString() != "incompatible" || result.value("authoringEnabled").toBool(true)) return 14;
        if (result.value("extension").toObject().value("sha256").toString().size() != 64) return 15;
        QTextStream(stdout) << "PASS: Qt plugin metadata/factory reaches shared guarded initializer\n";
        return 0;
    }
    int wrongThread = 0;
    std::thread thread([&] { wrongThread = entry(reportPath.c_str()); });
    thread.join();
    if (wrongThread != -3 || QFile::exists(QString::fromStdWString(reportPath))) return 4;
    if (entry(reportPath.c_str()) != -1) return 5;
    QFile report(QString::fromStdWString(reportPath));
    if (!report.open(QIODevice::ReadOnly)) return 6;
    const auto bytes = report.readAll();
    const auto result = QJsonDocument::fromJson(bytes.trimmed()).object();
    if (result.value("state").toString() != "incompatible" || result.value("authoringEnabled").toBool(true)) return 7;
    if (entry(reportPath.c_str()) != 2) return 8;
    report.seek(0);
    if (report.readAll() != bytes) return 9;
    QTextStream(stdout) << "PASS: exact DLL export, thread refusal, unsupported-host report, duplicate activation preserves evidence\n";
    return 0;
}
