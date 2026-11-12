from pathlib import Path
root = Path(r'C:\Users\Gray\Documents\VS\GC-Website-2')
p = root/'tests/GC.Infrastructure.Tests/MissionMetadataChecks.cs'
s = p.read_text()
start=s.index('    public const string Generated =')
end=s.index('    static void Check',start)
s=s[:start]+'''    public const string Generated = """
        m_iSchemaVersion 1
        m_sWorld "{AAAAAAAAAAAAAAAA}worlds/Test.ent"
        m_sCaptureScope "world_subscene"
        m_iEntityCount 6
        m_aFactions { "US" "USSR" }
        m_aAssets {
         GC_MissionMetadataAsset {
          m_sCategory "tank"
          m_iCount 4
         }
        }
        m_aLimitations { "Placed content only" }
        m_aPrefabs { GC_MissionMetadataPrefab { m_sPrefab "not a player asset" } }
        """;
    static string Embed(string header, string generated) => header.TrimEnd()[..^1]+"\\n m_GC_GeneratedMetadata GC_GeneratedMissionMetadata {\\n"+generated+"\\n }\\n}";
    static CatalogMission Parse(string header=Header,string? extra=null,string? generated=null) => CatalogSync.Parse("Missions/Test.conf",generated is null?header:Embed(header,generated),"{1111111111111111}",extra,"test");
''' +s[end:]
lines=s.splitlines()
lines=[('        string Hours(string hours,bool allow=false)=>Generated+"\\nm_aStartHours { "+hours.Replace(\',\',\' \')+" }\\nm_bAllowHeaderTime "+(allow?"1":"0");' if 'string Hours(' in l else l) for l in lines]
s='\n'.join(lines)+'\n'
s=s.replace('Generated.TrimEnd()[..^1]+",\\\"randomStartTime\\\":true}"', 'Generated+"\\nm_bRandomStartTime 1"')
start=s.index('        var addonSnapshot=')
end=s.index('        Check(d.GetProperty("missionType")',start)
s=s[:start]+'''        var addonSnapshot=Generated+"\\n"+"""
            m_aLoadedAddons {
             GC_MissionMetadataAddon {
              m_sTitle "Used mod"
              m_bReferencedByPlacedPrefab 1
             }
             GC_MissionMetadataAddon { m_sTitle "Unused mod" }
             GC_MissionMetadataAddon {
              m_sTitle "Arma Reforger"
              m_bVanilla 1
              m_bReferencedByPlacedPrefab 1
             }
            }
            """;
        var detected=Parse(extra:"{\\"mods\\":[\\"Former tag\\"]}",generated:addonSnapshot);
        Check(detected.Metadata.GetProperty("mods").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(new[]{"Used mod"}) && detected.Tags.Contains("Used mod") && !detected.Tags.Contains("Former tag"),"Only referenced non-vanilla mods appear in mission details and search");
        var slots=Generated+"\\n"+"""
            m_aPlayerSlots {
             GC_MissionMetadataSlot "{1234567890123456}" {
              m_sGroup "Alpha"
              m_iIndex 0
              m_sFaction "US"
              m_sPrefab "Prefabs/Medic.et"
              m_sName "Medic"
              m_sLoadoutFaction "US"
             }
            }
            """;
        Check(Parse(generated:slots).Metadata.GetProperty("generated").GetProperty("playerSlots")[0].GetProperty("prefab").GetString()=="Prefabs/Medic.et","Native player slots and loadout references survive intake");
        Reject(()=>Parse(generated:slots.Replace("m_iIndex 0","m_iIndex zero")),"Malformed native slot index rejected");
        Reject(()=>Parse(generated:slots.Replace("GC_MissionMetadataSlot", "UnknownSlot")),"Unknown native slot class rejected");
        Check(Parse(generated:slots.Replace("m_iIndex 0", "")).Metadata.GetProperty("generated").GetProperty("playerSlots")[0].GetProperty("index").GetInt32()==0,"Omitted native default slot index is zero");
        Reject(()=>Parse(generated:Generated.Replace("m_iSchemaVersion 1", "m_iSchemaVersion 99")),"Unsupported native schema rejected");
        Reject(()=>Parse(generated:Generated.Replace("world_subscene", "unknown")),"Unknown native capture scope rejected");
        Reject(()=>Parse(generated:addonSnapshot.Replace("m_bVanilla 1", "m_bVanilla yes")),"Malformed native bool rejected");
        Check(Parse(generated:Generated.Replace("m_iSchemaVersion 1", "")).Metadata.GetProperty("generated").GetProperty("schemaVersion").GetInt32()==1,"Omitted attributed schema default is one");
        Check(!Parse().Metadata.TryGetProperty("generated",out _),"A header without generated data remains supported");
''' +s[end:]
s=s.replace('Generated.Replace("worlds/Test.ent","{BBBBBBBBBBBBBBBB}worlds/Test.ent")','Generated.Replace("AAAAAAAAAAAAAAAA","BBBBBBBBBBBBBBBB")')
s=s.replace('WithHeader(manifest,Header,Generated)', 'WithHeader(manifest,Embed(Header,Generated))')
p.write_text(s)
p=root/'tests/GC.Infrastructure.Tests/CatalogChecks.cs'
s=p.read_text().replace('World sidecar imported automatically alongside scenario header','Embedded native snapshot imported automatically from scenario header')
s=s.replace('(suffix==".conf.metadata.json"?Sidecar:"")','(suffix==".conf.metadata.json"?Sidecar:suffix==".conf"?GeneratedVersion.ToString():"")')
s='\n'.join(l for l in s.split('\n') if 'files.Add(new{path="World.ent.metadata.json"' not in l and 'if(path.EndsWith("/World.ent.metadata.json"))' not in l)
needle='    if(path.EndsWith(".json")&&Sidecar is not null)text=Sidecar;'
s=s.replace(needle,'''    if(path.EndsWith(".conf")&&GeneratedVersion>0)text=text.TrimEnd()[..^1]+$"\\n m_GC_GeneratedMetadata GC_GeneratedMissionMetadata {{\\n m_sWorld \\"{{AAAAAAAAAAAAAAAA}}World.ent\\"\\n m_sCaptureScope \\"world_subscene\\"\\n m_iEntityCount {GeneratedVersion}\\n }}\\n}}";
'''.replace('\\"','\\"')+needle)
p.write_text(s)
