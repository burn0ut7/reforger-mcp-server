from pathlib import Path
import re
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2/scripts')
for p in r.rglob('*.c'):
 lines=p.read_text(encoding='utf-8-sig').splitlines()
 for i,line in enumerate(lines):
  if re.search(r'\bfloat\s+\w+\s*;',line) and i and '[Attribute(' in lines[i-1]:
   print(f'{p.relative_to(r)}:{i+1} {line.strip()} {lines[i-1].strip()}')
