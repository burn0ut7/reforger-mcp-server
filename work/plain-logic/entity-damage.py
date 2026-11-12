from pathlib import Path
p=Path(r'C:\Users\Gray\Documents\My Games\ArmaReforgerWorkbench\addons\Lobby2')
def edit(name, fn):
    f=p/name
    s=f.read_text()
    f.write_text(fn(s), newline='\n')
def condition(s):
    s=s.replace('\tint m_iRequiredDeaths;', '\tint m_iRequiredDeaths;\n\tstring m_sDamageEntity;\n\tstring m_sDamageArea;\n\tint m_iDamageCheck;\n\tfloat m_fDamageNeeded;')
    s=s.replace('m_iCheck > 8', 'm_iCheck > 9')
    s=s.replace('\t\tif (m_iCheck == 7 || m_iCheck == 8)\n\t\t{', '\t\tif (m_iCheck == 9)\n\t\t\treturn !m_sDamageEntity.IsEmpty() && m_iDamageCheck >= 0 && m_iDamageCheck <= 2 && (m_iDamageCheck != 2 || (m_fDamageNeeded >= 0 && m_fDamageNeeded <= 1));\n\t\tif (m_iCheck == 7 || m_iCheck == 8)\n\t\t{',1)
    at='\t//------------------------------------------------------------------------------------------------\n\tbool UsesCasualties'
    methods='''\t//------------------------------------------------------------------------------------------------
\tbool UsesEntityDamage(array<GC_RuleCondition> changed = null, int depth = 0)
\t{
\t\tif (depth > 16)
\t\t\treturn false;
\t\tif (m_iCheck == 9)
\t\t\treturn !changed || changed.Contains(this);
\t\tif (m_iCheck == 0 && m_aChecks)
\t\t{
\t\t\tforeach (GC_RuleCondition child : m_aChecks)
\t\t\t{
\t\t\t\tif (child && child.UsesEntityDamage(changed, depth + 1))
\t\t\t\t\treturn true;
\t\t\t}
\t\t}
\t\treturn false;
\t}

\t//------------------------------------------------------------------------------------------------
\tvoid CollectEntityDamageConditions(array<GC_RuleCondition> result, int depth = 0)
\t{
\t\tif (depth > 16)
\t\t\treturn;
\t\tif (m_iCheck == 9 && !result.Contains(this))
\t\t\tresult.Insert(this);
\t\tif (m_iCheck == 0 && m_aChecks)
\t\t{
\t\t\tforeach (GC_RuleCondition child : m_aChecks)
\t\t\t{
\t\t\t\tif (child)
\t\t\t\t\tchild.CollectEntityDamageConditions(result, depth + 1);
\t\t\t}
\t\t}
\t}

'''
    assert at in s
    s=s.replace(at,methods+at)
    s=s.replace('map<GC_RuleCondition, bool> randomResults, int depth = 0)', 'map<GC_RuleCondition, bool> randomResults, map<GC_RuleCondition, ref GC_EntityDamageObservation> damage, int depth = 0)')
    s=s.replace('\t\tif (m_iCheck == 6)\n\t\t\treturn randomResults', '\t\tif (m_iCheck == 9)\n\t\t{\n\t\t\tGC_EntityDamageObservation observation;\n\t\t\treturn damage && damage.Find(this, observation) && observation && observation.IsAvailable();\n\t\t}\n\t\tif (m_iCheck == 6)\n\t\t\treturn randomResults')
    s=s.replace('HasEvaluationContext(events, randomResults, depth + 1)', 'HasEvaluationContext(events, randomResults, damage, depth + 1)')
    s=s.replace('GC_CasualtySnapshot casualties = null)', 'GC_CasualtySnapshot casualties = null, map<GC_RuleCondition, ref GC_EntityDamageObservation> damage = null)')
    s=s.replace('HasEvaluationContext(events, randomResults)', 'HasEvaluationContext(events, randomResults, damage)')
    s=s.replace('randomResults, casualties);', 'randomResults, casualties, damage);')
    s=s.replace('GC_CasualtySnapshot casualties)', 'GC_CasualtySnapshot casualties, map<GC_RuleCondition, ref GC_EntityDamageObservation> damage)')
    s=s.replace('\t\telse if (m_iCheck == 8)', '\t\telse if (m_iCheck == 9)\n\t\t\tanswer = damage.Get(this).Passes(m_iDamageCheck, m_fDamageNeeded);\n\t\telse if (m_iCheck == 8)')
    s=s.replace('randomResults, casualties))', 'randomResults, casualties, damage))')
    return s
