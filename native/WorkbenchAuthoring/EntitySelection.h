#pragma once
#include "PropertySource.h"
#include <QComboBox>
#include <QJsonObject>
#include <QPointer>
#include <functional>

namespace rst {
class EntitySelection final : public QObject
{
public:
    EntitySelection(QObject *property, QObject *editor, QComboBox *combo, QString provider,
        quint16 port, QString build, std::function<void(QJsonObject)> log);
    bool ready() const { return enabled; }
    void setWorld(QString value) { world = std::move(value); }
    bool isBusy() const { return busy; }
private:
    QPointer<QObject> property, editor;
    QPointer<QComboBox> combo;
    QString provider, build, world, initialValue;
    quint16 port;
    std::function<void(QJsonObject)> log;
    bool enabled = false, busy = false;
    void activate(int index);
    void finishText();
    void restoreText(const QString &value);
    void finish(const QString &state, const QString &name, const QString &reason);
    void pick(int index);
};
}
