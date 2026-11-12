from pathlib import Path
p = Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
labels = {
'NotConfigured':'Mission voting is not configured', 'InvalidConfig':'Mission voting configuration is invalid',
'Loading':'Loading mission choices…', 'Unavailable':'Mission catalog unavailable', 'InvalidResponse':'Mission catalog response is invalid',
'Limit':'Mission catalog exceeds voting limit', 'Choose':'Choose the next mission', 'Empty':'No installed missions are available for voting',
'EndedEmpty':'Voting ended — no mission available', 'LoadingWinner':'Winner selected — loading next mission…', 'LoadFailed':'The server could not load the winning mission',
'Continue':'Continue to debrief  >', 'Closed':'Voting closed', 'Rules':'Most votes wins. Ties are decided randomly. You can change your vote.',
'Selected':'Your vote', 'Winner':'Winner', 'Title':'Next mission', 'NoPreview':'Preview unavailable',
'Clock':'Voting closes in %1:%2', 'Votes':'%1 votes', 'OneVote':'1 vote', 'History':'Played %1 times · %2', 'Never':'Never played'
}
for rel in ['scripts/Game/Lobby/GC_MissionVote.c','scripts/Game/UI/GC_Lobby_VotingMenu.c','UI/layouts/Lobby_Voting.layout','UI/layouts/Lobby_VoteCard.layout']:
 f=p/rel; s=f.read_text(encoding='utf-8')
 for key,value in labels.items():
  if key in ('Clock','Votes','History','Never'): continue
  s=s.replace('"'+value+'"','"#GC-Vote_'+key+'"')
 s=s.replace('string.Format("Voting closes in %1:%2",', 'string.Format(WidgetManager.Translate("#GC-Vote_Clock"),')
 s=s.replace('m_Controller.m_aGC_VoteCounts[i].ToString() + " votes"','string.Format(WidgetManager.Translate("#GC-Vote_Votes"), m_Controller.m_aGC_VoteCounts[i])')
 s=s.replace('"Played " + plays + " times · " + lastPlayed','string.Format(WidgetManager.Translate("#GC-Vote_History"), plays, lastPlayed)')
 s=s.replace('json.ReadValue("lastPlayed", lastPlayed);','json.ReadValue("lastPlayed", lastPlayed);\n\t\tif (lastPlayed == "Never played")\n\t\t\tlastPlayed = WidgetManager.Translate("#GC-Vote_Never");')
 f.write_text(s,encoding='utf-8',newline='\n')
f=p/'Language/GC_Catalogue.st';s=f.read_text(encoding='utf-8'); pos=s.rfind(' }')
items=''.join('  CustomStringTableItem "{E37A21004D9510%02X}" { Id "GC-Vote_%s" Target_en_us "%s" }\n'%(i,k,v) for i,(k,v) in enumerate(labels.items()))
f.write_text(s[:pos]+items+s[pos:],encoding='utf-8',newline='\n')
f=p/'Language/GC_Catalogue.en_us.conf';s=f.read_text(encoding='utf-8');pos=s.index(' }')
s=s[:pos]+''.join('  "GC-Vote_'+k+'"\n' for k in labels)+s[pos:];pos=s.rfind(' }')
s=s[:pos]+''.join('  "'+v+'"\n' for v in labels.values())+s[pos:]
f.write_text(s,encoding='utf-8',newline='\n')
