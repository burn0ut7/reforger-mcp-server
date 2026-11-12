#include "Compatibility.h"
#include "ChoicesClient.h"
#include "EditableChoices.h"
#include "ArraySelection.h"
#include "MenuIcons.h"

#include "PropertyEditors.h"
#include <QApplication>
#include <QDateTime>
#include <QDropEvent>
#include <QFile>
#include <QFileInfo>
#include <QGenericPlugin>
#include <QJsonArray>
#include <QJsonDocument>
#include <QMimeData>
#include <QPointer>
#include <QRegularExpression>
#include <QThread>
#include <QTimer>
#include <QWidget>

namespace rst::detail {
class ReferenceField final : public QObject
{
public:
    ReferenceField(QObject *property, QObject *editor, QComboBox *combo, QString providerName,
        quint16 selectedPort, QString build, std::function<void(QJsonObject)> logger)
        : QObject(editor), property(property), provider(std::move(providerName)), port(selectedPort),
          expectedBuild(std::move(build)), log(std::move(logger))
    {

        if (PropertyEditors::destination(property).arrayIndex >= 0)
            arraySelection = new ArraySelection(property, editor, combo, port, expectedBuild, log);
        const QPointer<ReferenceField> guard(this);
        binding = new EditableChoices(combo, [guard](quint64 id) { if (guard) guard->refresh(id); });
        connect(property, &QObject::destroyed, this, &QObject::deleteLater);
        QTimer::singleShot(0, this, [this] { if (binding) binding->refresh(); });
    }
    ~ReferenceField() override { delete binding; }

private:
    QPointer<QObject> property;
    QPointer<EditableChoices> binding;
    QPointer<QObject> request;
    QPointer<ArraySelection> arraySelection;

    QString provider;
    quint16 port;
    QString expectedBuild;
    std::function<void(QJsonObject)> log;
    void refresh(quint64 id)
    {
        delete request;
        if (!property || !binding || (arraySelection && arraySelection->isBusy())) return;
        const QPointer<ReferenceField> guard(this);
        request = ChoicesClient::fetch(this, port, expectedBuild, provider, [guard, id](ChoicesReply reply) {
            if (!guard || !guard->property || !guard->binding) return;
            if (guard->arraySelection && guard->arraySelection->isBusy()) return;

            QVector<ReferenceChoice> choices;
            if (reply.state == "ready") {
                choices.append({QStringLiteral("(None)"), {}, {}, {}});
                for (const auto &value : reply.values)
                    if (!value.isEmpty()) choices.append({value, value,
                        reply.duplicateValues.contains(value)
                            ? QStringLiteral("Multiple sources define this value. Check the definitions before using it.") : QString{}, {}});
            }
            const bool applied = guard->binding->apply(id, choices);
            if (applied && guard->arraySelection) guard->arraySelection->setWorld(reply.world);

            guard->log({{"event", "choicesReply"}, {"provider", guard->provider},
                {"state", reply.state}, {"world", reply.world}, {"count", choices.size()},
                {"applied", applied}, {"error", reply.error},
                {"duplicateValues", QJsonArray::fromStringList(reply.duplicateValues)}});
        }, ChoiceKind::Values);
    }
};

class Session final : public QObject
{
public:
    explicit Session(QObject *parent) : QObject(parent) {}

