[BaseContainerProps(), BaseContainerCustomStringTitleField("Condition")]
class GC_MissionCondition
{
	[Attribute("0", UIWidgets.ComboBox, "Choose a flag check, or combine the conditions below. All requires every condition; Any requires one; None requires none.", enums: {ParamEnum("Flag is On", "0"), ParamEnum("Flag is Off", "1"), ParamEnum("All conditions", "2"), ParamEnum("Any condition", "3"), ParamEnum("No conditions", "4"), ParamEnum("Exactly this many", "5"), ParamEnum("At least this many", "6"), ParamEnum("At most this many", "7"), ParamEnum("All conditions agree", "8")})]
	int m_iType;
	[Attribute("", UIWidgets.EditBox, "Used by Flag is On or Flag is Off. Leave empty for a combination of conditions. A flag that has not been set is Off.")]
	string m_sFlagName;
	[Attribute("", UIWidgets.Object, "Used by condition combinations. Add conditions here; each may contain another combination. Flag checks ignore this list.")]
	ref array<ref GC_MissionCondition> m_aConditions;
	[Attribute("1", UIWidgets.SpinBox, "Only used by Exactly, At least and At most. How many conditions must pass.", "0 256 1", category: "Advanced")]
	int m_iNumber;

	//------------------------------------------------------------------------------------------------
	GC_RuleCondition BuildCheck(int depth = 0)
	{
		if (depth > 16 || m_iType < 0 || m_iType > 8)
			return null;
		if (m_iType >= 2)
			return BuildGroup(m_aConditions, m_iType - 2, m_iNumber, depth);
		if (m_sFlagName.IsEmpty())
			return null;
		GC_RuleCondition check = new GC_RuleCondition();
		check.m_iCheck = 1;
		check.m_sFlagName = m_sFlagName;
		check.m_bReverseAnswer = m_iType == 1;
		return check;
	}

	//------------------------------------------------------------------------------------------------
	static GC_RuleCondition BuildGroup(array<ref GC_MissionCondition> items, GC_ERuleCheck kind, int number, int depth)
	{
		if (depth > 16 || (items && items.Count() > 256))
			return null;
		GC_RuleCondition group = new GC_RuleCondition();
		group.m_eGroupNeeds = kind;
		group.m_iHowMany = number;
		group.m_aChecks = {};
		if (items)
		{
			foreach (GC_MissionCondition item : items)
			{
				if (!item)
					return null;
				GC_RuleCondition check = item.BuildCheck(depth + 1);
				if (!check)
					return null;
				group.m_aChecks.Insert(check);
			}
		}
		if (!group.IsValid(depth))
			return null;
		return group;
	}
}
