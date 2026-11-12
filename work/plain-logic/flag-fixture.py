from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'tests/McpTestAddon/Prefabs/FlagMemoryExample.et';p.write_text('''GC_LobbyGameMode : "{9B42B808F4E97C4D}Prefabs/MP/Modes/GameMode_Lobby.et" {
 ID "71620F377D834171"
 components {
  GC_LobbyManager "{6A137A39106A4973}" {
   m_aMissionFlags {
    GC_MissionFlag "{71620F377D834172}" {
     m_sName "wave"
     m_bRepeatable 1
     m_aConditions {
      GC_FlagCallCountCondition "{71620F377D834173}" {
       m_sFlagName "wave"
      }
     }
    }
    GC_MissionFlag "{71620F377D834174}" {
     m_sName "remember"
    }
   }
   m_aMissionEvents {
    GC_Event "{71620F377D834175}" {
     m_sName "Each accepted wave call"
     m_aConditions {
      GC_FlagStateCondition "{71620F377D834176}" {
       m_sFlagName "wave"
      }
     }
     m_aActions {
      GC_LogMessageAction "{71620F377D834177}" {
       m_sMessage "Wave call accepted."
      }
     }
     m_Advanced GC_RuleOptions "{71620F377D834178}" {
      m_iRun 1
     }
    }
   }
  }
 }
}
''');s=(r/'tests/McpTestAddon/Prefabs/MissionRulesLegacy.et.meta').read_text().replace('7B115D5DFF7ED997','71620F377D834170').replace('MissionRulesLegacy.et','FlagMemoryExample.et');Path(str(p)+'.meta').write_text(s)
p=r/'tests/McpTestAddon/scripts/Game/GC_FlagMemoryTests.c';s=p.read_text();idx=s.index('\n\tstring RunMemory()');s=s[:idx]+'''
	string RunSerializedMemory()
	{
		Resource resource = Resource.Load("{71620F377D834170}Prefabs/FlagMemoryExample.et");
		if (!resource || !resource.IsValid())
			return "FAIL: flag fixture load";
		BaseContainerList components = resource.GetResource().ToBaseContainer().GetObjectArray("components");
		array<ref GC_MissionFlag> flags = {};
		array<ref GC_CompiledMissionRule> events = {};
		for (int i; i < components.Count(); i++)
		{
			BaseContainer component = components.Get(i);
			if (component.GetClassName() != "GC_LobbyManager")
				continue;
			BaseContainerList savedFlags = component.GetObjectArray("m_aMissionFlags");
			for (int f; f < savedFlags.Count(); f++)
				flags.Insert(GC_MissionFlag.Cast(BaseContainerTools.CreateInstanceFromContainer(savedFlags.Get(f))));
			BaseContainerList savedEvents = component.GetObjectArray("m_aMissionEvents");
			for (int e; e < savedEvents.Count(); e++)
			{
				GC_EventDefinition definition = GC_EventDefinition.Cast(BaseContainerTools.CreateInstanceFromContainer(savedEvents.Get(e)));
				events.Insert(definition.BuildRule());
			}
		}
		if (flags.Count() != 2 || events.Count() != 1 || !flags[0] || !flags[1] || !events[0])
			return "FAIL: flag fixture deserialize";
		Check(flags[0].m_bRepeatable && !flags[1].m_bRepeatable, "saved repeatable and default nonrepeatable");
		GC_MissionRuleEngine engine = new GC_MissionRuleEngine();
		engine.Start(events, flags, SCR_EGameModeState.GAME);
		for (int n; n < 6; n++)
			engine.CallFlag("wave");
		Check(engine.GetRunCount() == 5 && engine.GetFlagCallCount("wave") == 5 && !engine.GetFlag("wave"), "serialized defaults less than five and full event chance");
		engine.CallFlag("remember");
		Check(engine.GetFlag("remember") && !engine.CallFlag("remember"), "serialized default nonrepeatable memory");
		return string.Format("FlagMemoryResource: checks=%1 failures=%2", m_iChecks, m_iFailures);
	}
'''+s[idx:];s=s.replace('\tstring GC_TestFlagAuthority()', '''	string GC_TestFlagMemoryResource()
	{
		GC_FlagMemoryTests tests = new GC_FlagMemoryTests();
		return tests.RunSerializedMemory();
	}

	string GC_TestFlagAuthority()''');p.write_text(s)