    int start(const QString &specification)
    {
        QString reportPath = specification;
        bool choicesEnabled = false;
        quint16 port = 0;
        QString expectedBuild;
        // Explicit @absolute-config.json enables the experimental choice path.
        // A plain path retains diagnostic-only activation for existing probes.
        if (specification.startsWith('@')) {
            const auto configPath = specification.mid(1);
            QFile config(configPath);
            if (!QFileInfo(configPath).isAbsolute() || !config.open(QIODevice::ReadOnly) || config.size() > 65536) return -2;
            QJsonParseError error;
            const auto document = QJsonDocument::fromJson(config.readAll(), &error);
            if (error.error != QJsonParseError::NoError || !document.isObject()) return -2;
            const auto settings = document.object();
            const auto selectedPort = settings.value("port").toInt(-1);
            expectedBuild = settings.value("adapterBuild").toString();
            static const QRegularExpression hash(QStringLiteral("^[A-Fa-f0-9]{64}$"));
            if (settings.value("schema").toInt() != 1 || settings.value("mode").toString() != "choices" ||
                selectedPort < 1 || selectedPort > 65535 || !hash.match(expectedBuild).hasMatch()) return -2;
            for (const auto &key : settings.keys())
                if (key != "schema" && key != "mode" && key != "port" && key != "adapterBuild" && key != "reportPath") return -2;
            reportPath = settings.value("reportPath").toString();
            port = quint16(selectedPort);
            choicesEnabled = true;
        }
        // Explicit opt-in diagnostic output; never overwrite previous evidence.
        if (reportPath.isEmpty() || !QFileInfo(reportPath).isAbsolute()) {
            qWarning("RST authoring: pass an absolute, new diagnostic report path after -plugin rstauthoring:");
            return -2;
        }
        report.setFileName(reportPath);
        if (!report.open(QIODevice::WriteOnly | QIODevice::NewOnly)) {
            qWarning("RST authoring: cannot create diagnostic report");
            return -2;
        }
        auto record = rst::inspectHost();
        const auto reason = rst::incompatibility(record);
        record.insert("event", "startup");
        record.insert("state", reason.isEmpty() ? (choicesEnabled ? "choices_initializing" : "diagnostics_only") : "incompatible");
        record.insert("reason", reason);
        record.insert("authoringEnabled", false);
        record.insert("schema", 1);
        write(record);
        if (!reason.isEmpty() || !qobject_cast<QApplication *>(QCoreApplication::instance())) return -1;
        if (choicesEnabled) {
            const QPointer<Session> guard(this);
            auto *editors = new PropertyEditors(this, [guard, port, expectedBuild](QObject *property,
                    QObject *editor, QComboBox *combo, const QString &provider, bool) {
                if (!guard) return;
                const auto source = PropertyEditors::sourceValue(property);
                guard->write({{"event", "propertySource"}, {"provider", provider},
                    {"state", source.state}, {"serializedValue", source.serializedValue},
                    {"arrayIndex", source.arrayIndex}});
                const auto destination = PropertyEditors::destination(property);
                QJsonArray path;
                for (const auto &entry : destination.path)
                    path.append(QJsonObject{{"key", entry.key}, {"index", entry.index},
                        {"className", entry.className}});
                guard->write({{"event", "propertyDestination"}, {"provider", provider},
                    {"state", destination.state}, {"entityIdHex", destination.entityIdHex},
                    {"key", destination.key}, {"className", destination.className},
                    {"path", path}, {"arrayIndex", destination.arrayIndex}});
                new ReferenceField(property, editor, combo, provider, port, expectedBuild,
                    [guard](QJsonObject record) { if (guard) guard->write(record); });
            });
            const auto hookError = editors->install();
            write({{"event", "propertyHook"}, {"state", hookError.isEmpty() ? "installed" : "unavailable"},
                {"error", hookError}, {"authoringEnabled", hookError.isEmpty()}});
            if (!hookError.isEmpty()) return -1;
            auto *menuIcons = new MenuIcons(this);
            const auto iconError = menuIcons->load(QFileInfo(record.value("extension").toObject().value("path").toString()).absolutePath());
            write({{"event", "menuIcons"}, {"state", iconError.isEmpty() ? "ready" : "unavailable"}, {"error", iconError}});
        }
        // Payload observation remains separate from the opt-in choice owner.
        QCoreApplication::instance()->installEventFilter(this);
        connect(QCoreApplication::instance(), &QCoreApplication::aboutToQuit, this, [this] {
            write({{"event", "shutdown"}});
        });
        return 1;
    }

protected:
    bool eventFilter(QObject *target, QEvent *event) override
    {
        if (event->type() != QEvent::DragEnter && event->type() != QEvent::Drop)
            return false;
        auto *drop = static_cast<QDropEvent *>(event);
        const auto *mime = drop->mimeData();
        QJsonArray formats;
        if (mime) {
            for (const auto &format : mime->formats())
                formats.append(format);
        }
        auto *source = drop->source();
        write({{"event", event->type() == QEvent::Drop ? "drop" : "dragEnter"},
            {"targetClass", target->metaObject()->className()}, {"targetName", target->objectName()},
            {"sourceClass", source ? source->metaObject()->className() : ""},
            {"formats", formats}, {"proposedAction", int(drop->proposedAction())}});
        // Do not accept, reject, decode/dereference payloads, or change normal editor behavior.
        return false;
    }

private:
    QFile report;
    int records = 0;
    void write(QJsonObject record)
    {
        if (!report.isOpen() || records >= 1024) return;
        record.insert("utc", QDateTime::currentDateTimeUtc().toString(Qt::ISODateWithMs));
        record.insert("sequence", records++);
        if (records == 1024) record.insert("recordLimitReached", true);
        const auto bytes = QJsonDocument(record).toJson(QJsonDocument::Compact) + '\n';
        if (report.write(bytes) != bytes.size() || !report.flush()) report.close();
    }
};

QPointer<Session> session;

int startSession(const QString &specification)
{
    auto *application = QCoreApplication::instance();
    if (!application || QThread::currentThread() != application->thread()) return -3;
    if (session) return 2;
    session = new Session(application);
    session->setObjectName(QStringLiteral("RST Workbench authoring diagnostics"));
    return session->start(specification);
}
}

// The MCP test adapter calls this exact DLL on the Workbench UI thread. Normal
// user startup uses Qt's generic-plugin entry point below and the same owner.
// Return: 1 session started, 2 already initialized, -1 incompatible/hook refused,
// -2 invalid/unwritable report, -3 not the application thread. Never unload live sessions.
extern "C" __declspec(dllexport) int RST_WorkbenchExtensionStart_v1(const wchar_t *specification)
{
    return rst::detail::startSession(specification ? QString::fromWCharArray(specification) : QString());
}

class AuthoringPlugin final : public QGenericPlugin
{
    Q_OBJECT
    Q_PLUGIN_METADATA(IID QGenericPluginFactoryInterface_iid FILE "Plugin.json")
public:
    QObject *create(const QString &name, const QString &specification) override
    {
        if (name.compare(QStringLiteral("rstauthoring"), Qt::CaseInsensitive) != 0) return nullptr;
        // Qt can call the factory before QApplication construction has finished.
        auto *activation = new QObject(QCoreApplication::instance());
        QTimer::singleShot(0, activation, [specification] { rst::detail::startSession(specification); });
        return activation;
    }
};

#include "Plugin.moc"
