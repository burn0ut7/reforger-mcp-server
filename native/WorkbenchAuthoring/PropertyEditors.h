#pragma once
#include "PropertySource.h"
#include <QComboBox>
#include <QObject>
#include <QString>
#include <functional>

namespace rst {
// Direct StringProperty editor-creation hook. No widget-tree scanning. This
// remains resident with the DLL; a different build requires Workbench restart.
class PropertyEditors final : public QObject
{
public:
    using Created = std::function<void(QObject *, QObject *, QComboBox *, const QString &, bool)>;
    PropertyEditors(QObject *parent, Created created);
    QString install(); // Empty on success; checks exact host and unmodified slot.
    static PropertySourceValue sourceValue(QObject *property);
    static PropertyDestination destination(QObject *property);
    static QString routeSelection(QObject *editor, QComboBox *combo,
        std::function<void(int)> activated, std::function<void()> finished);
    static void nativeActivate(QObject *editor);
    static void nativeFinish(QObject *editor);

private:
    Created created;
    static QWidget *createEditor(QObject *property, QWidget *parent);
};
}
