//! Current authoring catalog: related operations share one action. Older action types require migration.
[BaseContainerProps(), BaseContainerCustomStringTitleField("Flag Action")]
class GC_FlagAction : GC_ActionBase
{
	[Attribute("", UIWidgets.EditComboBox, "Exact name in Mission Flags.", enums: {ParamEnum("", "", "rst-choices:1:GC_MissionFlagChoices")})]
	string m_sFlagName;
	[Attribute("0", UIWidgets.ComboBox, "Call checks the flag's conditions and repeatability. All other operations edit its remembered count directly. Zero means false; a positive count means true. Reset does not reset mission events.", enums: {ParamEnum("Call", "0"), ParamEnum("Set count", "1"), ParamEnum("Add", "2"), ParamEnum("Subtract", "3"), ParamEnum("Reset to zero", "4"), ParamEnum("Restore starting count", "5")})]
	int m_iOperation;
	[Attribute("1", UIWidgets.SpinBox, "Amount for Set count, Add or Subtract only. Call always adds one when accepted. Subtract stops at zero. Reset and Restore use no amount.", "0 1000000 1")]
	int m_iAmount;

	//------------------------------------------------------------------------------------------------
	override GC_RuleAction BuildAction()
	{
		GC_RuleAction action = new GC_RuleAction();
		action.m_eDo = GC_ERuleAction.FLAG_CONTROL;
		action.m_sTextOrName = m_sFlagName;
		action.m_iOperation = m_iOperation;
		action.m_iAmount = m_iAmount;
		return action;
	}
}

[BaseContainerProps(), BaseContainerCustomStringTitleField("Event Action")]
class GC_EventControlAction : GC_ActionBase
{
	[Attribute("", UIWidgets.EditComboBox, "Name of the mission event or random event group to control. All event operations use this same name. A child event must still be selected by its group; Check now cannot run a child directly.", enums: {ParamEnum("", "", "rst-choices:1:GC_MissionEventChoices")})]
	string m_sEventName;
	[Attribute("0", UIWidgets.ComboBox, "Enable: allow future checks. Disable: stop future checks and cancel its pending delay. Reset: forget that it ran and cancel its delay; this does not enable it or run it. Check now: try to run using its normal conditions, chance and delay. Resetting a group does not reset its children. These operations do not change flags or random-condition results.", enums: {ParamEnum("Enable", "0"), ParamEnum("Disable", "1"), ParamEnum("Reset", "2"), ParamEnum("Check now", "3")})]
	int m_iOperation;

	//------------------------------------------------------------------------------------------------
	override GC_RuleAction BuildAction()
	{
		GC_RuleAction action = new GC_RuleAction();
		action.m_eDo = GC_ERuleAction.EVENT_CONTROL;
		action.m_sTextOrName = m_sEventName;
		action.m_iOperation = m_iOperation;
		return action;
	}
}

[BaseContainerProps(), BaseContainerCustomStringTitleField("Message Action")]
class GC_MessageAction : GC_ActionBase
{
	[Attribute("", UIWidgets.EditBox, "Heading shown above the message. Maximum 80 characters. Leave empty to use Mission, or Mission Warning when Warning is checked.")]
	string m_sTitle;
	[Attribute("", UIWidgets.EditBox, "Message shown to connected players using the same notification stack as admin messages. Maximum 512 characters. Messages wait their turn when the stack is full; joining later does not replay them.")]
	string m_sMessage;
	[Attribute("", UIWidgets.EditComboBox, "Faction keys receiving the message. Empty means everyone, including spectators. Multiple matching factions never send a player duplicate messages.", enums: {ParamEnum("", "", "rst-choices:1:GC_FactionChoices")})]
	ref array<string> m_aFactions;
	[Attribute("0", UIWidgets.CheckBox, "Checked: use the warning icon, color and sound. Your Title stays the same. With no Title, the heading is Mission Warning instead of Mission.")]
	bool m_bWarning;

