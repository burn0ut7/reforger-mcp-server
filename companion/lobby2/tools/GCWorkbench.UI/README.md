# GC tool windows

A small reusable Windows presentation layer for the external GC tools. It uses
WPF, whose runtime is already shipped in the Workbench integration package. No
browser engine, UI framework installer, network connection or game addon is needed.

This first version is a working **preview and form renderer**, not a replacement
for the existing installation, update or submission handlers. Those workflows
remain responsible for permissions, validation, file changes and verified outcomes.
The preview's statuses and mission names are illustrative, not machine observations.

`--tools` opens the GC Tools sidebar and action panel and returns a selection:
10 = submit mission, 12 = install dependencies, 2 = cancel.
Mod updates return 100 + a nonzero selection mask: Lobby2=1, Core=2, Units=4, Missions=8.
Workbench owns dispatch after this window closes; choosing an action here alone
does not perform it. Generic requests can also supply `actions` (`id`, `label`)
to return a named action with the form values instead of the default confirmation.

## Try the design

Build this project, then run `GCWorkbench.UI.exe --preview`. The sidebar switches
between dependency setup, mod updates and mission submission. Fields and selection
cards work; required-field errors appear beside their inputs. Preview actions never
install, update or submit anything. Switching pages retains the current form values.

## Reuse a window

Pass a UTF-8 JSON request and a new result path:

```powershell
& $uiExecutable --request $requestPath --result $resultPath
```

Both paths must be absolute. Exit codes: `0` confirmed, `2` cancelled (including
Escape or closing the window), `1` failed. The result contains `Action` and `Values`,
with text values as strings and checkbox values as booleans. An existing result
file is never overwritten. Do not treat confirmation as completion of an operation.

```json
{
  "title": "Name your mission",
  "section": "Mission details",
  "primaryAction": "Continue",
  "secondaryAction": "Cancel",
  "accent": "#d4b874",
  "fields": [
    { "id": "name", "label": "Mission name", "kind": "text", "required": true },
    { "id": "notes", "label": "Notes", "kind": "multiline" },
    { "id": "tested", "label": "Tested in Workbench", "kind": "check", "checked": false }
  ]
}
```

`items` adds read-only status cards with `title`, `description`, `status` and
`state` (`ready`, `pending`, `warning`, `error`). Text is plain text; requests cannot
run commands or embed markup. Labels, helper text, sections, action text and accent
are configurable. `ToolWindow` and `DialogSpec` can also be used directly from C#.
Layout uses scalable Windows units, wraps descriptions, scrolls the form body and
keeps actions visible. Field validation checks required values only; workflows
must validate their own domain rules.

## Packaging

Publish alongside the existing helper into its payload directory using the same
.NET target/runtime version. Both executables share the bundled runtime files;
there is no second runtime installation. Do not add this preview to game scripts
or make gameplay depend on it. Workflow integration and progress reporting are the
next slice; this kit currently collects choices and presents supplied statuses.

The theme uses the GC website field palette: background #191e18, text #f0e9d5,
muted #a9b09b, border #404735, accent #d4b874, panel #242b20, button ink #1b2119.
The integrated caption retains dragging, resizing, minimize, maximize and close.
GC Tools launches its registered executable directly through WScript, without
PowerShell, package hashing or extraction on the normal opening path. Installation
registers the app in %LOCALAPPDATA%/GCWorkbench/ui-path.txt (UTF-16). An uninstalled
source checkout prepares and registers a package-hash cache once. Preview retains
the cache bootstrap path. Cache versions live under %LOCALAPPDATA%/GCWorkbench/ui.
