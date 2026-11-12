#include "SourceText.h"
#include <QByteArray>
#include <cstdio>
#include <cstdlib>

static void require(bool condition, const char *name)
{
    if (!condition) { std::fprintf(stderr, "FAIL %s\n", name); std::exit(1); }
    std::printf("PASS %s\n", name);
}

int main()
{
    QString value;
    require(rst::decodeSourceText({}, value) && value.isEmpty(), "Empty value");
    const QByteArray literal(" leading,\"quoted\" trailing ");
    require(rst::decodeSourceText(literal, value) && value.toUtf8() == literal, "Literal punctuation and whitespace");
    const auto unicode = QByteArray::fromHex("e697a5f09f9880");
    require(rst::decodeSourceText(unicode, value) && value.toUtf8() == unicode, "Complete Unicode");
    const auto bom = QByteArray::fromHex("efbbbf616263");
    require(rst::decodeSourceText(bom, value) && value.toUtf8() == bom, "Leading U+FEFF retained");
    for (const auto &hex : {"c2", "e282", "f09f98", "80", "c080", "eda080"}) {
        value = "stale";
        require(!rst::decodeSourceText(QByteArray::fromHex(hex), value) && value.isEmpty(), hex);
    }
    return 0;
}
