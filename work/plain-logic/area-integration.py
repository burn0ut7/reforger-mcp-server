from pathlib import Path
p=Path(r'C:\Users\Gray\Documents\My Games\ArmaReforgerWorkbench\addons\Lobby2')
def edit(name,fn):
 f=p/name;s=f.read_text();f.write_text(fn(s),newline='\n')
def condition(s):
 s=s.replace('\tfloat m_fDamageNeeded;', '\tfloat m_fDamageNeeded;\n\tstring m_sAreaTarget;\n\tstring m_sAreaName;\n\tbool m_bAreaOutside;')
 s=s.replace('m_iCheck > 9','m_iCheck > 10')
 s=s.replace('\t\tif (m_iCheck == 9)\n\t\t\treturn !m_sDamageEntity', '\t\tif (m_iCheck == 10)\n\t\t\treturn !m_sAreaTarget.IsEmpty() && !m_sAreaName.IsEmpty();\n\t\tif (m_iCheck == 9)\n\t\t\treturn !m_sDamageEntity')
 start=s.index('\t//------------------------------------------------------------------------------------------------\n\tbool UsesEntityDamage')
 end=s.index('\t//------------------------------------------------------------------------------------------------\n\tbool UsesCasualties', start)
 area=s[start:end].replace('EntityDamage','Area').replace('m_iCheck == 9','m_iCheck == 10')
 s=s[:start]+area+s[start:]
 s=s.replace('ref GC_EntityDamageObservation> damage, int depth = 0)', 'ref GC_EntityDamageObservation> damage, GC_MissionAreaTracker areas, int depth = 0)')
 s=s.replace('\t\tif (m_iCheck == 9)\n\t\t{\n\t\t\tGC_EntityDamageObservation', '\t\tif (m_iCheck == 10)\n\t\t{\n\t\t\tGC_MissionAreaObservation area;\n\t\t\tif (areas)\n\t\t\t\tarea = areas.GetObservation(this);\n\t\t\treturn area && area.IsAvailable();\n\t\t}\n\t\tif (m_iCheck == 9)\n\t\t{\n\t\t\tGC_EntityDamageObservation')
 s=s.replace('randomResults, damage, depth + 1)', 'randomResults, damage, areas, depth + 1)')
 s=s.replace('ref GC_EntityDamageObservation> damage = null)', 'ref GC_EntityDamageObservation> damage = null, GC_MissionAreaTracker areas = null)')
 s=s.replace('HasEvaluationContext(events, randomResults, damage)', 'HasEvaluationContext(events, randomResults, damage, areas)')
 s=s.replace('randomResults, casualties, damage);', 'randomResults, casualties, damage, areas);')
 s=s.replace('ref GC_EntityDamageObservation> damage)', 'ref GC_EntityDamageObservation> damage, GC_MissionAreaTracker areas)')
 s=s.replace('\t\telse if (m_iCheck == 9)', '\t\telse if (m_iCheck == 10)\n\t\t\tanswer = areas.GetObservation(this).Passes(m_bAreaOutside);\n\t\telse if (m_iCheck == 9)')
 s=s.replace('randomResults, casualties, damage))', 'randomResults, casualties, damage, areas))')
 return s
