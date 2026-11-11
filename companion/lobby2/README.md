# Lobby2 companion components

These sources live beside the native Workbench integration, outside the Workshop
addon. They are not part of Reforger MCP's provider-independent server logic.

- `tools`: Windows Git helpers. `scripts/package-workbench-integration.ps1`
  includes these in the DLL installer. Installation copies them into the active
  Workbench profile's `GCWorkbench/tools` directory; modified files are preserved.
  The Lobby2 Workbench menu reports a missing installation explicitly.
- `integrations/StatsIngest` and `integrations/Contracts`: external statistics
  ingestion, player history and shared data formats. Build/deploy independently
  following `integrations/StatsIngest/README.md`. Database setup and service
  activation are explicit server operations, not desktop installer side effects.
- `integrations/MapDrawing`: optional separately published Reforger addon.
  Copy its source directory into an addon workspace before opening `addon.gproj`.
- `authoring`: mission-author documentation retained from the addon source.

The Windows helper chain is `GC-ModLauncher.js` (Windows Script Host) →
`GC-ModWorkflow.ps1` → `GC-SubmitMission.ps1` when submitting a mission.
It uses Git source checkouts and opens submission/review workflows. It is not
Enforce gameplay code and cannot operate directly on files inside a PAK.

Bohemia's publishing documentation says bundling removes unneeded source files:
https://community.bistudio.com/wiki/Arma_Reforger:Mod_Publishing_Process
The exact current publisher extension whitelist was not available in the
inspected installed configuration. Do not claim `.ps1`/`.js` are always excluded;
even bundling their bytes would not make Windows execute them from a PAK.
The installer supplies real filesystem paths instead.

Source migration: Lobby2 repository, release cleanup on 2026-11-11. Existing
runtime protocols and addon GUIDs are unchanged. Historical tests and evidence
were removed from the release source and preserved in a local recovery directory.
