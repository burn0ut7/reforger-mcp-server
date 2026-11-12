from pathlib import Path
p=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2/tests/McpTestAddon/scripts/Game/GC_GameModeConditionTests.c');s=p.read_text(encoding='utf-8-sig');s=s.replace('''		UpdateMissionGameState(next);
		if (!GetFlag(flag) || GetRunCount() != expected)
			return string.Format("FAIL: %1 flag=%2 runs=%3 expected=%4", flag, GetFlag(flag), GetRunCount(), expected);
		return string.Format''','''		if (!GetFlag(flag) || GetRunCount() != expected)
			return string.Format("FAIL: native callback %1 flag=%2 runs=%3 expected=%4", flag, GetFlag(flag), GetRunCount(), expected);
		UpdateMissionGameState(next);
		if (GetRunCount() != expected)
			return "FAIL: duplicate callback ran another action";
		return string.Format''');p.write_text(s,encoding='utf-8')
