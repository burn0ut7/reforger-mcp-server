#pragma once
#include <QByteArrayView>
#include <QStringConverter>

namespace rst {
// Container strings are complete literal values, not chunks of a text file.
inline bool decodeSourceText(QByteArrayView bytes, QString &text)
{
    QStringDecoder utf8(QStringDecoder::Utf8,
        QStringConverter::Flag::Stateless | QStringConverter::Flag::ConvertInitialBom);
    text = utf8(bytes);
    if (!utf8.hasError()) return true;
    text.clear();
    return false;
}
}
