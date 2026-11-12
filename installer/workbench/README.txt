GC WORKBENCH INTEGRATION - WINDOWS X64

VALIDATION STATUS
Build and isolated installer/lifecycle checks passed. Production setup functions,
fresh Workbench startup, verified native-hook loading, helper exit and Reload
reuse were observed on Workbench 1.9.0.37. The interactive wizard, live removal
and normal-launch authorization prompts still need complete verification.
This package does not claim dialog-free startup or Workshop release certification.

INSTALL
1. Extract the complete ZIP.
2. Open your project in Workbench, stop Play and save your work.
3. Double-click Install.bat, choose integration and select your Workbench EXE.
4. Optionally install/update Git using its official setup wizard.
5. Save and restart Workbench, then open it normally from now on.

Workbench's own profile plugin starts a hidden helper when its editor scripts
load. Steam, direct EXE, shortcuts and project-file launches use that same
profile entry point; no special launch arguments are required. Nothing starts
at Windows sign-in and no background process watches for future launches.
The helper exits after loading, if Workbench closes, or after a two-minute
readiness deadline. Reloading scripts may briefly start it again; a process
identity gate prevents duplicate or uncertain native loads.
An already-resident DLL must match a completed verification for the same
Workbench process, installed path and file checksum; presence alone is insufficient.

The profile you install into must be the profile used when launching Workbench.
A custom -profile directory needs its own installation; this installer manages
one profile at a time. Workbench must successfully load its editor scripts.
Compilation errors, disabled NET API, Play or readiness problems can prevent
activation. Correct the issue and Reload scripts to retry readiness; after an
uncertain DLL load, save and restart Workbench instead.

The package contains a self-contained helper and DLL: no .NET installation,
Visual Studio, Qt tools or MCP client is needed. The helper uses Reforger MCP's
existing setup and native loader. It never changes settings or reloads scripts
in the background. Reselect an already-open component to refresh its pickers.

Requires Windows x64 Workbench 1.9.0.37 with the checked Qt binary identities.
Other engine builds are refused. GC/Lobby2 supplies field markers and providers
and is installed separately. No game binaries or test addons are bundled.
Steam's installed files, launch arguments and project associations are unchanged.

Setup installs/checks the shared Reforger MCP adapter in the active profile,
enables its existing NET API port, and reloads scripts when required. NET API
may listen on network interfaces; helper traffic uses loopback. Locally edited
adapter or bootstrap files are preserved and reported as conflicts. Versions
live under %LOCALAPPDATA%\GCWorkbench so a loaded DLL is never overwritten.

UNINSTALL
Save and close Workbench, then double-click Uninstall.bat from this extracted
package. It removes the managed startup plugin, all installed GCWorkbench
versions, logs and helper state, the old desktop shortcut and any legacy
sign-in startup entry. If Workbench is still open, it disables future loading
and asks you to close Workbench and run Uninstall.bat again to finish removal.
The DLL leaves memory when Workbench closes; it is never forcibly unloaded.
A locally modified startup script or linked installation path is preserved
with a concrete message rather than recursively deleting unrelated files.

The shared Reforger MCP adapter and NET API preference are retained because
other MCP tools may use them. Projects, addons, Steam files and Git are retained.
Disable NET API in Workbench preferences if no longer needed. Remove Git using
Windows Settings if desired. The extracted ZIP folder may then be deleted.

GIT
The optional latest stable x64 release is resolved from the official GitHub API:
https://api.github.com/repos/git-for-windows/git/releases/latest
The release checksum and Windows signature are verified before its wizard runs.
Official download page: https://git-scm.com/install/windows
Git is optional; a network connection is required only for this download.

TROUBLESHOOTING
The installed version folder contains setup.log and setup-error.log.
state\startup\<process identity>\startup.log records helper outcomes;
waiting.txt and authoring-sessions record readiness and native load details.
Unsupported engine builds require an updated package. Uninstall.bat is safe to
run again if removal was interrupted. Keep the complete support folder beside
the two batch files.

LOBBY2 GIT TOOLS
The installer reuses Workbench's built-in compass icon for the GC plugin category.
The category text remains GC. It applies only when the four GC commands match;
unrelated plugin menus retain their icons. The category icon requires the extension.
The package also installs the Git update, branch and mission-submission helpers
under GCWorkbench/tools in the active Workbench profile. Lobby2 supplies the
native editor menus; these Windows files are not shipped inside the Workshop
addon. Modified helpers are preserved and reported during update or uninstall.
Statistics ingestion is a separate server deployment; this desktop installer
does not configure a database or start a server service.
