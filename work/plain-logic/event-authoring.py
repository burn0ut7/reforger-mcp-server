from pathlib import Path
import re
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
# Exact type rename; preserve similarly named interpreter types and all unrelated content.
for root in [r/'scripts',r/'tests/McpTestAddon/scripts',r/'Prefabs',r/'worlds',r/'tests/McpTestAddon/worlds',r/'tests/McpTestAddon/Prefabs']:
 for p in root.rglob('*'):
  if p.suffix not in ['.c','.et','.layer','.conf']: continue
  raw=p.read_bytes()
  changed=re.sub(rb'\bGC_MissionRule\b',b'GC_Event',raw)
  if changed!=raw:p.write_bytes(changed)
p=r/'scripts/Game/MissionFramework/GC_MissionRule.c';p.rename(p.with_name('GC_Event.c'))
p=r/'scripts/Game/MissionFramework/GC_Event.c'
p.write_text('''[BaseContainerProps(), BaseContainerCustomStringTitleField("Advanced event settings")]
class GC_RuleOptions
{
	[Attribute("0", UIWidgets.ComboBox, "Once is enough for most events. Every time allows later matching state changes, not repeated execution while a state stays the same.", enums: {ParamEnum("Once", "0"), ParamEnum("Every time", "1")})]
	int m_iRun;
	[Attribute("0", UIWidgets.SpinBox, "Wait this many seconds before running. Zero means now. Conditions must still pass when the wait ends.", "0 86400 0.1")]
	float m_fWaitSeconds;
	[Attribute("1", UIWidgets.SpinBox, "Chance to run this event's actions, from 0 to 1. For example, 0.8 is 80%, 1 is 100%, and 0 never runs. The default is 1; leaving this setting or Advanced unset means 100%. This does not spawn anything unless the actions do so.", "0 1 0.01")]
	float m_fRandomChance;
	[Attribute("0", UIWidgets.CheckBox, "Keep this event in the list without running it.")]
	bool m_bDisabled;
}

//! Shared serialized list type. Incomplete entries fail closed.
[BaseContainerProps(visible: false, insertable: false)]
class GC_EventDefinition
{
	//------------------------------------------------------------------------------------------------
	GC_CompiledMissionRule BuildRule()
	{
		return null;
	}

	//------------------------------------------------------------------------------------------------
	protected GC_CompiledMissionRule BuildSettings(string name, array<ref GC_MissionCondition> conditions, GC_RuleOptions advanced)
	{
		GC_CompiledMissionRule rule = new GC_CompiledMissionRule();
		rule.m_sName = name;
		rule.m_bEnabled = true;
		rule.m_bRunOnlyOnce = true;
		rule.m_fEverySeconds = 5;
		rule.m_aThen = {};
		if (advanced)
		{
			if (advanced.m_iRun < 0 || advanced.m_iRun > 1 || advanced.m_fRandomChance < 0 || advanced.m_fRandomChance > 1)
				return null;
			rule.m_bEnabled = !advanced.m_bDisabled;
			rule.m_bRunOnlyOnce = advanced.m_iRun == 0;
			rule.m_fWaitSeconds = advanced.m_fWaitSeconds;
			rule.m_fChance = advanced.m_fRandomChance;
		}
		if (conditions && !conditions.IsEmpty())
		{
			rule.m_If = GC_MissionCondition.BuildGroup(conditions, GC_ERuleCheck.ALL, 0, 0);
			if (!rule.m_If)
				return null;
			if (rule.m_If.UsesGameModeState())
				rule.m_eWhen = GC_ERuleEvent.GAME_MODE_STATE;
		}
		return rule;
	}
}

[BaseContainerProps(), BaseContainerCustomTitleField("m_sName")]
class GC_Event : GC_EventDefinition
{
	[Attribute("Event", UIWidgets.EditBox, "A short name for this event.")]
	string m_sName;
	[Attribute("", UIWidgets.Object, "Every condition must pass. Game Mode State checks at startup and on state changes. At the top level, an empty list runs at match start. Inside a random group, an empty list adds no restriction.")]
	ref array<ref GC_MissionCondition> m_aConditions;
	[Attribute("", UIWidgets.Object, "Actions to run, in list order.")]
	ref array<ref GC_MissionAction> m_aActions;
	[Attribute("", UIWidgets.Object, "Optional repeat, delay, random chance and disable settings. Unset means once, no delay, 100% chance.")]
	ref GC_RuleOptions m_Advanced;

	//------------------------------------------------------------------------------------------------
	override GC_CompiledMissionRule BuildRule()
	{
		GC_CompiledMissionRule rule = BuildSettings(m_sName, m_aConditions, m_Advanced);
		if (!rule || !m_aActions || m_aActions.IsEmpty() || m_aActions.Count() > 256)
			return null;
		foreach (GC_MissionAction definition : m_aActions)
		{
			if (!definition)
				return null;
			GC_RuleAction action = definition.BuildAction();
			if (!action || !action.IsValid())
				return null;
			rule.m_aThen.Insert(action);
		}
		if (!rule.IsValid())
			return null;
		return rule;
	}
}

[BaseContainerProps(), BaseContainerCustomTitleField("m_sName")]
class GC_RandomEventGroup : GC_EventDefinition
{
	[Attribute("Random event group", UIWidgets.EditBox, "A short name for this random selection.")]
	string m_sName;
	[Attribute("", UIWidgets.Object, "Decides when to select events. Every condition must pass. Leave empty to select at match start. Child conditions filter the selection; they do not trigger the group themselves.")]
	ref array<ref GC_MissionCondition> m_aConditions;
	[Attribute("1", UIWidgets.SpinBox, "How many different events to pick from those whose conditions pass. No repeats in one selection. If fewer qualify, pick those available. Each selected event's own chance and delay still apply.", "1 255 1")]
	int m_iPickCount;
	[Attribute("", UIWidgets.Object, "Regular events to choose from. Disabled, already-completed once-only events, waiting events and events with failing conditions are excluded. Groups cannot be nested here; conditions can still nest normally.")]
	ref array<ref GC_Event> m_aEvents;
	[Attribute("", UIWidgets.Object, "Optional repeat, delay, chance and disable settings for the whole selection. Child events keep their own Advanced settings.")]
	ref GC_RuleOptions m_Advanced;

	//------------------------------------------------------------------------------------------------
	override GC_CompiledMissionRule BuildRule()
	{
		GC_CompiledMissionRule rule = BuildSettings(m_sName, m_aConditions, m_Advanced);
		if (!rule || !m_aEvents || m_aEvents.IsEmpty() || m_aEvents.Count() > 255 || m_iPickCount < 1 || m_iPickCount > m_aEvents.Count())
			return null;
		rule.m_iPickCount = m_iPickCount;
		rule.m_aChoices = {};
		foreach (GC_Event event : m_aEvents)
		{
			if (!event)
				return null;
			GC_CompiledMissionRule child = event.BuildRule();
			if (!child)
				return null;
			rule.m_aChoices.Insert(child);
		}
		if (!rule.IsValid())
			return null;
		return rule;
	}
}
''',encoding='utf-8')
p=r/'scripts/Game/GC_LobbyManager.c';s=p.read_text(encoding='utf-8').replace('array<ref GC_Event> m_aMissionEvents','array<ref GC_EventDefinition> m_aMissionEvents').replace('foreach (GC_Event definition : m_aMissionEvents)','foreach (GC_EventDefinition definition : m_aMissionEvents)');p.write_text(s,encoding='utf-8')
p=r/'scripts/Game/MissionFramework/GC_CompiledMissionRule.c';s=p.read_text(encoding='utf-8');a=s.index('class GC_CompiledMissionRule');s=s[:a]+s[a:].replace('\tref GC_RuleRandom m_Random;','\tref GC_RuleRandom m_Random;\n\tfloat m_fChance = 1;\n\tint m_iPickCount;\n\tref array<ref GC_CompiledMissionRule> m_aChoices;',1).replace('bool IsValid()','bool IsValid(int depth = 0)',1).replace('\t\tif (m_Random &&','\t\tif (depth > 1 || m_fChance < 0 || m_fChance > 1)\n\t\t\treturn false;\n\t\tif (m_Random &&',1)
needle='\t\tif (!m_aThen || m_aThen.IsEmpty() || m_aThen.Count() > 256)'
a=s.index(needle,s.index('class GC_CompiledMissionRule'));s=s[:a]+'''		if (m_aChoices)
		{
			if (depth > 0 || m_aChoices.IsEmpty() || m_aChoices.Count() > 255 || m_iPickCount < 1 || m_iPickCount > m_aChoices.Count() || (m_aThen && !m_aThen.IsEmpty()))
				return false;
			foreach (GC_CompiledMissionRule child : m_aChoices)
			{
				if (!child || !child.IsValid(depth + 1))
					return false;
			}
			return true;
		}
'''+s[a:];p.write_text(s,encoding='utf-8')
# Script-created settings do not receive editor Attribute defaults.
for p in (r/'tests/McpTestAddon/scripts').rglob('*.c'):
 s=p.read_text(encoding='utf-8-sig')
 if 'new GC_RuleOptions()' not in s:continue
 s=re.sub(r'(\t*)([\w.]+) = new GC_RuleOptions\(\);',lambda m:m[0]+'\n'+m[1]+m[2]+'.m_fRandomChance = 1;',s)
 p.write_text(s,encoding='utf-8')
