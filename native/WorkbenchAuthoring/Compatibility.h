#pragma once
#include <QJsonObject>
#include <QString>

namespace rst {
// Diagnostic admission is deliberately distinct from enabling native property hooks.
QJsonObject inspectHost();
QString incompatibility(const QJsonObject &observed);
}
