from pathlib import Path
import uuid
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
def uid():return uuid.uuid4().hex[:16].upper()
p=r/'Prefabs/Logic/Mission/Interaction.et'
p.write_text('''GenericEntity {
 ID "'''+uid()+'''"
 components {
  GC_MissionInteractionComponent "{'''+uid()+'''}" {
  }
  ActionsManagerComponent "{'''+uid()+'''}" {
   ActionContexts {
    UserActionContext "{'''+uid()+'''}" {
     ContextName "MissionInteraction"
     Position PointInfo "{'''+uid()+'''}" {
     }
     Radius 2
    }
   }
   additionalActions {
    GC_MissionInteractionAction "{'''+uid()+'''}" {
     ParentContextList {
      "MissionInteraction"
     }
     ActionTitle "MissionInteraction"
     Duration 1
    }
   }
  }
  RplComponent "{'''+uid()+'''}" {
  }
  Hierarchy "{'''+uid()+'''}" {
  }
 }
}
''')
meta=(r/'Prefabs/Logic/Mission/RadiusArea.et.meta').read_text()
import re
meta=re.sub(r'Name ".*"','Name "{'+uid()+'}Prefabs/Logic/Mission/Interaction.et"',meta,count=1)
Path(str(p)+'.meta').write_text(meta)
p=r/'scripts/Game/MissionFramework/GC_ConditionBase.c';s=p.read_text().replace('Area Condition, Group Condition', 'Area Condition, Interaction Condition, Group Condition');p.write_text(s)