	//------------------------------------------------------------------------------------------------
	override GC_RuleAction BuildAction()
	{
		GC_RuleAction action = new GC_RuleAction();
		action.m_eDo = GC_ERuleAction.MISSION_MESSAGE;
		action.m_sMessageTitle = m_sTitle;
		action.m_sMessageTitle.Replace("\r", " ");
		action.m_sMessageTitle.Replace("\n", " ");
		action.m_sMessageTitle.Replace("\t", " ");
		action.m_sMessageTitle.TrimInPlace();
		action.m_sTextOrName = m_sMessage;
		action.m_sTextOrName.TrimInPlace();
		action.m_bFlagAnswer = m_bWarning;
		action.m_aFlagNames = {};
		if (m_aFactions)
			action.m_aFlagNames.Copy(m_aFactions);
		return action;
	}
}

[BaseContainerProps(), BaseContainerCustomStringTitleField("Reset Random Chance Action")]
class GC_ResetRandomChanceAction : GC_ActionBase
{
	[Attribute("", UIWidgets.EditComboBox, "Unique Reset Name on a Random Chance Condition. Clears its answer to false and waits its full Check Interval before rolling again. Also allows Roll Once to roll again. Does not reset flags, events or random groups.", enums: {ParamEnum("", "", "rst-choices:1:GC_RandomResetChoices")})]
	string m_sResetName;

	//------------------------------------------------------------------------------------------------
	override GC_RuleAction BuildAction()
	{
		GC_RuleAction action = new GC_RuleAction();
		action.m_eDo = GC_ERuleAction.RESET_RANDOM;
		action.m_sTextOrName = m_sResetName;
		return action;
	}
}

[BaseContainerProps(), BaseContainerCustomStringTitleField("End Mission Action")]
class GC_EndMissionAction : GC_ActionBase
{
	[Attribute("0", UIWidgets.ComboBox, "Ends the active Match and opens the after-action phase. Has no effect before Match or after the mission has ended.", enums: {ParamEnum("Mission complete", "0"), ParamEnum("Faction victory", "1"), ParamEnum("Faction defeat", "2"), ParamEnum("Faction draw", "3")})]
	int m_iResult;
	[Attribute("", UIWidgets.EditComboBox, "Faction key for a faction result. Leave empty for Mission complete.", enums: {ParamEnum("", "", "rst-choices:1:GC_FactionChoices")})]
	string m_sFaction;

	//------------------------------------------------------------------------------------------------
	override GC_RuleAction BuildAction()
	{
		GC_RuleAction action = new GC_RuleAction();
		action.m_eDo = GC_ERuleAction.END_MISSION;
		action.m_iOperation = m_iResult;
		action.m_sTextOrName = m_sFaction;
		return action;
	}
}

[BaseContainerProps(), BaseContainerCustomStringTitleField("Respawn Tickets Action")]
class GC_RespawnTicketsAction : GC_ActionBase
{
	[Attribute("", UIWidgets.EditComboBox, "Faction key whose remaining respawn tickets will change. This action does not respawn anyone.", enums: {ParamEnum("", "", "rst-choices:1:GC_FactionChoices")})]
	string m_sFaction;
	[Attribute("0", UIWidgets.ComboBox, "Set replaces the remaining tickets. Add increases them. Subtract removes tickets, stopping at zero.", enums: {ParamEnum("Set", "0"), ParamEnum("Add", "1"), ParamEnum("Subtract", "2")})]
	int m_iOperation;
	[Attribute("1", UIWidgets.SpinBox, "Number of tickets to set, add or subtract.", "0 1000000 1")]
	int m_iAmount;

	//------------------------------------------------------------------------------------------------
	override GC_RuleAction BuildAction()
	{
		GC_RuleAction action = new GC_RuleAction();
		action.m_eDo = GC_ERuleAction.RESPAWN_TICKETS;
		action.m_sTextOrName = m_sFaction;
		action.m_iOperation = m_iOperation;
		action.m_iAmount = m_iAmount;
		return action;
	}
}
