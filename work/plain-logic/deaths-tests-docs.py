from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'scripts/Game/MissionFramework/GC_MissionCasualtyTracker.c';s=p.read_text().replace('if (!player)\n\t\t{','if (!player && !starting)\n\t\t{');p.write_text(s)
p=r/'tests/McpTestAddon/scripts/Game/GC_CasualtyConditionTests.c';s=p.read_text();pos=s.index('\t\treturn string.Format("Casualty condition checks:');s=s[:pos]+'''		GC_TotalDeathsCondition total = new GC_TotalDeathsCondition();
		total.m_aFactions = {"US"};
		total.m_iComparison = 0;
		total.m_iRequiredDeaths = 4;
		GC_RuleCondition totalCheck = total.BuildCheck();
		GC_CasualtySnapshot repeatedLives = new GC_CasualtySnapshot();
		GC_CasualtyTotals original = repeatedLives.AddStartingLife("US", true);
		original.m_iDeaths = 1;
		for (int life; life < 4; life++)
			repeatedLives.RecordTotalDeath("US", true);
		Check(!totalCheck.Evaluate(null, SCR_EGameModeState.GAME, null, null, null, repeatedLives), "four deaths do not exceed four");
		repeatedLives.RecordTotalDeath("US", true);
		Check(totalCheck.Evaluate(null, SCR_EGameModeState.GAME, null, null, null, repeatedLives), "replacement lives can reach five total deaths");
		Check(original.m_iStarting == 1 && original.m_iDeaths == 1, "total deaths never alter starting-life casualty ratio");
		totalCheck.m_iDeathComparison = 1;
		Check(!totalCheck.Evaluate(null, SCR_EGameModeState.GAME, null, null, null, repeatedLives), "five deaths are not less than four");
		totalCheck.m_iRequiredDeaths = 5;
		totalCheck.m_iDeathComparison = 2;
		Check(totalCheck.Evaluate(null, SCR_EGameModeState.GAME, null, null, null, repeatedLives), "exact total death count");
		totalCheck.m_iDeathComparison = 3;
		Check(!totalCheck.Evaluate(null, SCR_EGameModeState.GAME, null, null, null, repeatedLives), "Not equal rejects equal death count");
		repeatedLives.RecordTotalDeath("US", true);
		Check(totalCheck.Evaluate(null, SCR_EGameModeState.GAME, null, null, null, repeatedLives), "Not equal accepts a different death count");
		totalCheck.m_iDeathComparison = 2;
		totalCheck.m_iRequiredDeaths = 6;
		repeatedLives.RecordTotalDeath("USSR", true);
		repeatedLives.RecordTotalDeath("US", false);
		Check(totalCheck.Evaluate(null, SCR_EGameModeState.GAME, null, null, null, repeatedLives), "death count uses shared faction and population filters");
		totalCheck.m_aCasualtyFactions = {};
		totalCheck.m_iCasualtyCount = 2;
		totalCheck.m_iRequiredDeaths = 8;
		Check(totalCheck.Evaluate(null, SCR_EGameModeState.GAME, null, null, null, repeatedLives), "empty filter and Both combine all deaths");
		total.m_iComparison = 4;
		Check(!total.BuildCheck(), "invalid death comparison rejected");
		total.m_iComparison = 0;
		total.m_iRequiredDeaths = -1;
		Check(!total.BuildCheck(), "negative death threshold rejected");
		total.m_iRequiredDeaths = 0;
		authored.m_aConditions = {total};
		GC_CasualtySnapshot fresh = new GC_CasualtySnapshot();
		engine.Start({authored.BuildRule()}, null, SCR_EGameModeState.GAME, null, fresh);
		Check(engine.GetRunCount() == 0, "no deaths initially even without a starting population");
		fresh.RecordTotalDeath("US", true);
		engine.SetCasualties(fresh);
		Check(engine.GetRunCount() == 1, "post-start life death can activate Total Deaths");
		engine.Stop();
'''+s[pos:];p.write_text(s)
p=r/'docs/MissionRules/README.md';s=p.read_text().replace('**Casualties**, **Condition Group**','**Casualties**, **Total Deaths**, **Condition Group**');s+='''

## Total Deaths condition (respawn missions)

**Total Deaths** counts confirmed character deaths during Match, including deaths of respawned characters, later joins and reinforcements. It does not use a starting population or a casualty-ratio field.

- **Factions**: exact faction keys; empty means all. Uses faction at death, unlike Casualties which keeps each starting life's faction.
- **Count**: Players, AI, or Players and AI. Player victims reported by the engine, claimed player-slot characters, and tracked starting player lives count as Players. Spectators and unoccupied playable AI slots are excluded. The same faction/population matching implementation serves both condition types.
- **Comparison**: Greater than, Less than, Equal, or Not equal.
- **Required Deaths**: a whole number. Greater than 4 means five or more deaths; default Greater than 0 means the first death.

Each character life adds at most one death. Respawning creates another life whose later death counts again. A later life does not change Casualties' starting population or counted starting deaths. For example, one starting player can reach 100% Casualties after the original life dies, then accumulate five Total Deaths over five lives. Deleting a living body or disconnecting adds no death. Total Deaths starts at zero even when the initial population was empty. Equal 0 and Less than a positive threshold can therefore pass at Match start. Final totals remain readable after Match; Stop/fresh Start clears the engine context.

Two objects keep the different fields clear: **Casualties** has a ratio; **Total Deaths** has a comparison and number. Both integrate into the same nested condition lists and optional mission flags, using the same authoritative death notification and tracker. There is no separate faction condition and no irrelevant ratio field on Total Deaths.
''';p.write_text(s)
