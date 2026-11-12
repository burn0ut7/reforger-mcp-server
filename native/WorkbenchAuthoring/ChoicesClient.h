#pragma once
#include <QObject>
#include <QJsonObject>
#include <QStringList>
#include <QVector>
#include <functional>

namespace rst {
struct EntityChoice {
    QString id;
    QString name;
    QString label;
};
enum class ChoiceKind { Values, Entities };
struct ChoicesReply {
    QString state;
    QString world;
    QStringList values;
    QString error;
    QStringList duplicateValues;
    QVector<EntityChoice> entities;
};
struct EntityEditReply {
    QString state;
    QString name;
    QString reason;
    QString error;
};

// Asynchronous access to this Workbench process's existing NET API script
// handler. No helper process, external host, retry, or alternate executor.
class ChoicesClient
{
public:
    using Completed = std::function<void(ChoicesReply)>;
    using EditCompleted = std::function<void(EntityEditReply)>;
    // Destroy the returned object to cancel observation. The handler is read-only.
    static QObject *fetch(QObject *lifetime, quint16 port, const QString &adapterBuild,
        const QString &provider, Completed completed, ChoiceKind kind = ChoiceKind::Values);
    // prepare_scalar is read-only; commit_scalar may write and is never retried.
    // Destroying the returned object cancels observation, not a dispatched edit.
    // Caller supplies one token shared by preparation and its single commit.
    static QObject *edit(QObject *lifetime, quint16 port, const QString &adapterBuild,
        QJsonObject request, EditCompleted completed);
};
}
