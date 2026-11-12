from pathlib import Path
import uuid
p=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2/worlds/examples/arland/Arland_Lobby_Layers/default.layer')
s=p.read_text(encoding='utf-8')
Path('work/plain-logic/arland-before-area-migration.layer').write_text(s,encoding='utf-8')
old='''      GC_LivingCharactersCondition "{6AD2962A3AE41AB8}" {
       m_iWho "Players and AI"
       m_Requirement GC_LivingProportion "{6AD2962A0E15AFEB}" {
       }
      }'''
new='''      GC_AreaCondition "{6AD2962A3AE41AB8}" {
       m_Targets GC_CharacterAreaTargets "{'''+uuid.uuid4().hex[:16].upper()+'''}" {
        m_iWho "Players and AI"
       }
       m_Requirement GC_LivingProportion "{6AD2962A0E15AFEB}" {
       }
      }'''
assert s.count(old)==1
p.write_text(s.replace(old,new),encoding='utf-8')
p=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2/docs/MissionRules/README.md')
s=p.read_text(encoding='utf-8').replace('No saved Living Characters entries were found in project resources during this change.', 'The reload saved one previously unsaved Living Characters proportion entry from the local scene. It was migrated to Area Condition, preserving Players and AI and the proportion; its previously unset area remains unset. Other scene edits were preserved and are not included in the source commit.')
p.write_text(s,encoding='utf-8')
