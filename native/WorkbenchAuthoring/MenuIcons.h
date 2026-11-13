#pragma once
#include <QObject>
#include <QStringList>
#include <QVector>

namespace rst {
// Optional category presentation. Native commands remain owned by Workbench.
class MenuIcons final : public QObject
{
public:
    explicit MenuIcons(QObject *parent) : QObject(parent) {}
    QString load(const QString &directory);

protected:
    bool eventFilter(QObject *target, QEvent *event) override;

private:
    struct Entry { QString category; QStringList commands; QString iconCommand; QString bottomCommand; };
    QVector<Entry> entries;
};
}
