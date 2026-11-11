# Collapsible Advanced options

Regular events now show **Name, Conditions, Actions, Advanced**. Random event groups show their name, conditions and event-selection fields, then Advanced. Flags show **Name, Conditions, Advanced**.

Advanced is one optional object that can be expanded or collapsed. Workbench owns its remembered expansion state; the script does not force it open or closed. Add the object only when you need nondefault settings.

| Object | Settings | When Advanced is empty |
| --- | --- | --- |
| Event / random event group | Repeatable, Delay, Random Chance, Disabled | Run once, no delay, 100% chance, enabled |
| Mission flag | Start Call Count, Repeatable | Zero calls (false), one successful call allowed |

Delay remains seconds, Random Chance remains a 0-to-1 slider, and two-state settings remain checkboxes. Flags do not gain event-only options or an Actions list. The existing runtime continues to own repeatability, delay, random chance and flag counts.

## Resource migration

The open scene was saved before migration. Local resource scanning found three containers with explicit old options: the NoFlags example group's delay, and the FlagMemoryExample flag and event's repeatability values. These values moved into their corresponding Advanced objects. Containers with no explicit options retain the defaults without requiring an object. Unrelated scene edits were preserved.

For resources outside this checkout, move the four event fields (`m_bRepeatable`, `m_fDelay`, `m_fRandomChance`, `m_bDisabled`) from the event/group into `m_Advanced GC_EventOptions { ... }`. Move flag `m_iStartCallCount` and `m_bRepeatable` into `m_Advanced GC_FlagOptions { ... }`. Preserve explicit zero/false values and migrate both inherited parents and their overrides. Do not leave old flat settings behind: the new schema no longer reads them. Earlier `GC_RuleOptions` objects must be renamed to `GC_EventOptions` as well.

Example for a repeatable delayed event:

```text
m_Advanced GC_EventOptions {
 m_bRepeatable 1
 m_fDelay 5
}
```

Workbench applies the event object's Random Chance default of 1. Scripts constructing `new GC_EventOptions()` directly must explicitly set `m_fRandomChance = 1` when full chance is intended; a null options object already uses full chance. No script initializer overrides authored values.

Verification: source/default/migration review and script compilation through Reload. Updated optional fixtures and `GC_AdvancedOptionsTests` compile but were not executed. No in-game test was run. Rendered object expansion and default collapse state have not been independently verified.