edit('scripts/Game/MissionFramework/GC_RuleCondition.c',condition)
def engine(s):
 s=s.replace('ref array<GC_RuleCondition> m_aDamageChecks;', 'ref array<GC_RuleCondition> m_aDamageChecks;\n\tref array<GC_RuleCondition> m_aAreaChecks;')
 s=s.replace('\tprotected BaseWorld m_DamageWorld;', '\tprotected BaseWorld m_DamageWorld;\n\tprotected ref GC_MissionAreaTracker m_Areas;\n\tprotected float m_fNextAreaCheck;')
 s=s.replace('\t\tRegisterEntityDamageConditions();', '\t\tRegisterEntityDamageConditions();\n\t\tRegisterAreaConditions(damageWorld);')
 s=s.replace('\t\tResetRandomConditions();\n\t\tif (state ==', '\t\tResetRandomConditions();\n\t\tSampleAreas(true);\n\t\tif (state ==')
 s=s.replace('\t\tm_DamageWorld = null;', '\t\tm_DamageWorld = null;\n\t\tm_Areas = null;')
 s=s.replace('m_Casualties, m_mDamage)', 'm_Casualties, m_mDamage, m_Areas)')
 s=s.replace('\t\tAdvanceRandomConditions();', '\t\tAdvanceRandomConditions();\n\t\tif (m_eGameModeState == SCR_EGameModeState.GAME && m_fNow >= m_fNextAreaCheck)\n\t\t\tSampleAreas();')
 s=s.replace('state.m_Rule.m_If.UsesEntityDamage()', 'state.m_Rule.m_If.UsesEntityDamage() || state.m_Rule.m_If.UsesArea()')
 s=s.replace('\t\t\tbool damageChanged =', '\t\t\tbool areaChanged = notification.m_eKind == GC_ERuleEvent.AREA_CHANGED;\n\t\t\tbool damageChanged =')
 s=s.replace('|| (damageChanged && memory.m_Condition.UsesEntityDamage(notification.m_aDamageChecks))', '|| (damageChanged && memory.m_Condition.UsesEntityDamage(notification.m_aDamageChecks))\n\t\t\t\t\t|| (areaChanged && memory.m_Condition.UsesArea(notification.m_aAreaChecks))')
 s=s.replace('|| (damageChanged && rule.m_If.UsesEntityDamage(notification.m_aDamageChecks))))', '|| (damageChanged && rule.m_If.UsesEntityDamage(notification.m_aDamageChecks))\n\t\t\t\t\t|| (areaChanged && rule.m_If.UsesArea(notification.m_aAreaChecks))))')
 at='\t//------------------------------------------------------------------------------------------------\n\tprotected void RegisterEntityDamageConditions()'
 methods='''\t//------------------------------------------------------------------------------------------------
\tprotected void RegisterAreaConditions(BaseWorld world)
\t{
\t\tarray<GC_RuleCondition> conditions = {};
\t\tforeach (GC_MissionRuleState state : m_aStates)
\t\t{
\t\t\tif (state.m_bValid && state.m_Rule.m_bEnabled && state.m_Rule.m_If)
\t\t\t\tstate.m_Rule.m_If.CollectAreaConditions(conditions);
\t\t}
\t\tforeach (string name, GC_MissionFlagState memory : m_mFlagDefinitions)
\t\t{
\t\t\tif (memory.m_Condition)
\t\t\t\tmemory.m_Condition.CollectAreaConditions(conditions);
\t\t}
\t\tif (conditions.IsEmpty())
\t\t\treturn;
\t\tm_Areas = new GC_MissionAreaTracker(world);
\t\tbool overflow;
\t\tforeach (GC_RuleCondition condition : conditions)
\t\t{
\t\t\tif (!m_Areas.Register(condition))
\t\t\t\toverflow = true;
\t\t}
\t\tif (overflow)
\t\t\tPrint("GC | Mission Framework | More than 256 unique target/area pairs. Extra area conditions cannot pass.", LogLevel.ERROR);
\t\tSampleAreas(true);
\t}

\t//------------------------------------------------------------------------------------------------
\t//! Position changes have no general trigger callback. Sample once a second, then publish changes.
\tprotected void SampleAreas(bool initial = false)
\t{
\t\tm_fNextAreaCheck = m_fNow + 1;
\t\tif (!m_Areas)
\t\t\treturn;
\t\tGC_MissionRuleEvent notification = new GC_MissionRuleEvent(GC_ERuleEvent.AREA_CHANGED, "");
\t\tnotification.m_aAreaChecks = {};
\t\tm_Areas.Sample(notification.m_aAreaChecks, initial);
\t\tif (m_eGameModeState == SCR_EGameModeState.GAME && !notification.m_aAreaChecks.IsEmpty())
\t\t\tm_aEvents.Insert(notification);
\t}

'''
 assert at in s
 return s.replace(at,methods+at)
edit('scripts/Game/MissionFramework/GC_MissionRuleEngine.c',engine)
edit('scripts/Game/MissionFramework/GC_MissionAreaTracker.c',lambda s:s.replace('closed, non-flat polygon','closed polygon with nonzero horizontal area').replace('void Sample(array<GC_RuleCondition> changed)', 'void Sample(array<GC_RuleCondition> changed, bool initial = false)').replace('if (observation.m_bChanged)', 'if (initial || observation.m_bChanged)'))
edit('scripts/Game/MissionFramework/GC_CompiledMissionRule.c', lambda s:s.replace('\tENTITY_DAMAGE_CHANGED\n', '\tENTITY_DAMAGE_CHANGED,\n\tAREA_CHANGED\n'))
edit('scripts/Game/MissionFramework/GC_ConditionBase.c',lambda s:s.replace('Entity Damage, Condition Group','Entity Damage, Area Condition, Condition Group'))
edit('scripts/WorkbenchGame/GC_AOPolygonTool.c',lambda s:s.replace('!GC_SetupZoneEntity.Cast(shape) &&', '!GC_SetupZoneEntity.Cast(shape) && !GC_PolygonAreaEntity.Cast(shape) &&').replace('AO limit or setup zone','AO limit, setup zone or mission polygon area').replace('MapShape/AOLimit prefab','MapShape/AOLimit/PolygonArea prefab'))
import uuid
for name,root,settings in [('RadiusArea','GC_RadiusAreaEntity',' m_fRadius 10\n'),('PolygonArea','GC_PolygonAreaEntity',' IsClosed 1\n LineColor 0.2 0.6 1 1\n')]:
 d=p/'Prefabs/Logic/Mission'; d.mkdir(parents=True,exist_ok=True)
 guid=uuid.uuid4().hex[:16].upper(); eid=uuid.uuid4().hex[:16].upper()
 (d/(name+'.et')).write_text(root+' {\n ID "'+eid+'"\n'+settings+'}\n')
 (d/(name+'.et.meta')).write_text('MetaFileClass {\n Name "{'+guid+'}Prefabs/Logic/Mission/'+name+'.et"\n Configurations {\n  EntityTemplateResourceClass PC {\n  }\n  EntityTemplateResourceClass XBOX_ONE : PC {\n  }\n  EntityTemplateResourceClass XBOX_SERIES : PC {\n  }\n  EntityTemplateResourceClass PS4 : PC {\n  }\n  EntityTemplateResourceClass PS5 : PC {\n  }\n  EntityTemplateResourceClass HEADLESS : PC {\n  }\n }\n}\n')
