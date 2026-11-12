#include "ArraySelection.h"
#include "PropertyEditors.h"
#include "ChoicesClient.h"
#include <QCoreApplication>
#include <QAbstractItemView>
#include <QCompleter>
#include <QItemSelectionModel>
#include <QJsonArray>
#include <QLineEdit>
#include <QSignalBlocker>
#include <QUuid>

namespace rst {
ArraySelection::ArraySelection(QObject *source, QObject *editor, QComboBox *widget,
    quint16 selectedPort, QString expectedBuild, std::function<void(QJsonObject)> logger)
    : QObject(editor), property(source), combo(widget), port(selectedPort),
      build(std::move(expectedBuild)), log(std::move(logger))
{
    const QPointer<ArraySelection> guard(this);
    const auto error = PropertyEditors::routeSelection(editor, widget,
        [guard](int index) {
            if (guard && guard->combo && index >= 0)
                guard->commit(guard->combo->itemData(index).toString());
        }, [guard] {
            if (!guard || !guard->combo) return;
            const auto *completer = guard->combo->completer();
            const auto *popup = completer ? completer->popup() : nullptr;
            // Qt finishes text before activating an accepted completion.
            if (popup && popup->isVisible() && popup->currentIndex().isValid() &&
                popup->selectionModel() && popup->selectionModel()->isSelected(popup->currentIndex())) return;
            guard->commit(guard->combo->currentText());
        });
    if (!error.isEmpty()) {
        widget->setEnabled(false);
        widget->setToolTip(error);
        log({{"event", "arraySelection"}, {"state", "unavailable"}, {"reason", error}});
    }
}
void ArraySelection::commit(const QString &value)
{
    if (busy || !property || !combo) return;
    const auto destination = PropertyEditors::destination(property);
    bool validId = false;
    const auto id = destination.entityIdHex.toULongLong(&validId, 16);
    if (world.isEmpty() || !validId || destination.state != "captured" ||
        destination.arrayIndex < 0 || destination.arrayIndex >= destination.arrayValues.size()) {
        combo->setToolTip(QStringLiteral("Array destination unavailable; refresh the field"));
        return;
    }
    if (destination.arrayValues[destination.arrayIndex] == value) return;
    QJsonArray keys, indices, classes;
    for (const auto &entry : destination.path) {
        keys.append(entry.key); indices.append(entry.index); classes.append(entry.className);
    }
    QJsonObject request{{"operation", "set_array_string"}, {"token", QUuid::createUuid().toString(QUuid::Id128)},
        {"world", world}, {"destinationHi", qint32(id >> 32)}, {"destinationLo", qint32(id)},
        {"expectedClass", destination.className}, {"key", destination.key},
        {"arrayIndex", destination.arrayIndex}, {"value", value},
        {"expectedArray", QJsonArray::fromStringList(destination.arrayValues)},
        {"pathKeys", keys}, {"pathIndices", indices}, {"pathClasses", classes}};
    busy = true;
    combo->setEnabled(false);
    const QPointer<ArraySelection> guard(this);
    const auto logger = log;
    ChoicesClient::edit(QCoreApplication::instance(), port, build, request,
        [guard, logger](EntityEditReply reply) {
            logger({{"event", "arrayCommit"}, {"state", reply.state}, {"value", reply.name},
                {"reason", reply.reason}, {"error", reply.error}});
            if (!guard) return;
            guard->busy = false;
            if (!guard->combo) return;
            if (!reply.error.isEmpty() || reply.state == "finalization_failed") {
                guard->combo->setEnabled(false);
                guard->combo->setToolTip((reply.error.isEmpty() ? reply.reason : reply.error) + QStringLiteral("; outcome unknown. Reopen the properties to inspect before another edit."));
                return;
            }
            guard->combo->setEnabled(true);
            guard->combo->setToolTip(reply.error.isEmpty() ? reply.reason : reply.error);
            if (reply.state == "committed" || reply.state == "unchanged") {
                const QSignalBlocker comboBlock(guard->combo);
                const QSignalBlocker lineBlock(guard->combo->lineEdit());
                guard->combo->setCurrentIndex(-1);
                guard->combo->setEditText(reply.name);
            }
        });
}
}
