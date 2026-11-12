from pathlib import Path
import json
p=Path('work/mission-rules-cleanup/package.py');s=p.read_text().replace('a fixed match-start casualty ratio. General timer', 'a fixed match-start casualty ratio. Total Deaths uses the same filters with count comparisons and includes respawned lives. General timer');p.write_text(s)
p=Path('configuration.json');current=json.loads(p.read_text(encoding='utf-8-sig'));backup=Path('work/plain-logic/config-before-casualties.json');original=json.loads(backup.read_text(encoding='utf-8-sig'))
for key in ('processId','processStartedUtc'):
 if key in original['workbench']: current['workbench'][key]=original['workbench'][key]
 else: current['workbench'].pop(key,None)
if current==original: p.write_bytes(backup.read_bytes())
else: p.write_text(json.dumps(current,indent=2))
print('Restored saved MCP configuration; active session remains attached to Lobby2. Both Workbench instances remain open.')
