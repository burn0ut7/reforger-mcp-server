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

With the user's agreement to experiment with entity-based authoring,
`GC_MissionChoicesProbeEntity` in Lobby2's optional test addon now supplies three
native editable dropdowns through `_WB_GetUserEnums`, using the existing flag,
playable-faction, and faction-manager discovery providers. Loose-source tests
verified source renaming, temporary flag addition/removal, unique playable
faction exclusion, and selection persistence across save/reopen without the
native extension. Refresh is verified on entity reselection. The entity has no
gameplay behavior and does not replace existing nested conditions. Packaging and
Workshop delivery remain untested; see Lobby2 `tests/README.md` for rollback.

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

## TILW nested-field probe (2026-10-26)

TILW stores its events on a GenericEntity, unlike the earlier component-owned
Lobby2 configuration. An inert test fixture explicitly checked this distinction
in Workbench 1.8.0.13, with the native choices helper disabled. No TILW production
files were modified.

The same entity exposed a direct EditComboBox, a single nested BaseContainerProps
condition, and an event array containing a nested condition. The direct popup
listed the empty choice, Probe Alpha and Probe Beta. Opening each nested Flag
field produced no observable popup. Callback logging recorded the entity's own
properties, including the condition and events properties, but no nested m_sFlag
callback. This establishes failure of this tested approach, not impossibility of
every potential script approach. The test used a structural fixture in the loaded
test addon, not a live TILW project.

Evidence: .temp/local/evidence/flag-picker-20261026 contains the saved
TILW_EditorDropdownProbeEntity.c and .et fixture, helper status receipt
1793067354923824200, UI receipts, callback logs, and screenshot
1793067644182092200.png. Temporary world objects were discarded after preserving
the previously saved user world; test source/resources were removed and scripts
reloaded. Ghidra functions 141ac6810 and 141acb5a0 show inferred callback dispatch;
they do not establish a supported script API for nested property list refresh.

The requested all-fields, addon-only UI change remains unimplemented. Moving
fields to new entities would alter authoring structure; enabling the external
native helper would violate the distribution constraint. Neither is an accepted
substitute for this request.

## Addon-only feasibility follow-up (2026-10-26, Workbench 1.8.0.13)

The remaining reflection route was tested with a new inert fixture,
TILW_AttributeRefreshProbeEntity. Its nested term has an EditComboBox initialized
with `Initial option`. A checkbox handler obtains the actual Attribute through
`typename.GetVariableAttributes`, replaces `m_Enums`, and obtains it again.
Both the write and the independent reread report `Updated option` in the log.
The actual editable popup nevertheless retains `Initial option` immediately,
after selecting another entity and returning, and on a fresh prefab instance
created after the metadata mutation. This closes a different path from the
previous shared ParamEnumArray mutation test: the reflected Attribute itself was
changed, not only a previously returned array.

A SearchComboBox control was included but its open action returned
`control_unavailable` from MCP. Its popup behavior is unverified and must not be
reported as a failed engine test. It consumes the same declared Attribute enum
source; the observed EditComboBox result is the tested requirement.

Luna independently retrieved the native attribute conversion path. Ghidra's
inferred FUN_141ed7e70 reads Attribute.m_Enums at code line 208 and passes it to
FUN_141ed8ad0 at line 209. The latter creates native enf::ParmEnum entries from
the script key/description fields and stores them in the property data (string
case, lines 215-236). Native artifacts are under the configured research cache:

- queries/b894fe973ba348538fb63557816b89c3/decompilation.c
- queries/22e547fec7db4e1a9e97d1383201489a/decompilation.c
- queries/89e6f15b74ef428e87ed03f5cfb03dc2/decompilation.c

The binary SHA-256 is
7dd36ca1f662b0c13669a4f2f7f10e3c461c2906b021e742272d64b3ab7e9798.
The refreshed installed-source catalogue revision is
e135e8b45aa844efa237b165de99c1cb. Luna also verified the earlier inspected core
archive against the installed archive: SHA-256
a2f2aee2b1043ebee24995581fc53fb5f3713195fda432e6c2b4c5acdcc64ea4.
BaseContainer exposes GetEnumValues but no enum metadata setter in its generated
API. Nested custom-title attributes supply titles, not choice-list callbacks.
WorldEditorTool.UpdatePropertyPanel refreshes a tool panel; it is not an exposed
setter for a nested field's native enum metadata.

