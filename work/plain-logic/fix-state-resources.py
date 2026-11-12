from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'Prefabs/MP/Modes/GameMode_Lobby_RulesExample.et';s=p.read_text(encoding='utf-8');needle='''     m_aConditions {
      GC_GameModeStateCondition "{7B115D5DFF7EDA10}"''';s=s.replace(needle,'''     m_aActions {
      GC_SetFlagAction "{7B115D5DFF7EDA02}" {
       m_sFlagName "test_ready"
       m_iSetTo 1
      }
     }
'''+needle);p.write_text(s,encoding='utf-8')
p=r/'worlds/examples/arland/Arland_Lobby_Layers/default.layer';s=p.read_text(encoding='utf-8');s=s.replace('''     m_Event GC_MissionEvent "{6AD24908E66798EE}" {
     }''','''     m_aConditions {
      GC_GameModeStateCondition "{6AD24908E66798EE}" {
       m_eGameModeState 1
      }
     }''');p.write_text(s,encoding='utf-8')
