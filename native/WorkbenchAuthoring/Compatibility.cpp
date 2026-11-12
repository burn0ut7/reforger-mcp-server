#include "Compatibility.h"
#include <QCryptographicHash>
#include <QFile>
#include <QFileInfo>
#include <QJsonArray>
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>

namespace {
struct Module {
    const wchar_t *name;
    const char *hash;
};

// Identity evidence: NativeWorkbench.Profile.cs and NativeWorkbench.cs.
// PropertyEditors additionally checks its owning slot before opt-in installation.
const Module modules[] = {
    {L"ArmaReforgerWorkbenchSteamDiag.exe", "456a3e4e3b55f47d89c09e4364df78d9a0b1f7348bdc2354cce3b34e1db70b28"},
    {L"Qt6Core.dll", "0cc62e841959fb8f6d366ea116c6f6bbc273e30f0ef90d8aa2e4adeb81fd63f8"},
    {L"Qt6Gui.dll", "ca9f20d9b59d5bc321a87005b926b853ebcd6a2394823e14d3c8b5fc5702fa8c"},
    {L"Qt6Widgets.dll", "d34ef78b3d3b5506ee8279ce6efbfc263d773b74860af76e66b0715346897a3a"},
    {L"Qt6Network.dll", "2c2f65c70989646f06d668883c031f06fac3f8de1f459278c94cc50dfe89988d"}
};
}

namespace rst {
QString incompatibility(const QJsonObject &observed)
{
    if (observed.value("qtVersion").toString() != QStringLiteral("6.8.3"))
        return QStringLiteral("Unsupported Qt version");
    if (observed.value("hostName").toString().compare(
            QStringLiteral("ArmaReforgerWorkbenchSteamDiag.exe"), Qt::CaseInsensitive) != 0)
        return QStringLiteral("Not the supported Workbench executable");
    const auto identities = observed.value("modules").toObject();
    for (const auto &module : modules) {
        const auto name = QString::fromWCharArray(module.name);
        if (identities.value(name).toObject().value("sha256").toString() != QLatin1String(module.hash))
            return QStringLiteral("Missing or incompatible module: ") + name;
    }
    return {};
}

QJsonObject inspectHost()
{
    wchar_t path[32768]{};
    auto length = GetModuleFileNameW(nullptr, path, DWORD(std::size(path)));
    const auto hostPath = length > 0 && length < std::size(path)
        ? QString::fromWCharArray(path, int(length)) : QString();
    QJsonObject identities;
    for (const auto &module : modules) {
        const auto handle = GetModuleHandleW(module.name);
        if (!handle) continue;
        length = GetModuleFileNameW(handle, path, DWORD(std::size(path)));
        if (!length || length >= std::size(path)) continue;
        const auto filePath = QString::fromWCharArray(path, int(length));
        QFile file(filePath);
        QCryptographicHash hash(QCryptographicHash::Sha256);
        if (!file.open(QIODevice::ReadOnly) || !hash.addData(&file)) continue;
        identities.insert(QString::fromWCharArray(module.name), QJsonObject{
            {"path", filePath}, {"sha256", QString::fromLatin1(hash.result().toHex())}});
    }
    QJsonObject extension;
    HMODULE ownModule = nullptr;
    if (GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCWSTR>(&inspectHost), &ownModule)) {
        length = GetModuleFileNameW(ownModule, path, DWORD(std::size(path)));
        if (length > 0 && length < std::size(path)) {
            const auto filePath = QString::fromWCharArray(path, int(length));
            extension.insert("path", filePath);
            QFile file(filePath);
            QCryptographicHash hash(QCryptographicHash::Sha256);
            if (file.open(QIODevice::ReadOnly) && hash.addData(&file))
                extension.insert("sha256", QString::fromLatin1(hash.result().toHex()));
        }
    }
    return {{"hostName", QFileInfo(hostPath).fileName()}, {"hostPath", hostPath},
        {"pid", qint64(GetCurrentProcessId())}, {"qtVersion", qVersion()}, {"modules", identities}, {"extension", extension}};
}
}
