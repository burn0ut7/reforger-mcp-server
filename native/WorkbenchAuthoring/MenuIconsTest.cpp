#include "MenuIcons.h"
#include <QAction>
#include <QApplication>
#include <QEvent>
#include <QFile>
#include <QMenu>
#include <QTemporaryDir>

int main(int argc, char **argv)
{
    QApplication app(argc, argv);
    QTemporaryDir directory;
    QFile config(directory.filePath("menu-icons.json"));
    if (!config.open(QIODevice::WriteOnly)) return 1;
    config.write(R"({"schema":1,"categories":[{"category":"Example","commands":["Author","Install","Update"],"iconCommand":"Author","bottomCommand":"Install"}]})");
    config.close();
    rst::MenuIcons presentation(&app);
    if (!presentation.load(directory.path()).isEmpty()) return 2;
    QMenu parent;
    auto *category = parent.addMenu("Example");
    auto *author = category->addAction("Author");
    auto *install = category->addAction("Install");
    auto *update = category->addAction("Update");
    int calls = 0;
    QObject::connect(install, &QAction::triggered, [&calls] { ++calls; });
    QEvent show(QEvent::Show);
    QCoreApplication::sendEvent(&parent, &show);
    if (category->actions() != QList<QAction *>({author, update, install})) return 3;
    QCoreApplication::sendEvent(&parent, &show);
    if (category->actions() != QList<QAction *>({author, update, install})) return 4;
    install->trigger();
    if (calls != 1) return 5;
    // A new/unknown command must keep the whole category untouched.
    category->removeAction(install);
    category->insertAction(update, install);
    category->addAction("Unrelated");
    const auto original = category->actions();
    QCoreApplication::sendEvent(&parent, &show);
    if (category->actions() != original) return 6;
    if (!config.open(QIODevice::WriteOnly | QIODevice::Truncate)) return 7;
    config.write(R"({"schema":1,"categories":[{"category":"Example","commands":["Author","Install","Update","Unrelated"],"iconCommand":"Author"}]})");
    config.close();
    if (!presentation.load(directory.path()).isEmpty()) return 8;
    category->insertSeparator(install);
    const auto withSeparator = category->actions();
    QCoreApplication::sendEvent(&parent, &show);
    if (category->actions() != withSeparator) return 9;
    return 0;
}
