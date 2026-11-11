# Admin missions

Admin Tools has **Missions** under Mission, with **Mission browser** and **End match** subsections. **Gear** is under Players. Mission search, debrief message and countdown inputs have visible text-box backgrounds. Both require a current native admin login; the server checks again for every request and logs accepted/rejected actions through `GC_AdminActionLogger`.

## End match

Choose Mission complete, Faction victory, Faction defeat, Faction draw or Mission failed. Faction results require a current faction. Add an optional debrief message (512 characters maximum), then start/replace the countdown (1–3600 whole seconds), cancel it, or end immediately. These controls use `GC_BoundaryAdmin`, the existing pending-ending owner. Statistics continue during the countdown. The retained native result drives the notification, debrief heading and mission history. Revisions reject stale changes from another admin; cancellation invalidates the old revision. Authored end-mission actions share the native result builder.

## Mission browser

The server fetches the website catalogue through the private `$profile:Lobby2Voting.json` configuration already used by voting. Players only send owner RPCs. There are no client HTTP requests or embedded credentials.

Search matches names, tags and player counts. The table pages through all catalogue entries, including archived/unavailable entries, without the population filter used for voting. Select a row to see its thumbnail, full scrollable description, tags and declared required add-on IDs. Missing thumbnails and unknown player ranges remain explicit. Unavailable missions cannot be selected for loading; the website independently checks availability and scenario identity when accepting the request.

**Load mission** queues an idempotent request on the website. It does not restart or switch the running mission in this slice. A newer selection replaces the server's previous queued request, but not one already claimed by a future worker. The UI reports queuing explicitly. A repeated click for the same selected request uses the same request ID. After a connection error, the outcome may be unknown; reusing that request ID is safe.

The queue snapshots the exact scenario resource, declared `requiredAddonIds` and source metadata. Declared IDs are not a complete transitive dependency graph or proof of installed versions. The future PowerShell restart worker must resolve/validate the owning add-on and its dependency closure before execution. It must not substitute all currently loaded add-ons. This slice does not execute shell commands or resolve dependency versions.
