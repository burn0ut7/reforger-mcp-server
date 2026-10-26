# Dynamic Workbench choices

The experimental editor extension fills native string dropdowns from the loaded
world. Lobby2 uses it for **Flag Is Set**, required interaction flags, and scalar
mission faction keys. Flag names remain strings in saved missions. Creating a flag
at its source makes it available to conditions; no second declaration is needed.

## Run the extension

1. Run the server built from this revision. Configure `workbench.runtimePort` to
   match Workbench's NET API port.
2. Use `workbench_runtime_setup(action="install", processId=...)` to install its
   editor adapter. Save the world and close World Editor for this tool's Reload
   step, then reopen the same world. Keep the Workbench process and project.
3. Call `workbench_dynamic_choices(action="enable", processId=...)` for the current
   Workbench process. `action="status"` reports active fields and refresh duration;
   `action="disable"` stops polling.

Keep that server running. Enabling is process-local and is required again after
server or Workbench restart. Without it, the field metadata only supplies the
empty choice, or an existing control retains its last list. Do not treat those
lists as live. An `unavailable` status stops refresh: resolve its reported cause
before enabling again. Reload, closed editors and Play suspend refresh.

The native integration supports the exact checked **Workbench 1.8.0.13 / Qt 6.8.3**
binaries. A different binary is refused; no installed executable is patched.
This is an editor extension dependency, not a script-only dropdown feature.

## Author a provider

Put a provider in the addon's Game scripts under `#ifdef WORKBENCH`. Its instance
method `array<string> GetChoices(WorldEditorAPI api)` reads current authoring data
and returns values. It must not modify the world or retain world references.
The generic server has no Lobby2 field names or discovery rules.

Opt in a scalar string field explicitly:

```c
[Attribute("", UIWidgets.ComboBox, "Choose an existing value.",
    enums: {ParamEnum("(None)", "", "rst-choices:1:My_ChoicesProvider")})]
string m_sValue;
```

The first empty entry's description is routing metadata. The extension keeps it
in the native property and hides it from the combo's tooltip. Provider identifiers
are class names; labels and saved values are identical in this first slice.

Every second, the extension queries providers for visible marked controls,
deduplicates and sorts values, and updates the owning native StringProperty list
only when it differs. An existing value absent from the provider remains selected
as `value (not found)`. Refresh does not author a replacement or create undo steps.
Selecting an option follows the editor's normal activation and undo path.

Limits are explicit: 64 marked controls per observation, 256 entries per field
including the empty and retained-missing entries, 255 UTF-8 bytes per label and
1023 per value. Exceeding a limit stops refresh with a diagnostic. String arrays
and multi-select fields are outside this slice.

## Validation

On Arland with Lobby2's test addon loaded, actual native popup captures and
authoring readbacks verified source rename `Flag86` to `Flag87`, decentralized
Set Flag action discovery, missing-value retention, and faction keys `US`/`USSR`.
Choosing values persisted through the normal property editor; undo restored the
previous faction value. Evidence is retained locally under
`.temp/local/evidence/flag-picker-20261026`.
