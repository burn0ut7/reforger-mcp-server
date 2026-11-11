# Native primitive checks

Production owner: `GC_AdminBanExpiry`. Explicit isolated checks invoke its public parser; they do not issue bans or change live player/mission state.

Mount `scripts/Game` temporarily as a junction under the Workbench profile `scripts/Game/NativePrimitiveChecks`, Reload, start embedded Play, and call `GC_LobbyManager.GC_TestNativePrimitives()` on `GameMode_Lobby2` through Reforger MCP. Remove only the junction and Reload afterward. Keep this fixture outside the production addon.

Validated in Workbench 1.9.0.37 on 2026-11-11: **87 checks, zero failures**, runtime operation `91a0c2f3aa8d4b4a97e1dbe76e9c7fa6`. Covers UTC epoch conversion, leap years, all months across representative years, 2037 upper limit, malformed dates, normalized/invalid dates, and strict numeric syntax. Expected epochs were calculated independently using Python datetime in UTC. Compilation had no script errors; three existing ArlandLobby resource GUID/name errors remain unrelated.

Native evidence: installed catalogue revision `c52648a62fcb4229ad089610d060ea44`, `game/core/data.pak/scripts/Core/generated/System/Time/Timestamp.c` lines 25–34 and `game/core/data.pak/scripts/Core/generated/Types/string.c` lines 397–402. `Timestamp.FromIsoString` reports unparseable input as epoch zero; canonical `ToIsoString` comparison rejects failures and normalized dates while preserving the valid epoch itself. Public input remains `YYYY-MM-DD HH:MM`, UTC, 1970–2037. Duration and authority policies remain unchanged.
