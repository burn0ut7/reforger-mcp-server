#include "EditableChoices.h"
#include <QCompleter>
#include <QApplication>
#include <QLineEdit>
#include <cstdio>

static void require(bool condition, const char *name)
{
    if (!condition) { std::fprintf(stderr, "FAIL %s\n", name); std::exit(1); }
    std::printf("PASS %s\n", name);
}

int main(int argc, char **argv)
{
    QApplication app(argc, argv);
    QComboBox combo;
    combo.setEditable(true);
    combo.addItem("United States", "US");
    combo.setCurrentIndex(0);
    quint64 requested = 0;
    rst::EditableChoices binding(&combo, [&](quint64 id) { requested = id; });
    int changes = 0;
    QObject::connect(&combo, &QComboBox::currentIndexChanged, [&] { ++changes; });
    QObject::connect(&combo, &QComboBox::editTextChanged, [&] { ++changes; });
    QObject::connect(combo.lineEdit(), &QLineEdit::textChanged, [&] { ++changes; });
    QObject::connect(combo.lineEdit(), &QLineEdit::editingFinished, [&] { ++changes; });
    const QVector<rst::ReferenceChoice> choices = {{"Russia", "USSR", ""}, {"United States", "US", ""}};
    auto id = binding.refresh();
    require(id != 0 && requested == id, "Refresh requests choices for this editor");
    require(binding.apply(id, choices), "Choice reply applied");
    require(combo.currentData().toString() == "US" && combo.currentText() == "United States",
        "Reordered choices retain the selected value");
    require(changes == 0, "Refreshing emits no editing or commit signals");
    require(!binding.apply(id, {}), "Duplicate response cannot replace the list");

    auto described = choices;
    described[1].description = "Multiple sources define this value.";
    id = binding.refresh();
    require(binding.apply(id, described), "Source warning updates presentation");
    require(combo.itemData(1, Qt::ToolTipRole).toString() == described[1].description &&
        combo.currentData().toString() == "US" && combo.currentText() == "United States" && changes == 0,
        "Source warning preserves selected value and emits no commit");

    id = binding.refresh();
    combo.setEditText("MY_CUSTOM_FACTION");
    require(!binding.apply(id, {}), "Typing invalidates an in-flight response");
    require(combo.currentText() == "MY_CUSTOM_FACTION", "Pending reply preserves manual text");
    id = binding.refresh();
    changes = 0;
    require(binding.apply(id, {{"Custom label", "MY_CUSTOM_FACTION", ""}}), "New choices retain a typed value");
    require(combo.currentIndex() == -1 && combo.currentText() == "MY_CUSTOM_FACTION" && changes == 0,
        "A label cannot replace manual text or implicitly commit it");

    combo.clear();
    combo.addItem("United States", "US");
    combo.setCurrentIndex(0);
    id = binding.refresh();
    changes = 0;
    require(binding.apply(id, {}), "Missing selection remains manually editable");
    require(combo.currentIndex() == -1 && combo.currentText() == "US" && changes == 0,
        "Removed choice retains its value rather than its display label");

    const auto old = binding.refresh();
    const auto latest = binding.refresh();
    require(!binding.apply(old, choices) && binding.apply(latest, choices), "Only latest request can update the editor");
    id = binding.refresh();
    binding.invalidate();
    require(!binding.apply(id, choices), "Explicit world/lifecycle invalidation rejects pending replies");
    combo.setEditable(false);
    require(binding.refresh() == 0, "Noneditable controls are not adopted");
    QComboBox literal;
    literal.setEditable(true);
    literal.setInsertPolicy(QComboBox::NoInsert);
    literal.completer()->setCaseSensitivity(Qt::CaseInsensitive);
    literal.addItem("Old", "old-name");
    literal.addItem("Group Example (unnamed)", "");
    int activations = 0;
    QObject::connect(&literal, &QComboBox::activated, [&] { ++activations; });
    literal.lineEdit()->setText("group example (unnamed)");
    QMetaObject::invokeMethod(literal.lineEdit(), "editingFinished", Qt::DirectConnection);
    require(activations == 1 && literal.currentIndex() == 1, "Baseline Qt promotes typed matching label to selection");
    literal.setCurrentIndex(0);
    literal.lineEdit()->setText("group example (unnamed)");
    activations = 0;
    require(rst::EditableChoices::preserveLiteralFinish(&literal), "Disconnect only Qt implicit finish selection");
    QString finishedText;
    QObject::connect(literal.lineEdit(), &QLineEdit::editingFinished, [&] { finishedText = literal.currentText(); });
    QMetaObject::invokeMethod(literal.lineEdit(), "editingFinished", Qt::DirectConnection);
    require(activations == 0 && finishedText == "group example (unnamed)", "Typed label remains literal and reaches finish owner without activation");
    QMetaObject::invokeMethod(&literal, "activated", Qt::DirectConnection, Q_ARG(int, 1));
    require(activations == 1, "Explicit selection activation is retained");
    return 0;
}
