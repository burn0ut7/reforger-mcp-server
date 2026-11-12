#include "EntitySelection.h"
#include "ChoicesClient.h"
#include "PropertyEditors.h"
#include <QCoreApplication>
#include <QAbstractItemView>
#include <QCompleter>
#include <QItemSelectionModel>
#include <QJsonArray>
#include <QLineEdit>
#include <QRegularExpression>
#include <QSignalBlocker>
#include <QUuid>

namespace rst {
namespace {
bool sameBinding(const PropertyDestination &a, const PropertyDestination &b)
{
    if (a.state != "captured" || b.state != "captured" || a.bindingFingerprint.isEmpty() ||
        a.bindingFingerprint != b.bindingFingerprint || a.entityIdHex != b.entityIdHex ||
        a.key != b.key || a.className != b.className || a.arrayIndex != b.arrayIndex ||
        a.arrayValues != b.arrayValues || a.path.size() != b.path.size()) return false;
    for (qsizetype i = 0; i < a.path.size(); ++i)
        if (a.path[i].key != b.path[i].key || a.path[i].index != b.path[i].index || a.path[i].className != b.path[i].className) return false;
    return true;
}
}

EntitySelection::EntitySelection(QObject *source, QObject *owner, QComboBox *widget, QString selectedProvider,
    quint16 selectedPort, QString expectedBuild, std::function<void(QJsonObject)> logger)
    : QObject(owner), property(source), editor(owner), combo(widget), provider(std::move(selectedProvider)),
      build(std::move(expectedBuild)), initialValue(widget->currentText()), port(selectedPort), log(std::move(logger))
{
    const auto destination = PropertyEditors::destination(source);
    if (destination.state != "captured" || destination.arrayIndex >= 0) {
        log({{"event", "entitySelection"}, {"state", "unavailable"}, {"reason", "Scalar destination required"}});
        return;
    }
    const QPointer<EntitySelection> guard(this);
    const auto error = PropertyEditors::routeSelection(owner, widget,
        [guard](int index) { if (guard) guard->activate(index); },
        [guard] { if (guard) guard->finishText(); });
    enabled = error.isEmpty();
    if (!enabled) log({{"event", "entitySelection"}, {"state", "unavailable"}, {"reason", error}});
}

void EntitySelection::activate(int index)
{
    if (busy || !combo || !editor) return;
    if (index >= 0 && !combo->itemData(index, Qt::UserRole + 1).toString().isEmpty()) {
        pick(index);
        return;
    }
    PropertyEditors::nativeActivate(editor);
}

void EntitySelection::finishText()
{
    if (busy || !combo || !editor) return;
    // Qt emits editingFinished before accepting an active completion. Leave
    // that explicit choice to activated; do not create a preceding text edit.
    const auto *completer = combo->completer();
    const auto *popup = completer ? completer->popup() : nullptr;
    if (popup && popup->isVisible() && popup->currentIndex().isValid() &&
        popup->selectionModel() && popup->selectionModel()->isSelected(popup->currentIndex())) return;
    const auto index = combo->currentIndex();
    if (index >= 0 && combo->currentText() == combo->itemText(index) &&
        !combo->itemData(index, Qt::UserRole + 1).toString().isEmpty()) return;
    // A manually typed display label is still literal input. Native finish
    // searches labels; temporarily give matching rows that literal value.
    const auto text = combo->currentText();
    QVector<QPair<int, QVariant>> restored;
    for (int i = 0; i < combo->count(); ++i) {
        if (combo->itemText(i).compare(text, Qt::CaseInsensitive) != 0 || combo->itemData(i, Qt::UserRole + 1).toString().isEmpty()) continue;
        restored.append({i, combo->itemData(i)});
        combo->setItemData(i, text);
    }
    const QPointer<QComboBox> widget = combo;
    PropertyEditors::nativeFinish(editor);
    // Native commit can destroy this controller and its editor.
    if (widget) for (const auto &entry : restored) widget->setItemData(entry.first, entry.second);
}

void EntitySelection::restoreText(const QString &value)
{
    if (!combo || !combo->lineEdit()) return;
    const QSignalBlocker blockCombo(combo);
    const QSignalBlocker blockLine(combo->lineEdit());
    combo->setCurrentIndex(-1);
    combo->setEditText(value);
}

void EntitySelection::finish(const QString &state, const QString &name, const QString &reason)
{
    if (state == "committed" || state == "unchanged") restoreText(name);
    busy = false;
    if (combo) {
        combo->setToolTip(reason);
        combo->setEnabled(true);
    }
}

void EntitySelection::pick(int index)
{
    if (!property || !combo || world.isEmpty()) return;
    const auto target = combo->itemData(index, Qt::UserRole + 1).toString();
    const auto targetName = combo->itemData(index).toString();
    const auto destination = PropertyEditors::destination(property);
    const auto source = PropertyEditors::sourceValue(property);
    const QString oldValue = source.state == "observed" ? source.serializedValue : initialValue;
    busy = true;
    restoreText(oldValue);
    if (destination.state != "captured" || destination.arrayIndex != -1 ||
        (source.state != "observed" && source.state != "no_local_value")) {
        finish("refused", {}, "This field is not available for entity selection"); return;
    }
    static const QRegularExpression idPattern(QStringLiteral("^0x([0-9A-Fa-f]{16}) \\{\\}$"));
    const auto match = idPattern.match(target);
    bool targetOk = false, destinationOk = false;
    const auto targetId = match.captured(1).toULongLong(&targetOk, 16);
    const auto destinationId = destination.entityIdHex.toULongLong(&destinationOk, 16);
    if (!match.hasMatch() || !targetOk || !destinationOk) {
        finish("refused", {}, "Entity identity is unavailable"); return;
    }
    QJsonArray keys, indices, classes;
    for (const auto &entry : destination.path) {
        keys.append(entry.key); indices.append(entry.index); classes.append(entry.className);
    }
    const auto token = QUuid::createUuid().toString(QUuid::Id128);
    QJsonObject request{{"operation", "prepare_scalar"}, {"token", token}, {"world", world}, {"provider", provider},
        {"destinationHi", qint32(destinationId >> 32)}, {"destinationLo", qint32(destinationId)},
        {"entityHi", qint32(targetId >> 32)}, {"entityLo", qint32(targetId)}, {"entityName", targetName},
        {"expectedClass", destination.className}, {"key", destination.key}, {"expectedValue", oldValue},
        {"arrayIndex", -1}, {"pathKeys", keys}, {"pathIndices", indices}, {"pathClasses", classes}};
    combo->setEnabled(false);
    const QPointer<EntitySelection> guard(this);
    ChoicesClient::edit(this, port, build, request, [guard, destination, oldValue, token](EntityEditReply reply) {
        if (!guard) return;
        guard->log({{"event", "entityPrepare"}, {"state", reply.state}, {"reason", reply.reason}, {"error", reply.error}});
        if (reply.state != "prepared") {
            guard->finish(reply.state, {}, reply.error.isEmpty() ? reply.reason : reply.error); return;
        }
        if (!guard->property || !sameBinding(destination, PropertyEditors::destination(guard->property))) {
            guard->finish("refused", {}, "Destination changed while preparing the edit"); return;
        }
        const auto current = PropertyEditors::sourceValue(guard->property);
        if ((current.state == "observed" && current.serializedValue != oldValue) ||
            (current.state != "observed" && current.state != "no_local_value")) {
            guard->finish("refused", {}, "Field changed while preparing the edit"); return;
        }
        const auto logger = guard->log;
        // Observe a dispatched write even if native reconstruction destroys
        // this property editor. Never retry or cancel it as though read-only.
        ChoicesClient::edit(QCoreApplication::instance(), guard->port, guard->build,
            {{"operation", "commit_scalar"}, {"token", token}}, [guard, logger](EntityEditReply committed) {
                logger({{"event", "entityCommit"}, {"state", committed.state}, {"name", committed.name},
                    {"reason", committed.reason}, {"error", committed.error}});
                if (guard) guard->finish(committed.state, committed.name,
                    committed.error.isEmpty() ? committed.reason : committed.error + "; inspect the field before retrying");
            });
    });
}
}
