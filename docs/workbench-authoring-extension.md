# Workbench authoring integration

A separate Windows x64 installer supplies native reference pickers and Lobby2's
Git helpers. The Workshop addon contains the Enforce menus, mission templates
and gameplay; no DLL, PowerShell or Windows Script Host file belongs in it.

## Build and install

Build the native extension with the exact Qt 6.8.3 MSVC x64 development package:

```powershell
./scripts/build-workbench-authoring.ps1 -QtRoot <qt-root> -OutputDirectory <new-native-build>
./scripts/package-workbench-integration.ps1 -NativePackage <new-native-build>/package -OutputDirectory <new-installer-folder>
```

The native build runs its checks before packaging. Installer packaging verifies
the DLL checksum and all native source hashes against the supplied build's
manifest, publishes the shared C# helper, includes notices and Git helpers,
and produces a ZIP, SHA-256 sidecar and per-file manifest. It includes no game
archives, example worlds, test addons, debug symbols or local configuration.

Extract the ZIP and use `Install.bat` with a saved, stopped Workbench project
open. Choose the existing Workbench executable and restart after installation.
Git installation is optional and uses its official signed setup wizard.
See `installer/workbench/README.txt` for user-facing instructions and limits.

To ship the installer with a Lobby2 source checkout, pass
`-LobbyDirectory <absolute-Lobby2-directory>` to the packaging script. It creates
`external/workbench` with the complete compressed package, checksum and GUI
launcher. The GC **Install External Dependencies** command launches that local
installer without blocking Workbench (setup can reload scripts). Existing external
packages are refused to prevent accidental replacement. Preserve the prior package
before deliberately replacing it. Installer and native sources remain authoritative
in this repository; the Lobby2 archive is a generated distribution artifact.

The supported host is Workbench 1.9.0.37 with the checked Qt binary identities.
Other versions are refused; matching Qt alone does not establish compatibility.
End users do not need a .NET runtime, development tools or MCP client.

## Shared tool UI preview

The package includes `GCWorkbench.UI.exe` beside the helper, sharing its bundled
Windows desktop runtime. `external/workbench/Preview UI.js` opens three illustrative
forms without installing, updating or submitting anything. Source and JSON form
contract: `companion/lobby2/tools/GCWorkbench.UI/README.md`. Default examples use
labels, statuses and actions only; explanatory copy is optional. Workflow migration
is separate from this presentation kit. The existing installation wizard is still
the production workflow and has not yet been replaced by the preview.

The external launcher imports Utility and Archive from the running PowerShell's
own module directory, so a Workbench process launched from PowerShell 7 cannot
redirect its Windows PowerShell helper to incompatible modules.

## Ownership and lifecycle

The DLL and self-contained helper live in a new per-user version directory under
`%LOCALAPPDATA%/GCWorkbench`. Setup uses the shared Reforger MCP adapter and native
loader, observes the active profile and preserves its existing NET API port.
The installed profile plugin starts the helper from Workbench itself. Nothing
runs at Windows sign-in or watches for future Workbench launches.

Startup pins the launching Workbench PID and start time. It exits after verified
loading, parent exit, deactivation or its two-minute readiness deadline. It never
installs adapters, reloads scripts, changes preferences or stops Play in the
background. Reload can start a new short-lived helper; the process lease prevents
duplicate work and a persistent attempt marker prevents uncertain native replay.
An already-resident DLL counts as success only when this exact process,
installation path and DLL hash have a completed hook-verification record.
Save and restart Workbench after a failed/uncertain native load or build change.

Lobby2's update, branch and submission menus launch the installed ordinary files
under the active profile's `GCWorkbench/tools`. They require real Git source
checkouts. Sibling checkout discovery is based on the supplied Lobby2 project,
not the helper installation folder. A missing installation is reported explicitly.
No Git operation runs automatically at installation or Workbench startup.

Uninstall removes owned bootstrap and helper files, then versioned integration
files after Workbench closes. Modified bootstrap/Git files and linked paths are
preserved and reported. The shared MCP adapter, NET API setting, project files
and Git installation are retained. The DLL is never forcibly unloaded.

## Authoring behavior

Reference fields retain ordinary text entry and gain provider-backed choices.
The native owning property setter applies the selected string; there is no
second gameplay backend or runtime dependency on the DLL. Reopen a selected
component after activation to refresh its controls.

This extension does not implement custom drag/drop conversion or custom Undo.
Known limitation: native Redo for array edits can restore the temporary empty
registration value. Save/reopen and inherited-source behavior were separately
exercised; this is not a claim of complete editor compatibility.

## Verification and provenance

Before category artwork was added, native source matched all 27 build-041 snapshot hashes. Its DLL
SHA-256 is `27b3db2fd527d56c475e3add48fdfa854e0c635fdcd6542ee8093a0eec6767a5`.
That build passed eight native checks and existing live string-picker checks.
The category-icon addition requires a new native build and visual validation;
build-041 does not contain it and must not be used for the next release package.
The C# maintenance checks cover native callbacks, ownership, startup exclusion,
activation and verified resident-DLL reuse. Windows PowerShell helper checks
cover install, update, uninstall, modified-file preservation and relocated paths.

On 2026-11-11 the assembled installer payload was installed through its production
setup functions. Fresh Workbench process 43848 loaded the property hook, wrote
its verification record and exited the helper; Reload reused that verified
installation and exited again. The same Lobby2/Arland working context remained.
The interactive setup wizard, live uninstall and normal-launch authorization
prompts are not yet fully verified. No Workshop upload was performed.

Source/declaration evidence for the profile bootstrap: installed catalogue
revision `c52648a62fcb4229ad089610d060ea44`,
`game/core/data.pak/scripts/GameLib/generated/WorkbenchAPI/Workbench.c`, lines
37-56 (`RunProcess`/`FinishProcess`), and the quoted executable example in
`EnfusionBlenderTools/BlenderAPI/BlenderRestAPI.c`, lines 103-109. Native analysis
of Workbench SHA-256 `456a3e4e3b55f47d89c09e4364df78d9a0b1f7348bdc2354cce3b34e1db70b28`
found `FinishProcess` closes the process handle without waiting or terminating
its child. These are distinct from the live startup observations above.

## Plugin category artwork

The optional `menu-icons.json` beside the DLL maps a category plus its exact
command set to the native icon of one of those commands. The Lobby2 companion package
selects Spawn Mission Template's built-in compass icon for GC. No custom image is shipped. Generic installations need no icon mapping.
Names and command behavior remain unchanged. The optional `bottomCommand` moves
one existing action to the end of the matching category. Rules apply in file order;
Lobby2 moves Open Wiki, then Install External Dependencies, to form the final two
items. Other actions keep their relative order. Both presentation
changes apply when the parent menu is shown, including after script Reload creates
fresh actions. Without the extension, Workbench retains alphabetical ordering.

Workbench's category builder (`0x1402ca7d0` in the executable above) only calls
`QMenu::addMenu(title)`; the script attribute's icon belongs to individual commands
in `0x1402cb940`. There is no script category-icon or ordering parameter. This cosmetic feature
therefore uses Qt's menu Show event and inspects that menu's immediate actions,
not a binary detour or a widget-tree search. A changed command set deliberately
does not match and must be updated in the companion mapping.
The category owner is a nonvirtual internal function: unlike the existing
property-editor vtable hook, intercepting it would require a new executable-code
patch/trampoline mechanism. That additional native patching is disproportionate
for optional artwork. The bounded Show-event observer is the explicit limitation
of this feature, not a recovery route after a failed direct hook. Invalid configuration
is reported as unavailable without disabling the working property editors.
