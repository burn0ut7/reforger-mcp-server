from pathlib import Path
import re
p=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2/tests/McpTestAddon/scripts/Game/GC_RandomConditionTests.c')
s=p.read_text();s=re.sub(r'"[^"\n]*"', lambda m:m[0].replace('authoredEvent','event'),s);s=re.sub(r'//[^\n]*',lambda m:m[0].replace('authoredEvent','event'),s);p.write_text(s)
