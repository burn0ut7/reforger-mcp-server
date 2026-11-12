# Markers

Admins open **Admin tools > Markers** and enter a plan name.

- **Save** exports the whole mission's shared player markers and supported drawings to the admin computer. Confirm before overwriting a file. Saving remains available during gameplay.
- **Load** replaces the shared map with that named plan, after confirmation.
- **Clear map** removes shared player markers and supported drawings, after confirmation, and updates automatic recovery.

Load and Clear are available before gameplay only. All three require a currently authenticated admin; commander status alone is insufficient. They affect all factions. Private player notes and dynamic markers are excluded. Native faction visibility still applies.

Manual files are in `$profile:Lobby2MapPlans/<plan name>.json`. To share a prepared plan, copy its JSON file into that folder on the receiving admin's computer, enter its filename without `.json`, and Load. Names accept 1-64 letters, numbers, spaces, hyphens and underscores. Manual plans never expire, but must match the mission resource and faction order. Restored markers belong to the mission rather than a previous player; replace/clear them through this panel instead of individual player editing.

Automatic recovery runs on the server after shared changes and refreshes last-active time every 30 seconds. It restores only the immediately last mission, when that same mission restarts within two hours of last activity. Playing another mission with Lobby2 loaded invalidates an existing eligibility record; it does not record the unrelated mission or create a new Lobby2 profile file. Servers which remove Lobby2 entirely for an intervening mission cannot record that mission; remove `Lobby2LastMission.txt` when switching such configurations to invalidate recovery.

The server profile owns `Lobby2MapRecovery0.json` and `Lobby2MapRecovery1.json`, alternating complete snapshots so an interrupted write leaves the preceding complete save. `Lobby2LastMission.txt` independently records last-played eligibility, including when a map snapshot cannot be saved. No gameplay changes or timers are installed in unrelated modes. A recent plan with unavailable drawing support is preserved and recovery pauses; the panel reports the issue. Load the required addons and restart, or explicitly Load/Clear before gameplay. A crash can lose changes still waiting to be written; this is not a transactional disk guarantee.

Plans are limited to 512 markers, 256 drawings and 128 KiB. Invalid or mismatched imports are rejected before replacement.

## Till W Map Drawing

The optional companion addon is in `integrations/MapDrawing`. Load/publish it as a separate addon with both Lobby2 and Till W's **Map Drawing** (`656AC01634459D8D`, tested version 1.1.1) as dependencies. Include that companion in the server's addon set so clients load it too. Base Lobby2 has no mandatory drawing-mod dependency. The installed third-party addon is unchanged and is not redistributed here.

The companion saves shared drawings in the same JSON plan, uses the mod's native replication/cache for restore, and blocks new drawings once gameplay begins. Loading Map Drawing without the companion explicitly disables map-plan operations instead of silently losing drawings. The companion contains source and a project manifest; it still needs normal Workbench packaging/publication for a distributed server.
