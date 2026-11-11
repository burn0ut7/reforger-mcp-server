# Message Action

Use one **Message Action** (`GC_MessageAction`) in an event's Actions list.

| Delivery | Result |
| --- | --- |
| Players | Popup for all connected players, or the selected Factions. Empty Factions includes spectators. |
| Admins only | Popup only for players currently logged in as administrators. Factions is ignored. |
| Server log | Writes the message body to the existing mission-framework server/Workbench log, including its event name. No popup or message-history entry. Title, Factions and Warning are ignored. |

**Warning** selects the existing warning icon, color and sound for either popup delivery. This provides ordinary messages, player warnings, admin messages, admin warnings and log output without separate action types. Add multiple Message Actions when an event should both notify players and write a log.

**Title** is optional, up to 80 characters. Default headings are Mission, Mission Warning, Admin or Admin Warning. The body allows up to 512 characters for popups; markup is rejected. Log output keeps the existing log command's text limits. Blank/whitespace-only messages and invalid delivery values are rejected. Existing faction validation and duplicate-recipient suppression are unchanged for Players.

Admin delivery checks the native current admin roles on the server and again on the receiving controller. It is trusted server-authored mission logic, not an admin request or a client-send permission. Logging in later does not replay old messages. Logout removes admin-only history, visible notices and waiting notices through the existing role-change lifecycle; queued delivery also rechecks login. Ordinary player notices remain visible. Existing admin audit/history messages retain their no-popup behavior.

All popups use the existing bounded notification stack and lifetime. No new layout, transport, timer or global override was added. Old saved Message Actions default to Players and preserve their Warning setting. Old `GC_LogMessageAction` resources still load and execute, but that type is hidden from new authoring; use Message Action / Server log instead.
