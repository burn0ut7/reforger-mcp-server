#pragma once
#include <QComboBox>
#include <QPointer>
#include <QString>
#include <QVector>
#include <functional>

namespace rst {
struct ReferenceChoice {
    QString label;
    QString value;
    QString description;
    QString entityId;
};

// Presentation only. Workbench's existing editor owns commits and undo. Requests
// are asynchronous; replies are bound to a particular editor and edit revision.
class EditableChoices final : public QObject
{
public:
    using Request = std::function<void(quint64)>;
    EditableChoices(QComboBox *combo, Request request);
    quint64 refresh();
    bool apply(quint64 request, const QVector<ReferenceChoice> &choices);
    void invalidate();
    // Disable Qt's text-to-selection promotion. The native owner will finish
    // literal input; explicit popup/completer selections remain connected.
    static bool preserveLiteralFinish(QComboBox *combo);

protected:
    bool eventFilter(QObject *target, QEvent *event) override;

private:
    QPointer<QComboBox> combo;
    Request requestChoices;
    quint64 generation = 0;
    quint64 pending = 0;
    QString requestedValue;
    QString value() const;
};
}
