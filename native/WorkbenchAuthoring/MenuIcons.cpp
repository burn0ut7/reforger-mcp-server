#include "MenuIcons.h"
#include <QAction>
#include <QCoreApplication>
#include <QDir>
#include <QEvent>
#include <QFile>
#include <QIcon>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QMenu>

namespace rst {
QString MenuIcons::load(const QString &directory)
{
    QFile file(QDir(directory).filePath(QStringLiteral("menu-icons.json")));
    if (!file.exists()) return {}; // Generic authoring packages need no branding.
    if (!file.open(QIODevice::ReadOnly) || file.size() > 16384)
        return QStringLiteral("Cannot read menu icon configuration");
    QJsonParseError error;
    const auto document = QJsonDocument::fromJson(file.readAll(), &error);
    if (error.error != QJsonParseError::NoError || !document.isObject())
        return QStringLiteral("Invalid menu icon configuration");
    const auto root = document.object();
    const auto configured = root.value("categories").toArray();
    if (root.value("schema").toInt() != 1 || configured.isEmpty() || configured.size() > 16)
        return QStringLiteral("Unsupported menu icon configuration");
    QVector<Entry> loaded;
    for (const auto &value : configured) {
        const auto record = value.toObject();
        Entry entry;
        entry.category = record.value("category").toString();
        entry.iconCommand = record.value("iconCommand").toString();
        if (entry.category.isEmpty() || entry.iconCommand.isEmpty())
            return QStringLiteral("Invalid menu icon entry");
        for (const auto &command : record.value("commands").toArray()) {
            if (!command.isString() || command.toString().isEmpty())
                return QStringLiteral("Invalid menu icon command");
            entry.commands.append(command.toString());
        }
        if (!entry.commands.contains(entry.iconCommand))
            return QStringLiteral("Menu icon source is not an expected command");
        loaded.append(entry);
    }
    entries = loaded;
    QCoreApplication::instance()->installEventFilter(this);
    return {};
}

bool MenuIcons::eventFilter(QObject *target, QEvent *event)
{
    if (event->type() != QEvent::Show) return false;
    auto *menu = qobject_cast<QMenu *>(target);
    if (!menu) return false;
    // Workbench's category builder has no icon parameter. Inspect only the menu
    // being shown, never the widget tree, and leave unrelated menus untouched.
    for (auto *action : menu->actions()) {
        auto *categoryMenu = action->menu();
        if (!categoryMenu) continue;
        for (const auto &entry : entries) {
            if (action->text() != entry.category) continue;
            QStringList commands;
            QAction *iconSource = nullptr;
            for (auto *command : categoryMenu->actions()) {
                if (!command->isSeparator()) commands.append(command->text());
                if (command->text() == entry.iconCommand) iconSource = command;
            }
            if (commands.size() != entry.commands.size()) continue;
            bool matches = true;
            for (const auto &expected : entry.commands)
                if (!commands.contains(expected)) { matches = false; break; }
            if (matches && iconSource && !iconSource->icon().isNull())
                action->setIcon(iconSource->icon());
        }
    }
    return false;
}
}
