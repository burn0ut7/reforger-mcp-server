from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'docs/MissionRules/README.md';s=p.read_text(encoding='utf-8')
s+='''

### Mission interactions

Place **Prefabs/Logic/Mission/Interaction.et**. This is an empty, invisible-in-game entity with GC_MissionInteractionComponent, RplComponent, and a native ActionsManager context/action. The supplied context has Radius 2 and the action Duration 1 second. It uses the game's existing interaction menu and progress behavior, not a custom menu. Place it where players should interact; add visible scenery separately if wanted.

The component provides:

- **Action Name:** the player-facing text, such as Activate radio.
- **Factions:** optional allowed faction keys; empty allows every faction.
- **Repeatable:** off allows one accepted use on this object; on allows later uses.
- **Required Flag:** optional existing Mission Flag that must currently be On. Put complex requirements on that flag in GC Lobby Manager. This is a gate, not a second condition list on the object. Use a persistent flag for a lasting availability gate; repeatable flag pulses are transient framework-cycle values.
- **Call Flag:** optional existing Mission Flag to call after use. Its normal conditions, count limit and repeatability apply. Rejection does not undo an otherwise accepted interaction.

Interactions work only during Match with an active authoritative Lobby2 mission framework. Living, conscious, connected player-controlled characters can use them; spectators, AI and dead/unconscious characters cannot. Availability and use count are replicated by the component. Keep RplComponent on the same entity. The server verifies eligibility again at completion, and performs the mutation once; clients do not execute the effect.

To put this on an existing object, add **GC_MissionInteractionComponent** to that entity, keep/add its **RplComponent**, and add **GC_MissionInteractionAction** under its existing **ActionsManagerComponent > additionalActions**. Set the action's **ParentContextList** to an existing action context on that same entity, or create a native UserActionContext. Native context Position/Radius and action Duration own positioning/reach and hold time. Reuse existing components rather than adding a second ActionsManager. All GC interaction action slots on one entity share its component, label, restrictions and use count; use separate child interaction entities for independent buttons. No changes to unrelated prefabs are required.

To observe use without flags, give the object a unique world name such as RadioSwitch. Add **Interaction Condition** to a mission event and select Has been used (or Has not been used), naming RadioSwitch. The condition reads remembered use, not the interaction's gate or conditions. After the first use, Has been used stays true; each accepted repeat use sends another notification to observing events. Event Advanced > Run still controls whether the event runs once or every time. Other conditions in its group must pass normally. Conditions can also live in an existing Mission Flag definition if the result needs to be combined there, but flags are optional.

Place referenced interaction objects before Match starts. Names resolve at framework startup and retry at Match entry, then the resolved component is cached; there is no world scan or continuous name search. A missing component or deleted object is unavailable, including under None or Has not been used. Use an output flag if memory must survive deletion of the object. Duplicate world names are unsupported; the engine's named lookup is not a uniqueness scan.

Use notifications are processed together with the optional output flag call, preserving pulse-flag behavior and existing loop guards. The same interpreter evaluates all conditions; the interaction object contains no nested framework or condition list. State changes update availability through an event subscription. The existing framework tick observes deletion and drains pending changes; no extra timer or frame callback is added. Stop removes condition listeners, and component deletion removes availability subscriptions. No global modded-class override is used.

Reference research: TilW checkout 4a5300f94fb9323ce4942ef296bcffcdda922626, scripts/Game/UserActions/TILW_BaseInteraction.c and Prefabs/Utils/Interactions/TILW_SetFlagInteraction.et, uses a native scripted action, optional faction/flag filters and single-use state. This implementation reuses the native action/context pattern with original Lobby2 code, authoritative component state, and the existing GC flag semantics; destructive parent deletion and teleport are outside this slice.

Installed engine evidence (catalogue revision 95a1918a7fed40bf884083293ae050d6): game/data/data007.pak/scripts/Game/generated/UserAction/ScriptedUserAction.c documents server-first verification and server-only/nonbroadcast effects (lines 40-53); BaseUserAction.c documents native context access and action-manager ownership. SCR_InteractionHandlerComponent.c lines 347-357 uses controlled-entity origin, context visibility range and squared-distance tolerance 1.1 for a range bound. The extra server bound uses that same calculation and only this action's registered contexts; native action handling still owns exact context eligibility/visibility. Component/player identity checks do not rely on UI visibility alone.

Verification: script compilation/reload and independent source review. Optional GC_InteractionTests.c covers authored input validation, unavailable conditions under None, repeated interaction notifications, stop behavior, and interaction plus pulse-flag dispatch. Those tests were compiled but not run. Native menu appearance, interaction input, resource instantiation, client/server transport and replication/JIP have not been tested in Play, per the requested no-in-game-test scope. Treat those as prototype verification limits, not established runtime results. Existing unrelated Arland resource/menu errors remain.
'''
p.write_text(s,encoding='utf-8')
p=Path('work/mission-rules-cleanup/package.py');s=p.read_text(encoding='utf-8')
s=s.replace("'tests/McpTestAddon/scripts/Game/GC_LivingCharactersTests.c',", "'tests/McpTestAddon/scripts/Game/GC_LivingCharactersTests.c','tests/McpTestAddon/scripts/Game/GC_InteractionTests.c',",1)
s=s.replace('General timer conditions remain a future slice.', 'A reusable Mission Interaction component/action and empty Interaction prefab support native action contexts, optional faction/flag gates, an output flag call, and Interaction Condition. Interaction input, prefab instantiation and network behavior remain untested in Play. General timer conditions remain a future slice.')
p.write_text(s,encoding='utf-8')
