from pathlib import Path
import re
p=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2/worlds/examples/arland/Arland_Lobby_Layers/default.layer')
b=p.read_bytes();Path('work/plain-logic/arland-before-combined-count.layer').write_bytes(b)
s=b.decode();pattern=r'GC_FlagCallCountCondition ("\{[^}]+\}") \{([^{}]*)\}'
def convert(m):
 body=m[2]; indent=re.search(r'\n([ \t]*)\}',m[0])[1]+' '
 body=body.replace('m_iCount ', 'm_iRequiredCount ')
 fields=f'\n{indent}m_iCountFrom 1'
 if 'm_iComparison' not in body: fields+=f'\n{indent}m_iComparison 1'
 if 'm_iRequiredCount' not in body: fields+=f'\n{indent}m_iRequiredCount 5'
 return 'GC_ConditionCount '+m[1]+' {'+fields+body+'}'
s,n=re.subn(pattern,convert,s);assert n==2;nbytes=s.encode();p.write_bytes(nbytes);print('Migrated 2 saved entries; original saved layer backed up.')
