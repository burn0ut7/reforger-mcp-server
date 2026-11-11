# Mission voting

## Catalog configuration

The dedicated server reads `$profile:Lobby2Voting.json`, kept private on the machine and outside the addon package:

```json
{
  "url": "https://catalog.example/",
  "apiKey": "<same private machine key>"
}
```

The values are placeholders. Use the website base URL; a trailing slash is added when needed. The key is the same private machine key configured for the website's ingest service. Restrict file access to the dedicated server service account. Players use game owner RPCs; they never receive HTTP access or the key.

The server fetches `GET api/v1/catalog/voting?offset=0` using bearer authentication. The endpoint is server-only and returns pages of 25. Voting candidates are listed available catalog `.conf` headers with valid GUIDs. The game checks installed headers before offering them. Metadata `previewResource` values must name installed game resources, not web URLs.

## Player flow

At `POSTGAME`, the server samples the connected player count once when selecting the ballot. The top three slots randomly select missions with maximum capacity at or above that count; the bottom three randomly select capacities at or below it. Every eligible mission in a group has the same chance: there is no closest-match ranking, 20% cutoff or minimum-player exclusion. Exact-current capacities can appear in either row, once per ballot. If a group is short, it borrows randomly from remaining known capacities, then missions with unknown counts. Only a catalogue with fewer than six installed missions leaves empty slots; those slots cannot receive votes or win. The choices remain stable when players join or leave during voting. Each identity has one vote that can be changed. After 120 seconds, a random tie-break selects a winner; zero votes is also resolved randomly. The winner is revealed for 3 seconds, then the native game transition starts it and retains addons.

The first screen is the vote screen. **Debrief** opens stats; **Back** returns to the vote screen. There is no header tab.

Website session counts and last-started UTC time come from existing `catalog_sessions` records in `running` or `completed` state. The catalog is fetched when POSTGAME starts, without postponing the two-minute deadline.
