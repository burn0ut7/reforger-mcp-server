from pathlib import Path
root = Path(r'C:\Users\Gray\Documents\VS\GC-Website-2')
p = root/'src/GC.Infrastructure/CatalogSync.cs'
s = p.read_text().replace('ParserVersion=11', 'ParserVersion=12')
s = s.replace('var content=await Read(raw+EscapePath(path),1024*1024,ct);', 'var content=await Read(raw+EscapePath(path),8*1024*1024,ct);')
s = '\n'.join(l for l in s.split('\n') if not any(x in l for x in ['var worldPath=', 'var generatedPath=', 'var generated=generatedPath']))
s = s.replace('+(generatedPath is null?"":files[generatedPath])','')
s = s.replace('return Parse(path,content,meta,extra,hash,generated);','return Parse(path,content,meta,extra,hash);')
s = s.replace('string hash,string? generated=null)', 'string hash)')
s = s.replace('if(generated is not null)MissionMetadata.ApplyGenerated(metadata,generated,world,header);', 'if(MissionMetadata.GeneratedFromHeader(header) is { } generated)MissionMetadata.ApplyGenerated(metadata,generated.ToJsonString(),world,header);')
p.write_text(s)
p = root/'src/GC.Infrastructure/MissionConfig.cs'
s = p.read_text().replace('text.Length > 1024 * 1024', 'text.Length > 8 * 1024 * 1024')
p.write_text(s)
p = root/'src/GC.Infrastructure/LocalMissionImport.cs'
s = p.read_text().replace('string header, string? generated = null)', 'string header)').replace('null,"local",generated)', 'null,"local")')
p.write_text(s)
p = root/'src/GC.Worker/CatalogJob.cs'
s = p.read_text()
s = '\n'.join(l for l in s.split('\n') if 'var snapshot = configuration[' not in l)
s = s.replace('await File.ReadAllTextAsync(header, token), await File.ReadAllTextAsync(snapshot, token)', 'await File.ReadAllTextAsync(header, token)')
p.write_text(s)
p = root/'Program.cs'
s = p.read_text()
start = s.index('        string? generated=null;')
end = s.index('\n    }',start)
s = s[:start] + '''        if(args.Contains("--mission-content"))throw new ArgumentException("Generated content now belongs in the mission header. Refresh it in Workbench and omit --mission-content.");
        manifest=LocalMissionImport.WithHeader(manifest,await File.ReadAllTextAsync(args[headerAt+1]));''' + s[end:]
p.write_text(s)