Engineering conclusion: the complete requested change cannot be delivered through
the exposed addon scripting interfaces in the inspected Workbench version while
preserving existing nested authoring and excluding native helpers. This conclusion
is bounded to those requirements and version; it is not a proof against unknown
engine defects, unsupported exploitation, or future engine APIs. A native engine
change exposing a nested enum provider or metadata refresh would address the
missing capability. The earlier Lobby2 entity-based result does not satisfy
TILW's unchanged-authoring requirement. No production TILW edits are justified by
these unsuccessful prototypes.

The reflection fixture source and prefab are archived in the same local evidence
folder. Both temporary instances were discarded, fixture files removed from the
test addon, and the original saved Arland world reopened after a successful
cleanup reload. A fresh entity query found no TILW_Attribute probe instances.
TILW git status remained clean. Independent review approved the bounded findings
and documentation; this is a completed feasibility investigation, not a shipped
UI implementation.

## Separate native plugin feasibility (2026-10-26)

A separate DLL plugin is feasible on the tested Workbench 1.8.0.13 / Qt 6.8.3,
using Qt's generic plugin mechanism. This is distinct from an addon script and
is not a documented Bohemia native Workbench extension API.

A minimal C++ QGenericPlugin was built against Qt 6.8.3 with a statically linked
C++ runtime. Its PE dependency table contains only Qt6Core.dll, Qt6Gui.dll,
Qt6Widgets.dll and KERNEL32.dll. Those Qt libraries already ship with Workbench;
end users do not need an SDK, .NET, Python, an installer or a running helper.
Build tools are development-only dependencies.

Two live observations establish the loading path:

1. Qt's QGenericPluginFactory loaded the DLL inside the existing Workbench
   PID 12464; the plugin enumerated native QComboBox controls. MCP invoked the
   factory for this first development test.
2. A separate empty test project started Workbench PID 35436 with the ordinary
   command-line arguments `-plugin tilwprobe:<report-path>`. The DLL was under
   Workbench/generic/tilwprobe.dll. Its own report confirmed that Workbench loaded
   it at startup, without MCP invoking the plugin factory or installing a bridge
   into that process. The original Workbench instances were left running.

The second launch used MCP solely for normal process startup. It required no
startup injector, helper executable or persistent external service. The launch
option must be present on every launch, normally configured once in a shortcut
or Steam launch settings. Merely copying a DLL without activation is not proven
to load it automatically.

Evidence under .temp/local/qt-plugin-probe: probe.cpp, probe.json, build.cmd,
generic/tilwprobe.dll, dependencies.txt, loaded.json and startup.json. Startup
operation receipt:
.temp/local/evidence/flag-picker-20261026/1793070133392870700-workbench_open_project.json.
DLL SHA-256: F2206AAEAC7720A94B80ED12401A93DB5C3114141BECB44734827473994B4608.

Qt documents the loader and activation mechanism:
https://doc.qt.io/qt-6.8/qguiapplication.html
https://doc.qt.io/qt-6.8/qgenericplugin.html
The matching factory source searches the generic subdirectory:
https://raw.githubusercontent.com/qt/qtbase/v6.8.3/src/gui/kernel/qgenericpluginfactory.cpp

This establishes a viable deployment architecture, not a completed dropdown
extension. The probe only loads and observes controls. Porting the provider,
nested native property updates, refresh, typed entry and undo/save verification
into the DLL remains implementation work. Existing native dropdown experiments
show that native property updates can work, but that behavior was not implemented
or tested in this DLL. A production extension needs explicit engine/Qt version
checks because the property integration uses private native interfaces.

Cleanup: the temporary startup Workbench was closed through MCP and the installed
probe DLL removed after checking its hash. Temporary MCP loader/launch changes
were archived locally and removed from production source; the original research
server configuration was restored. The first inert probe remains loaded in the
already-open original Workbench until that process exits, with no repeating work
or gameplay changes. TILW production files were not modified.
