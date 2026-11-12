#pragma once
#include <QComboBox>
#include <QPointer>
#include <QJsonObject>
#include <functional>

namespace rst {
class ArraySelection final : public QObject {
public:
    ArraySelection(QObject *property, QObject *editor, QComboBox *combo, quint16 port,
        QString build, std::function<void(QJsonObject)> log);
    void setWorld(QString value) { world = std::move(value); }
    bool isBusy() const { return busy; }
private:
    QPointer<QObject> property;
    QPointer<QComboBox> combo;
    quint16 port;
    QString build, world;
    std::function<void(QJsonObject)> log;
    bool busy = false;
    void commit(const QString &value);
};
}
