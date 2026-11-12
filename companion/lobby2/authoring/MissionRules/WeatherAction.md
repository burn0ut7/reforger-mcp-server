# Time and Weather Action

Add **Time and Weather Action** to an event's Actions list. Use **Change Weather** and **Change Time** independently, or check both. At least one must be checked.

- **Change Weather:** checked by default, preserving existing weather actions.
- **Weather Preset:** exact preset name, default `Clear`. Choose a preset in the field using the Workbench integration, or type its exact name from the map's Time and Weather Manager.
- **Transition Seconds:** weather transition duration on the game's weather timeline, from 0 to 86400. Zero changes immediately. Time acceleration can affect real elapsed duration. This does not delay the clock change.
- **Allow Natural Changes:** unchecked keeps the requested weather until another system changes it. Checked allows normal weather progression afterward. The engine's default state duration is retained (0.001 hours); this does not guarantee a long hold at that preset.
- **Change Time:** unchecked by default. Check to set **Hours** (0–23), **Minutes** (0–59), and **Seconds** (0–59). For example, 18:30:00 means 6:30 PM. Date and normal time progression stay unchanged.

Unchecked sections are ignored, including their unused values. Invalid enabled settings reject the action. An unavailable weather preset rejects a combined action before the clock changes. The framework requests changes through the existing server-authoritative engine manager; it adds no polling, global override or custom replication. Other controllers and mission-start resets can still supersede the result.

The event's conditions and Delay decide when this action starts. A weather transition uses the engine's hours-based API after converting seconds to hours. Clock changes use SetHoursMinutesSeconds. The weather call has no success return, so accepted dispatch does not establish visual completion.

Compatibility: the serialized class remains GC_WeatherAction and existing weather fields retain their names. Existing missions change weather only unless Change Time is enabled.

Sources: installed catalogue c52648a62fcb4229ad089610d060ea44, TimeAndWeatherManagerEntity.c (ForceWeatherTo), BaseWeatherManagerEntity.c (SetHoursMinutesSeconds and GetWeatherStatesList), and SCR_AutomatedWeatherEditorAttribute.c (state enumeration and seconds-to-hours conversion).

Verification: Workbench compilation/reload and explicit editor-only production BuildAction/IsValid checks cover defaults, separate and combined changes, ignored inactive fields, invalid input and live preset enumeration. No in-game tests were run; visible transitions, clock replication and multiplayer delivery remain untested.
