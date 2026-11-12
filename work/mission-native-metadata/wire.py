from pathlib import Path
addon = Path(r'C:\Users\Gray\Documents\My Games\ArmaReforgerWorkbench\addons\Lobby2')
p = addon / 'scripts/WorkbenchGame/GC_CreateMissionHeaderPlugin.c'
s = p.read_text()
s = s.replace('\t\tResource resource;','\t\tResource resource;\n\t\tResourceName existingHeader;\n\t\tstring error;\n\t\tGC_MissionMetadataConfig metadata = new GC_MissionMetadataConfig();')
needle = '\n\t\tif (!editor.Save())'
s = s.replace(needle, '''
\t\telse
\t\t{
\t\t\tresource = metadata.FindHeader(world, worldResource, existingHeader, error);
\t\t\tif (!resource)
\t\t\t{
\t\t\t\tWorkbench.Dialog("Create Mission Data", error);
\t\t\t\treturn;
\t\t\t}
\t\t}
''' + needle, 1)
s = s.replace('\t\t\tstring error;\n\t\t\tif (!form.Create', '\t\t\tif (!metadata.Apply(resource.GetResource().ToBaseContainer(), exporter, worldResource, error))\n\t\t\t{\n\t\t\t\tWorkbench.Dialog("Create Mission Data", error);\n\t\t\t\treturn;\n\t\t\t}\n\t\t\tif (!form.Create', 1)
s = s.replace('if (!exporter.Save(world + ".metadata.json", world))', 'if (form.m_bMetadataOnly && !metadata.Save(resource, existingHeader, exporter, worldResource, error))')
s = s.replace('"World saved. Metadata export failed. Check write access to the mission folder and the Workbench console before retrying."', '"World saved. " + error')
p.write_text(s)
p = addon / 'scripts/WorkbenchGame/GC_SubmitMissionPlugin.c'
s = p.read_text().replace('\t\tif (!editor.Save())', '''\t\tResourceManager manager = Workbench.GetModule(ResourceManager);
\t\tMetaFile worldMeta;
\t\tif (manager)
\t\t\tworldMeta = manager.GetMetaFile(path);
\t\tif (!worldMeta)
\t\t{
\t\t\tWorkbench.Dialog("Submit Mission", "Save and register the mission world first.");
\t\t\treturn;
\t\t}
\t\tResourceName worldResource = worldMeta.GetResourceID();
\t\tResourceName headerName;
\t\tstring error;
\t\tGC_MissionMetadataConfig metadata = new GC_MissionMetadataConfig();
\t\tResource header = metadata.FindHeader(world, worldResource, headerName, error);
\t\tif (!header)
\t\t{
\t\t\tWorkbench.Dialog("Submit Mission", error);
\t\t\treturn;
\t\t}
\t\tif (!editor.Save())''', 1)
s = s.replace('!exporter.Save(world + ".metadata.json", world)', '!metadata.Save(header, headerName, exporter, worldResource, error)')
s = s.replace('"Could not refresh mission data. " + exporter.GetError()', '"Could not refresh mission data. " + exporter.GetError() + error')
p.write_text(s)
p = Path('companion/lobby2/tools/GC-SubmitMission.ps1')
s = p.read_text().replace('@($layers,"$relative.metadata.json")', '@($layers)')
p.write_text(s)
