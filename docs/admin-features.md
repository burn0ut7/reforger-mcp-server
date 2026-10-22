# Admin feature list

Retained and expanded 2026-10-22. All administrative features below require the current logged-in admin session, verified by the server for each operation. GM access or client UI visibility alone is insufficient. This is a planning list; no feature is marked implemented by these studies.

## Retained from Server Admin Tools study

- Player list, search and session history.
- Kick player from server.
- Persistent account-based ban with reason and confirmation.
- Ban list and unban.
- Server status: FPS, uptime and player counts; additional entity metrics as useful.
- GM grant/revoke remains a candidate if appropriate for our game mode.

## Additions to evaluate from PlayableSelector

- Respawn selected player at current position or original spawn position.
- Create a playable slot from an approved character template and location, or make a compatible existing character playable. A dedicated creation form would be our own feature, not an observed PlayableSelector form.
- Assign/reassign players to slots and force entry into the assigned character where appropriate.
- Free a slot without disconnecting the player.
- Lock/unlock a slot, a group, or all eligible slots; define occupied-slot handling explicitly.
- Pin/unpin a player assignment.
- Remove a playable slot, separately from deleting the character/entity; define occupied-player handling first.
- Vehicle availability locks.
- Mission stage and freeze-time controls, faction lock/balance overrides.
- Spectator/admin support: follow a player, identity lookup, admin communication; ordinary social features retain their own permissions.

Rename/duplicate slots and a standalone group editor remain potential additions requiring design, not verified reference features. Automatic admin grants, public MOTD, scheduled announcements, external event APIs and RCON remain outside the proposed first implementation slice.

## Shared design requirements

One server-owned admin policy across UI, RPC, command and editor routes. Derive actor identity from the connection and resolve current target identities server-side. Recheck permissions and targets before delayed execution. Use stable slot identity and explicit availability/occupancy/pin state. Coordinate assignment, faction, group, readiness, voice and controlled-character changes as one operation. Return denial/failure/completion with correlated audit evidence. Confirm destructive actions and report persistence/spawn failure accurately. Preserve unrelated game modes.

## Evidence

- Existing Server Admin Tools study: `.local/admin-study/report.md`.
- PlayableSelector study: `.local/admin-study/lobby/report.md`.
- Both are local source studies. Live multiplayer behavior, rendered UI, compatibility and performance remain unverified. Implementation requires MCP-driven production-handler tests before acceptance.