edit('scripts/Game/MissionFramework/GC_RuleCondition.c', condition)
def engine(s):
    s=s.replace('ref array<GC_RuleCondition> m_aRandomChecks;', 'ref array<GC_RuleCondition> m_aRandomChecks;\n\tref array<GC_RuleCondition> m_aDamageChecks;')
    s=s.replace('\tprotected ref GC_CasualtySnapshot m_Casualties;', '\tprotected ref GC_CasualtySnapshot m_Casualties;\n\tprotected BaseWorld m_DamageWorld;\n\tprotected ref map<GC_RuleCondition, ref GC_EntityDamageObservation> m_mDamage = new map<GC_RuleCondition, ref GC_EntityDamageObservation>();')
    s=s.replace('GC_CasualtySnapshot casualties = null)', 'GC_CasualtySnapshot casualties = null, BaseWorld damageWorld = null)')
    s=s.replace('\t\tRegisterRandomConditions();', '\t\tRegisterRandomConditions();\n\t\tm_DamageWorld = damageWorld;\n\t\tRegisterEntityDamageConditions();')
    s=s.replace('\t\tResetRandomConditions();\n\t\tforeach (GC_MissionRuleState pending', '\t\tResetRandomConditions();\n\t\tif (state == SCR_EGameModeState.GAME)\n\t\t\tBindEntityDamageConditions();\n\t\tforeach (GC_MissionRuleState pending')
    s=s.replace('\t\tDrainEvents();\n\t}\n\n\t//------------------------------------------------------------------------------------------------\n\tvoid Stop()', '\t\tif (state == SCR_EGameModeState.GAME)\n\t\t\tQueueEntityDamageChanges();\n\t\tDrainEvents();\n\t}\n\n\t//------------------------------------------------------------------------------------------------\n\tvoid Stop()')
    s=s.replace('\t\tm_Casualties = null;', '\t\tm_Casualties = null;\n\t\tforeach (GC_RuleCondition check, GC_EntityDamageObservation observation : m_mDamage)\n\t\t\tobservation.Stop();\n\t\tm_mDamage.Clear();\n\t\tm_DamageWorld = null;')
    s=s.replace('m_mRandomResults, m_Casualties)', 'm_mRandomResults, m_Casualties, m_mDamage)')
    s=s.replace('\t\tAdvanceRandomConditions();', '\t\tAdvanceRandomConditions();\n\t\tQueueEntityDamageChanges();')
    s=s.replace('state.m_Rule.m_If.UsesCasualties()', 'state.m_Rule.m_If.UsesCasualties() || state.m_Rule.m_If.UsesEntityDamage()')
    s=s.replace('\t\t\tbool casualtyChanged =', '\t\t\tbool damageChanged = notification.m_eKind == GC_ERuleEvent.ENTITY_DAMAGE_CHANGED;\n\t\t\tbool casualtyChanged =')
    s=s.replace('|| (casualtyChanged && memory.m_Condition.UsesCasualties())', '|| (casualtyChanged && memory.m_Condition.UsesCasualties())\n\t\t\t\t\t|| (damageChanged && memory.m_Condition.UsesEntityDamage(notification.m_aDamageChecks))')
    s=s.replace('|| (casualtyChanged && rule.m_If.UsesCasualties())))', '|| (casualtyChanged && rule.m_If.UsesCasualties())\n\t\t\t\t\t|| (damageChanged && rule.m_If.UsesEntityDamage(notification.m_aDamageChecks))))')
    at='\t//------------------------------------------------------------------------------------------------\n\tprotected void RegisterRandomConditions()'
    methods='''\t//------------------------------------------------------------------------------------------------
\tprotected void RegisterEntityDamageConditions()
\t{
\t\tarray<GC_RuleCondition> conditions = {};
\t\tforeach (GC_MissionRuleState state : m_aStates)
\t\t{
\t\t\tif (state.m_bValid && state.m_Rule.m_bEnabled && state.m_Rule.m_If)
\t\t\t\tstate.m_Rule.m_If.CollectEntityDamageConditions(conditions);
\t\t}
\t\tforeach (string name, GC_MissionFlagState memory : m_mFlagDefinitions)
\t\t{
\t\t\tif (memory.m_Condition)
\t\t\t\tmemory.m_Condition.CollectEntityDamageConditions(conditions);
\t\t}
\t\tforeach (GC_RuleCondition condition : conditions)
\t\t\tm_mDamage.Set(condition, new GC_EntityDamageObservation());
\t\tBindEntityDamageConditions();
\t}

\t//------------------------------------------------------------------------------------------------
\tprotected void BindEntityDamageConditions()
\t{
\t\tforeach (GC_RuleCondition condition, GC_EntityDamageObservation observation : m_mDamage)
\t\t\tobservation.Bind(m_DamageWorld, condition);
\t}

\t//------------------------------------------------------------------------------------------------
\t//! Coalesce health/state callbacks so the complete tree sees the latest settled damage values.
\tprotected void QueueEntityDamageChanges()
\t{
\t\tif (m_eGameModeState != SCR_EGameModeState.GAME || m_mDamage.IsEmpty())
\t\t\treturn;
\t\tGC_MissionRuleEvent notification = new GC_MissionRuleEvent(GC_ERuleEvent.ENTITY_DAMAGE_CHANGED, "");
\t\tnotification.m_aDamageChecks = {};
\t\tforeach (GC_RuleCondition condition, GC_EntityDamageObservation observation : m_mDamage)
\t\t{
\t\t\tif (observation.ConsumeChange())
\t\t\t\tnotification.m_aDamageChecks.Insert(condition);
\t\t}
\t\tif (!notification.m_aDamageChecks.IsEmpty())
\t\t\tm_aEvents.Insert(notification);
\t}

'''
    assert at in s
    return s.replace(at,methods+at)
edit('scripts/Game/MissionFramework/GC_MissionRuleEngine.c',engine)
edit('scripts/Game/MissionFramework/GC_CompiledMissionRule.c', lambda s:s.replace('\tCASUALTIES_CHANGED\n','\tCASUALTIES_CHANGED,\n\tENTITY_DAMAGE_CHANGED\n'))
edit('scripts/Game/GC_LobbyManager.c',lambda s:s.replace('eventNames, casualties);', 'eventNames, casualties, GetGame().GetWorld());'))
edit('scripts/Game/MissionFramework/GC_ConditionBase.c',lambda s:s.replace('Casualties, Total Deaths, Condition Group','Casualties, Total Deaths, Entity Damage, Condition Group'))
