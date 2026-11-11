//! Explicit checks using isolated data; never changes the live mission or player state.
class GC_FrameworkSimplificationTests
{
	protected int m_iChecks;
	protected int m_iFailures;

	protected void Check(bool passed, string label)
	{
		m_iChecks++;
		if (passed)
			return;
		m_iFailures++;
		Print("GC | Simplification check FAILED | " + label, LogLevel.ERROR);
	}

	string Run()
	{
		CheckConditions();
		CheckParameters();
		return string.Format("checks=%1 failures=%2", m_iChecks, m_iFailures);
	}

	protected void CheckConditions()
	{
		GC_RuleCondition group = new GC_RuleCondition();
		group.m_aChecks = {};
		for (int kind = -1; kind <= 15; kind++)
		{
			GC_RuleCondition leaf = new GC_RuleCondition();
			leaf.m_iCheck = kind;
			group.m_aChecks.Clear();
			group.m_aChecks.Insert(null);
			group.m_aChecks.Insert(leaf);
			group.m_aChecks.Insert(leaf);
			array<GC_RuleCondition> selected = {leaf};
			array<GC_RuleCondition> empty = {};
			Check(group.UsesArea() == (kind == 10), "area kind " + kind);
			Check(group.UsesAIGroup() == (kind == 14), "AI kind " + kind);
			Check(group.UsesScript() == (kind == 13), "script kind " + kind);
			Check(group.UsesInteraction() == (kind == 11), "interaction kind " + kind);
			Check(group.UsesEntityDamage() == (kind == 9), "damage kind " + kind);
			Check(group.UsesRandom() == (kind == 6 || kind == 12), "random kind " + kind);
			Check(group.UsesCasualties() == (kind == 7 || kind == 8), "casualty kind " + kind);
			Check(group.UsesGameModeState() == (kind == 3 || kind == 12), "phase kind " + kind);
			Check(!group.UsesArea(empty) && !group.UsesAIGroup(empty) && !group.UsesScript(empty)
				&& !group.UsesInteraction(empty) && !group.UsesEntityDamage(empty) && !group.UsesRandom(empty), "empty change set");
			Check(group.UsesArea(selected) == group.UsesArea() && group.UsesRandom(selected) == group.UsesRandom(), "selected change set");
			array<GC_RuleCondition> found = {};
			group.CollectAreaConditions(found);
			group.CollectAIGroupConditions(found);
			group.CollectScriptConditions(found);
			group.CollectInteractionConditions(found);
			group.CollectEntityDamageConditions(found);
			group.CollectRandomConditions(found);
			int expected;
			if (kind == 6 || (kind >= 9 && kind <= 14))
				expected = 1;
			Check(found.Count() == expected, "unique collection kind " + kind);
			if (expected == 1)
				Check(found[0] == leaf, "collection identity");
		}
		GC_RuleCondition area = new GC_RuleCondition();
		area.m_iCheck = 10;
		group.m_aChecks = {area};
		Check(group.UsesArea(null, 15) && !group.UsesArea(null, 16), "recursion boundary");
		Check(area.UsesArea(null, 16) && !area.UsesArea(null, 17), "leaf depth boundary");
		array<GC_RuleCondition> atLimit = {};
		group.CollectAreaConditions(atLimit, 16);
		Check(atLimit.IsEmpty(), "collection depth boundary");
		group.CollectAreaConditions(atLimit, 15);
		Check(atLimit.Count() == 1 && atLimit[0] == area, "collection at allowed depth");
		group.m_iCheck = 2;
		Check(!group.UsesArea(), "non-group does not traverse attached children");
		array<GC_RuleCondition> ignored = {};
		group.CollectAreaConditions(ignored);
		Check(ignored.IsEmpty(), "non-group collection");
		GC_OverrideConditionTest custom = new GC_OverrideConditionTest();
		group.m_iCheck = 0;
		group.m_aChecks = {custom};
		Check(group.UsesArea(), "nested public override");
		group.CollectAreaConditions(ignored);
		Check(ignored.Count() == 1 && ignored[0] == custom, "nested collection override");
	}

	protected void CheckParameters()
	{
		GC_LobbyParameterCatalog catalog = new GC_LobbyParameterCatalog();
		catalog.GetDefinitions().Insert(new GC_LobbyBoolParameterDefinition());
		catalog.GetDefinitions().Insert(new GC_LobbyBoolParameterDefinition());
		GC_LobbyParameterValue first = new GC_LobbyParameterValue();
		GC_LobbyParameterValue second = new GC_LobbyParameterValue();
		first.SetBool(0, true);
		second.SetBool(1, false);
		array<ref GC_LobbyParameterValue> input = {second, first};
		array<ref GC_LobbyParameterValue> storage = catalog.GetValues();
		Check(catalog.ApplyValues(input), "unordered parameter batch");
		Check(catalog.GetRevision() == 1 && storage == catalog.GetValues(), "revision and storage identity");
		Check(storage.Count() == 2 && storage[0].GetDefinitionIndex() == 0 && storage[1].GetDefinitionIndex() == 1, "canonical order");
		Check(storage[0] != first && storage[1] != second && storage[0].GetBoolValue() && !storage[1].GetBoolValue(), "independent copies");
		first.SetBool(0, false);
		Check(storage[0].GetBoolValue(), "caller mutation isolated");
		Check(catalog.ApplyValues(storage) && catalog.GetRevision() == 2 && storage[0].GetBoolValue(), "self-aliased apply");
		Check(catalog.ApplySnapshot(5, storage) && catalog.GetRevision() == 5 && storage[0].GetBoolValue(), "self-aliased snapshot");
		Check(!catalog.ApplySnapshot(4, input) && catalog.GetRevision() == 5 && storage[0].GetBoolValue(), "stale snapshot preserves state");
		Check(catalog.ApplySnapshot(6, input) && !storage[0].GetBoolValue() && catalog.GetRevision() == 6, "new snapshot");
		array<ref GC_LobbyParameterValue> duplicate = {first, first};
		Check(!catalog.ApplyValues(duplicate) && catalog.GetRevision() == 6 && storage.Count() == 2, "duplicate rejected atomically");
		array<ref GC_LobbyParameterValue> missing = {first};
		Check(!catalog.ApplyValues(missing) && catalog.GetRevision() == 6, "missing parameter rejected");
		array<ref GC_LobbyParameterValue> nullValue = {first, null};
		Check(!catalog.ApplySnapshot(7, nullValue) && catalog.GetRevision() == 6, "null parameter rejected");
	}
}

//! A custom condition must retain its public behavior when nested in a group.
class GC_OverrideConditionTest : GC_RuleCondition
{
	override bool UsesArea(array<GC_RuleCondition> changed = null, int depth = 0)
	{
		return true;
	}

	override void CollectAreaConditions(array<GC_RuleCondition> result, int depth = 0)
	{
		result.Insert(this);
	}
}

modded class GC_LobbyManager
{
	string GC_TestFrameworkSimplification()
	{
		GC_FrameworkSimplificationTests checks = new GC_FrameworkSimplificationTests();
		return checks.Run();
	}
}
