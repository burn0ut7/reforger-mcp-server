#include "Compatibility.h"
#include <QCoreApplication>
#include <QTextStream>

int main(int argc, char **argv)
{
    QCoreApplication app(argc, argv);
    // These tests guard refusal paths. Real Workbench admission requires live evidence.
    QJsonObject missing{{"hostName", "ArmaReforgerWorkbenchSteamDiag.exe"}, {"qtVersion", "6.8.3"}};
    if (rst::incompatibility(missing).isEmpty()) return 1;
    auto wrongQt = missing;
    wrongQt.insert("qtVersion", "6.9.0");
    if (rst::incompatibility(wrongQt) != "Unsupported Qt version") return 2;
    auto wrongHost = missing;
    wrongHost.insert("hostName", "ArmaReforgerSteam.exe");
    if (rst::incompatibility(wrongHost) != "Not the supported Workbench executable") return 3;
    QJsonObject fixtureModules;
    const char *names[] = {"ArmaReforgerWorkbenchSteamDiag.exe", "Qt6Core.dll", "Qt6Gui.dll", "Qt6Widgets.dll", "Qt6Network.dll"};
    const char *hashes[] = {
        "456a3e4e3b55f47d89c09e4364df78d9a0b1f7348bdc2354cce3b34e1db70b28",
        "0cc62e841959fb8f6d366ea116c6f6bbc273e30f0ef90d8aa2e4adeb81fd63f8",
        "ca9f20d9b59d5bc321a87005b926b853ebcd6a2394823e14d3c8b5fc5702fa8c",
        "d34ef78b3d3b5506ee8279ce6efbfc263d773b74860af76e66b0715346897a3a",
        "2c2f65c70989646f06d668883c031f06fac3f8de1f459278c94cc50dfe89988d"};
    for (int i = 0; i < 5; ++i) fixtureModules.insert(names[i], QJsonObject{{"sha256", hashes[i]}});
    auto complete = missing;
    complete.insert("modules", fixtureModules);
    if (!rst::incompatibility(complete).isEmpty()) return 6;
    for (const auto *name : names) {
        auto changed = fixtureModules;
        changed.insert(name, QJsonObject{{"sha256", "changed"}});
        auto candidate = complete;
        candidate.insert("modules", changed);
        if (rst::incompatibility(candidate).isEmpty()) return 7;
    }
    const auto actual = rst::inspectHost();
    if (rst::incompatibility(actual).isEmpty()) return 4;
    if (!actual.contains("hostPath") || !actual.contains("pid")) return 5;
    QTextStream(stdout) << "PASS: exact identity fixture, each changed module, missing modules, wrong Qt, game host, real test-host refusal\n";
    return 0;
}
