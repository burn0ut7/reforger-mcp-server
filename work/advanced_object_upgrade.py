from pathlib import Path
import re

root = Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
def read(p):
    return p.read_text(encoding='utf-8-sig')
def write(p, s):
    p.write_text(s, encoding='utf-8', newline='\r\n')

p = root/'scripts/Game/MissionFramework/GC_Event.c'
s = read(p)
pattern = r'\t\[Attribute\("0", UIWidgets.CheckBox, "Checked: run again.*?\tbool m_bDisabled;\n'
blocks = re.findall(pattern, s, re.S)
assert len(blocks) == 2 and blocks[0] == blocks[1]
options = '[BaseContainerProps(), BaseContainerCustomStringTitleField("Advanced")]\nclass GC_EventOptions\n{\n' + blocks[0].replace(', category: "Advanced"','') + '}\n'
write(root/'scripts/Game/MissionFramework/GC_EventOptions.c', options)
s = re.sub(pattern, '\t[Attribute("", UIWidgets.Object, "Optional advanced settings. Leave empty to run once, with no delay, 100% chance and enabled. Expand this object only when you need to change those defaults.")]\n\tref GC_EventOptions m_Advanced;\n', s, flags=re.S)
s = s.replace('bool repeatable, float delay, float chance, bool disabled)', 'GC_EventOptions options)')
anchor = '\t\tGC_CompiledMissionRule rule = new GC_CompiledMissionRule();'
s = s.replace(anchor, '''\t\tbool repeatable;
\t\tfloat delay;
\t\tfloat chance = 1;
\t\tbool disabled;
\t\tif (options)
\t\t{
\t\t\trepeatable = options.m_bRepeatable;
\t\t\tdelay = options.m_fDelay;
\t\t\tchance = options.m_fRandomChance;
\t\t\tdisabled = options.m_bDisabled;
\t\t}
''' + anchor)
s = s.replace('m_bRepeatable, m_fDelay, m_fRandomChance, m_bDisabled)', 'm_Advanced)')
write(p,s)

p = root/'scripts/Game/MissionFramework/GC_MissionFlag.c'
s = read(p)
pattern = r'\t\[Attribute\("0", UIWidgets.SpinBox, "Start with this remembered.*?\tbool m_bRepeatable;\n'
blocks = re.findall(pattern,s,re.S)
assert len(blocks) == 1
options = '[BaseContainerProps(), BaseContainerCustomStringTitleField("Advanced")]\nclass GC_FlagOptions\n{\n' + blocks[0].replace(', category: "Advanced"','') + '}\n'
write(root/'scripts/Game/MissionFramework/GC_FlagOptions.c', options)
s = re.sub(pattern, '\t[Attribute("", UIWidgets.Object, "Optional advanced settings. Leave empty to start at zero calls (false) and accept one successful call. Expand to set a starting count or allow repeated calls.")]\n\tref GC_FlagOptions m_Advanced;\n', s, flags=re.S)
at = s.index('\t//---')
s = s[:at] + '''\t//------------------------------------------------------------------------------------------------
\tint GetStartCallCount()
\t{
\t\tif (!m_Advanced)
\t\t\treturn 0;
\t\treturn m_Advanced.m_iStartCallCount;
\t}

\t//------------------------------------------------------------------------------------------------
\tbool IsRepeatable()
\t{
\t\treturn m_Advanced && m_Advanced.m_bRepeatable;
\t}

''' + s[at:]
write(p,s)
p = root/'scripts/Game/MissionFramework/GC_MissionRuleEngine.c'
s = read(p).replace('flag.m_iStartCallCount','flag.GetStartCallCount()').replace('flag.m_bRepeatable','flag.IsRepeatable()')
write(p,s)

# Explicit optional fixtures: retain their existing authored values inside the new object.
for p in (root/'tests/McpTestAddon/scripts/Game').glob('*.c'):
    old = read(p)
    s = old
    fields = r'm_iStartCallCount|m_bRepeatable|m_fRandomChance|m_fDelay|m_bDisabled'
    # These fixture fields belong to authored flags/events, not compiled engine state.
    s = re.sub(r'\.('+fields+r')\b', r'.m_Advanced.\1', s)
    s = re.sub(r'(\t*)(GC_Event|GC_RandomEventGroup|GC_MissionFlag) (\w+) = new \2\(\);',
        lambda m: m[0]+'\n'+m[1]+m[3]+'.m_Advanced = new '+('GC_FlagOptions' if m[2]=='GC_MissionFlag' else 'GC_EventOptions')+'();'+
        ('' if m[2]=='GC_MissionFlag' else '\n'+m[1]+m[3]+'.m_Advanced.m_fRandomChance = 1;'), s)
    # Resource-loaded flags can legitimately omit the optional object.
    s = s.replace('flags[0].m_Advanced.m_bRepeatable && !flags[1].m_Advanced.m_bRepeatable', 'flags[0].IsRepeatable() && !flags[1].IsRepeatable()')
    if s != old:
        write(p,s)
        print('fixture',p.relative_to(root))

# Migrate only direct properties of these exact resource containers; nested conditions/actions untouched.
types = {'GC_Event': ('GC_EventOptions', {'m_bRepeatable','m_fDelay','m_fRandomChance','m_bDisabled'}),
         'GC_RandomEventGroup': ('GC_EventOptions', {'m_bRepeatable','m_fDelay','m_fRandomChance','m_bDisabled'}),
         'GC_MissionFlag': ('GC_FlagOptions', {'m_iStartCallCount','m_bRepeatable'})}
count = 0
for p in root.rglob('*'):
    if p.suffix.lower() not in {'.et','.ent','.layer','.conf'} or '.git' in p.parts:
        continue
    data = p.read_bytes()
    text = data.decode('utf-8-sig')
    lines = text.splitlines(keepends=True)
    stack, edits = [], []
    for i,line in enumerate(lines):
        clean = re.sub(r'"(?:\\.|[^"\\])*"', '""', line).split('//')[0]
        if stack and stack[-1][0] in types:
            kind, start, fields = stack[-1]
            match = re.match(r'\s*(\w+)\s+([-+\d.eE]+)\s*$', clean)
            if match and match[1] in types[kind][1]:
                fields.append(i)
        # Text resources use one opening/closing block per line. Refuse ambiguous targeted resources.
        for token in re.finditer(r'[{}]',clean):
            if token[0] == '{':
                match = re.match(r'\s*(GC_Event|GC_RandomEventGroup|GC_MissionFlag)\b',clean)
                stack.append((match[1] if match else '',i,[]))
            else:
                if not stack:
                    raise ValueError(f'Unbalanced resource {p}')
                kind,start,fields = stack.pop()
                if kind in types and fields:
                    indent = re.match(r'\s*',lines[fields[0]])[0]
                    ending = '\r\n' if '\r\n' in text else '\n'
                    block = indent+'m_Advanced '+types[kind][0]+' {'+ending
                    block += ''.join(' '+lines[n] for n in fields)
                    block += indent+'}'+ending
                    edits.append((i,block,fields))
    if edits:
        removed = {n for _,_,fields in edits for n in fields}
        inserted = {i:block for i,block,_ in edits}
        updated = ''.join(inserted.get(i,'') + (line if i not in removed else '') for i,line in enumerate(lines))
        p.write_bytes((b'\xef\xbb\xbf' if data.startswith(b'\xef\xbb\xbf') else b'') + updated.encode('utf-8'))
        count += len(edits)
        print('resource',p.relative_to(root),len(edits))
print('migrated containers',count)
