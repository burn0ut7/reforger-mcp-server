# Dynamic Workbench choices

The experimental editor extension fills native string dropdowns from the loaded
world. Lobby2 uses it for **Flag Is Set**, required interaction flags, and scalar
mission faction keys. Flag names remain strings in saved missions. Creating a flag
at its source makes it available to conditions; no second declaration is needed.

## Shipping constraint

Lobby2 must ship without an executable helper or an external installation.
The addon-owned executable packaging prototype has therefore been removed from
production and archived locally. No encoded executable payload is being shipped.
The MCP extension below remains a development experiment; it does not satisfy
that shipping requirement. Editable text entry works without this extension,
but the current nested fields do not populate live choices by themselves.

Workbench 1.8.0.13 was tested without the helper using an optional script probe.
An entity-level `_WB_GetUserEnums` returned two real native popup choices. The
component-instance callback produced no observed calls; its class callback ran
with entity-style field names rather than the tested direct/nested property
names. Neither component probe opened a populated popup, including a repeat with
`EditComboBox` on all three probes to match the successful entity control. This establishes the
observed boundary for this setup, not universal absence of every script-only
implementation. No supported hook for the current nested conditions has been
verified. The fixture is gated by `GC_EDITOR_ENUM_PROBE` in Lobby2's optional
test addon so its temporary fields stay out of the normal editor.

A separate `GC_ENUM_FUNCTION_PROBE` tested `enums: GetChoices()` with a shared
`ParamEnumArray`, following the installed ValidateFBXPlugin pattern. The nested
control showed `Function initial` even after an entity callback mutated that
provider's runtime array to `Function updated` before opening the popup. Logs
confirmed the mutation. The popup did not reflect that mutation in this test;
where choices are copied or retained was not established. Initial choices reach
the field, but live discovery through this mechanism remains unverified.

The rejected executable prototype did work from a local addon folder. Native
inspection showed the ordinary Pack Project list excludes `.exe`, and the
publisher's PAK-building branch calls that packer with no additional types.
The separate publisher extension callback filters the RDB cache, not the archive.
No packed-archive or Workshop download test was completed. Native analysis is
implementation evidence, not a supported publishing contract.

## Run the development extension

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
    enums: {ParamEnum("", "", "rst-choices:1:My_ChoicesProvider")})]
string m_sValue;
```

The first empty entry's description is routing metadata. The extension keeps it
in the native property and hides it from the combo's tooltip. Provider identifiers
are class names; labels and saved values are identical in this first slice.
Keep the script entry's label empty: Workbench's string-enum conversion uses that
label as its initial value. The extension supplies the visible `(None)` label with
an empty saved value. This prevents selecting a literal `(None)` before refresh.
JsonApiStruct omits empty arrays; a ready provider response without `values` means
an empty list, including after deleting its last source.

Every second, the extension queries providers for visible marked controls,
deduplicates and sorts values, and updates the owning native StringProperty list
only when it differs. An existing value absent from the provider remains selected
as `value (not found)`. Refresh does not author a replacement or create undo steps.
Selecting an option follows the editor's normal activation and undo path.

Use `UIWidgets.EditComboBox` with the same provider marker to also permit typed
values before their sources exist. Workbench owns text commit and undo; native
refresh skips a focused text field. Lobby2 uses this for all seven flag/faction
references. Fixed enums remain selection-only. The user confirmed custom flag
entry; exact value `sdads` survived saving and reopening, without the display-only
`(not found)` suffix. Undo for newly typed text was not retested.

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

Follow-up checks removed all three playable USSR groups from player availability:
the player-casualty list dropped USSR and restored it after undo. Removing the last
flag source retained its selected reference as `Flag86 (not found)` and kept
refresh active with no available flags. Choosing `(None)` stored an empty string;
after saving and reloading, the faction popup showed exactly `(None)`, `US`, and
`USSR`, with no undo steps from background refresh.
