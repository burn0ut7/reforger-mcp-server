#include "EditableChoices.h"
#include <QAbstractItemView>
#include <QEvent>
#include <QLineEdit>
#include <QSignalBlocker>
#include <QThread>

namespace rst {
bool EditableChoices::preserveLiteralFinish(QComboBox *combo)
{
    if (!combo || !combo->lineEdit() || combo->insertPolicy() != QComboBox::NoInsert) return false;
    // Qt 6.8.3 QComboBoxPrivate::editingFinished calls emitActivated for a
    // matching label before the native editor's finish handler runs. Its
    // QObjectPrivate connection uses the public combo as receiver context.
    return QObject::disconnect(combo->lineEdit(), &QLineEdit::editingFinished, combo, nullptr);
}

EditableChoices::EditableChoices(QComboBox *widget, Request request)
    : QObject(widget), combo(widget), requestChoices(std::move(request))
{
    widget->installEventFilter(this);
    widget->view()->installEventFilter(this);
    connect(widget, &QComboBox::currentIndexChanged, this, [this] { invalidate(); });
    connect(widget, &QComboBox::editTextChanged, this, [this] { invalidate(); });
}

QString EditableChoices::value() const
{
    const auto index = combo->currentIndex();
    // Qt may retain the old index while a user types new text. Do not replace
    // that draft with the old item data when a background reply arrives.
    if (index >= 0 && combo->currentText() == combo->itemText(index))
        return combo->itemData(index, Qt::UserRole).toString();
    return combo->currentText();
}

void EditableChoices::invalidate()
{
    ++generation;
    pending = 0;
}

quint64 EditableChoices::refresh()
{
    if (!combo || !combo->isEditable() || QThread::currentThread() != thread()) return 0;
    pending = ++generation;
    requestedValue = value();
    const auto id = pending;
    if (requestChoices) requestChoices(id);
    return id;
}

bool EditableChoices::apply(quint64 id, const QVector<ReferenceChoice> &choices)
{
    if (!combo || QThread::currentThread() != thread() || !combo->isEditable() ||
        id == 0 || id != pending || value() != requestedValue) return false;
    pending = 0; // A response is single-use, including unchanged lists.
    bool same = combo->count() == choices.size();
    for (qsizetype i = 0; same && i < choices.size(); ++i) {
        const auto &choice = choices[i];
        same = combo->itemText(int(i)) == choice.label &&
            combo->itemData(int(i), Qt::UserRole).toString() == choice.value &&
            combo->itemData(int(i), Qt::ToolTipRole).toString() == choice.description &&
            combo->itemData(int(i), Qt::UserRole + 1).toString() == choice.entityId;
    }
    if (same) return true;

    const auto draft = combo->currentText();
    auto *line = combo->lineEdit();
    const int cursor = line->cursorPosition();
    const int selection = line->selectionStart();
    const int selectionLength = int(line->selectedText().size());
    const bool hadSelection = combo->currentIndex() >= 0 &&
        draft == combo->itemText(combo->currentIndex());
    int match = -1;
    for (qsizetype i = 0; i < choices.size(); ++i) {
        if (choices[i].value == requestedValue) { match = int(i); break; }
    }
    const auto nextText = match >= 0 ? choices[match].label : requestedValue;
    if (line->hasFocus() && hadSelection && nextText != draft) return false;
    if (!hadSelection && match >= 0 && choices[match].label != draft) match = -1;
    // Both emit independently. Blocking only the combo could emit a line-edit
    // change into the native editor while rebuilding the presentation.
    const QSignalBlocker comboSignals(combo);
    const QSignalBlocker lineSignals(line);
    combo->clear();
    for (const auto &choice : choices) {
        combo->addItem(choice.label, choice.value);
        combo->setItemData(combo->count() - 1, choice.description, Qt::ToolTipRole);
        combo->setItemData(combo->count() - 1, choice.entityId, Qt::UserRole + 1);
    }
    combo->setCurrentIndex(match);
    if (match < 0) combo->setEditText(hadSelection ? requestedValue : draft);
    line->setCursorPosition(qMin(cursor, int(line->text().size())));
    if (selection >= 0) line->setSelection(selection, selectionLength);
    return true;
}

bool EditableChoices::eventFilter(QObject *target, QEvent *event)
{
    if (!combo) return false;
    if (target == combo->view() && event->type() == QEvent::Show)
        refresh();
    else if (target == combo && event->type() == QEvent::FocusIn)
        refresh();
    return false;
}
}
