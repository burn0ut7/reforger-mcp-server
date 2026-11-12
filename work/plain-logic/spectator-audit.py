from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
for name in ('GC_CasualtiesCondition.c','GC_TotalDeathsCondition.c'):
 p=r/'scripts/Game/MissionFramework'/name
 s=p.read_text().replace('Faction keys to include, such as US or USSR. Leave', 'Spectator bodies never count, with any faction or Count setting. Faction keys to include, such as US or USSR. Leave')
 p.write_text(s)
p=r/'docs/MissionRules/README.md';s=p.read_text()+'''

### Spectator exclusion audit

Casualties and Total Deaths reject every entity carrying `GC_LobbySpectatorBodyComponent` before faction/population selection. This applies both to the starting-life capture and the death-recording path; empty faction filters and Players and AI do not bypass it. Lobby2's dedicated spectator prefab contains that component, and the manager uses that prefab or reuses the same marked body for spectator/reconnect assignment. Following another player moves the spectator camera/body rather than adding another combat life.

The actual soldier's death remains a casualty when its player subsequently becomes a spectator. Checking the controller's current spectator state instead of the victim body could wrongly suppress that legitimate death. Connected claimed slot characters awaiting Match-entry possession remain intended player lives, not spectator bodies. AI is counted only by the AI or Players and AI selection. Disconnect/delete notifications do not increment either death measure.

This conclusion is based on source/prefab inspection, including an independent audit; it is not a new live spectator/respawn test. Custom spectator prefabs must retain `GC_LobbySpectatorBodyComponent`; unrelated mods' unmarked spectator characters cannot be identified reliably by this marker-based contract. The built-in Lobby2 paths inspected retain it. Only tooltip/documentation clarification was needed; death-counting behavior is unchanged.
''';p.write_text(s)
