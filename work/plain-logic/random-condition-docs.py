from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'scripts/Game/MissionFramework/GC_RuleCondition.c';s=p.read_text().replace('\t\tprotected bool HasEventContext','\tprotected bool HasEventContext').replace('HasEventContext','HasEvaluationContext');p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_MissionRuleEngine.c';s=p.read_text().replace('if (m_eGameModeState != SCR_EGameModeState.GAME)\n\t\t\treturn;\n\t\tGC_MissionRuleEvent notification', 'if (m_eGameModeState != SCR_EGameModeState.GAME || m_aRandomConditions.IsEmpty())\n\t\t\treturn;\n\t\tGC_MissionRuleEvent notification');p.write_text(s)
p=r/'docs/MissionRules/README.md';s=p.read_text().replace('**Mission Event Condition**, **Condition Group**','**Mission Event Condition**, **Random Chance**, **Condition Group**');s+='''

## Scheduled Random Chance condition

Add **Random Chance** inside an event or flag's Conditions list, including inside Condition Group and Condition Count.

- **Check Interval**: seconds between rolls; default 60, allowed 0.1 to 86400. The first roll waits one full interval after Match starts (or after the framework starts if Match is already active). No rolls run in preview, role selection, briefing or after action.
- **Chance**: a 0-to-1 float, default 1. `1` means 100%, `0.05` means 5%, `0.0005` means 0.05%, and `0` never produces true. Decimal entry is not limited to a one-percent spinner step.
- A result is held until the next scheduled roll. Reading a condition never rerolls it. Other matching flag/event changes can use that held result, so an Every time event can also run between rolls when those other conditions change.
- Before every random leaf in a condition tree has received its first result, that tree cannot pass, including under None or Any. Different intervals keep their own schedules and combine their latest results after initialization. Each due roll notifies its owning event/flag even if its answer has not changed, allowing repeated successes. Multiple leaves due in the same update are rolled before owners are checked, with one eligible event attempt per notification chain.
- Events still run once by default. Choose **Advanced > Run > Every time** for repeated activations. Flags retain their own Repeatable and accepted-call-count rules. No flag is required to use this condition.
- All/Any/None/Not all and Condition Count combine this answer normally. A failed roll can therefore satisfy None. Invalid chance/interval values never become passing answers through inversion.
- Delay keeps its existing meaning: latest conditions must pass when the delay ends. A later failed roll can prevent the delayed event from running. Pending events do not stack additional delayed runs. Advanced > Random Chance is a separate additional chance gate; leave it at 1 when only this condition should control probability.
- A random condition on a Random Event Group schedules group selection. A random condition inside a child only filters that child; it does not start its parent group. All random-group eligibility rules still apply.
- Leaving Match clears held results and cancels pending events that depend on them. Stop/fresh Start clears them too. Late updates roll once and schedule the next interval from that update, without replaying missed rolls in a burst. The existing manager tick supplies scheduling, with its authority/mode guard, and now also supports flag-only configurations.

For a 5% chance every 90 seconds, set Check Interval to `90` and Chance to `0.05`. Set the owning event to Every time if it should continue after a successful execution. This is a fresh independent chance each interval, not a guarantee that one in every twenty intervals succeeds.

This slice adds no weighted selection, cooldown, changing probability or shared random-result catalog. Existing Random Event Groups handle selection; optional flags can remember a result when the mission explicitly needs shared memory.

Verification: production Workbench script compilation and source-level logic review. Deterministic regression scenarios are provided in the optional `GC_RandomConditionTests.c`; they were not executed in-game, in accordance with the requested verification scope.
''';p.write_text(s)
