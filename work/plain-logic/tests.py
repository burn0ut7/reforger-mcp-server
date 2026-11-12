from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'tests/McpTestAddon/scripts/Game/GC_StandardLogicTests.c'
s=p.read_text();s=s.replace('\t\tGC_XorConditions xorCondition = new GC_XorConditions();\n','').replace('\t\tGC_XnorConditions xnorCondition = new GC_XnorConditions();\n','').replace(', xorCondition, xnorCondition','').replace('\t\txorCondition.m_aConditions = children;\n','').replace('\t\txnorCondition.m_aConditions = children;\n','').replace('including parity cases with three inputs','with up to three inputs').replace('{2, 2, 1, 1, 2, 1}','{2, 2, 1, 1}').replace('{8, 14, 1, 7, 6, 9}','{8, 14, 1, 7}').replace('{128, 254, 1, 127, 150, 105}','{128, 254, 1, 127}')
a=s.index('\t\tarray<float> firstNumbers');b=s.index('\t\treturn string.Format',a)
s=s[:a]+'''\t\tarray<ref GC_MissionCondition> counted = {};
'''+''.join(f'\t\t{x}.m_aConditions = counted;\n' for x in ['equal','different','less','lessEqual','greater','greaterEqual'])+'''		for (int passed; passed <= 3; passed++)
		{
			counted.Clear();
			for (int child; child < 3; child++)
			{
				GC_BooleanCondition leaf = new GC_BooleanCondition();
				leaf.m_bValue = child < passed;
				counted.Insert(leaf);
			}
			for (int required; required <= 4; required++)
			{
'''+''.join(f'\t\t\t\t{x}.m_iRequiredCount = required;\n' for x in ['equal','different','less','lessEqual','greater','greaterEqual'])+'''				array<bool> answers = {passed == required, passed != required, passed < required, passed <= required, passed > required, passed >= required};
				foreach (int index, GC_MissionCondition comparison : comparisons)
				{
					GC_RuleCondition built = comparison.BuildCheck();
					Check(built && built.Evaluate(null) == answers[index], "count comparison boundary");
				}
			}
		}
		counted.Clear();
		foreach (GC_MissionCondition comparison : comparisons)
			Check(!comparison.BuildCheck(), "empty count group fails closed");
		counted.Insert(null);
		foreach (GC_MissionCondition comparison : comparisons)
			Check(!comparison.BuildCheck(), "missing count child fails closed");
		GC_BooleanCondition a = new GC_BooleanCondition();
		GC_BooleanCondition b = new GC_BooleanCondition();
		GC_BooleanCondition c = new GC_BooleanCondition();
		GC_BooleanCondition d = new GC_BooleanCondition();
		GC_AllConditions left = new GC_AllConditions();
		left.m_aConditions = {a, b};
		GC_AllConditions right = new GC_AllConditions();
		right.m_aConditions = {c, d};
		GC_AnyCondition either = new GC_AnyCondition();
		either.m_aConditions = {left, right};
		GC_NoneConditions inverse = new GC_NoneConditions();
		inverse.m_aConditions = {left};
		greaterEqual.m_iRequiredCount = 2;
		greaterEqual.m_aConditions = {left, right, inverse};
		for (int mask; mask < 16; mask++)
		{
			a.m_bValue = (mask & 1) != 0;
			b.m_bValue = (mask & 2) != 0;
			c.m_bValue = (mask & 4) != 0;
			d.m_bValue = (mask & 8) != 0;
			Check(either.BuildCheck().Evaluate(null) == ((63624 & (1 << mask)) != 0), "OR of two AND groups");
			Check(inverse.BuildCheck().Evaluate(null) == ((30583 & (1 << mask)) != 0), "None reverses an entire group");
			// Left and its inverse contribute exactly one; right decides whether two pass.
			Check(greaterEqual.BuildCheck().Evaluate(null) == ((61440 & (1 << mask)) != 0), "two of three nested groups count each group once");
		}
		GC_MissionRule missionEvent = SimpleRule("logic_passed");
		missionEvent.m_aConditions = {greaterEqual};
		GC_MissionRuleEngine engine = new GC_MissionRuleEngine();
		engine.Start({missionEvent.BuildRule()});
		Check(engine.GetFlag("logic_passed"), "nested count reaches production action execution");
		c.m_bValue = false;
		engine.Start({missionEvent.BuildRule()});
		Check(!engine.GetFlag("logic_passed"), "false nested count blocks production action execution");
		engine.Stop();
		greaterEqual.m_iRequiredCount = -1;
		Check(!greaterEqual.BuildCheck(), "negative required count rejected");
		greaterEqual.m_iRequiredCount = 257;
		Check(!greaterEqual.BuildCheck(), "oversized required count rejected");
		for (int removed = 8; removed <= 9; removed++)
		{
			GC_RuleCondition obsolete = new GC_RuleCondition();
			obsolete.m_eGroupNeeds = removed;
			obsolete.m_bReverseAnswer = true;
			Check(!obsolete.IsValid() && !obsolete.Evaluate(null), "removed operator cannot be reinterpreted or inverted");
		}
'''+s[b:];p.write_text(s)
