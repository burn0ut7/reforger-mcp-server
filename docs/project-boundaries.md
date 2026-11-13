# Project boundaries

Reforger MCP, Lobby2 and the GC website are separate products and installations.
Generic MCP tools may be used to develop or verify either project; that does not
make the server a product dependency.

## Ownership after the 2026-11-13 migration

- Reforger MCP owns generic mod-making tools, Workbench interaction/control,
  focus/assertion suppression and script-authorization policy. GC-specific
  authoring setup/startup CLI commands and implementation have been removed.
- Lobby2 owns `external/source` (GC Tools, dropdown DLL, fixed startup loader,
  Git scripts, installer and build scripts), `external/workbench` (distribution),
  and `external/development/lobby2` (authoring docs, integrations and test addons).
  These were moved out of this repository without discarding unfinished changes.
- The website owns its application and offline map archive reader. Its extractor
  no longer links source from this repository. Optional live map-export development
  uses the generic MCP API, not a website application/runtime dependency.

Lobby2's normal-start bootstrap initializes only its own checksum-verified DLL
in its Workbench parent. It does not install or call the MCP executable, load
arbitrary libraries, execute scripts, control windows or implement suppression.
The native DLL and GC-prefixed providers use their own protocol contract.
Generic RST-prefixed MCP adapters remain separately installed and maintained.

## Validation and limits

MCP builds independently and its 15 maintenance checks pass. The website's offline
extractor builds independently. Lobby2's self-contained package builds solely
from Lobby2 source plus ordinary developer SDKs; it contains GC UI/loader, Git
scripts, dropdown DLL and .NET runtime. Packaging rejects MCP server/SDK,
ASP.NET and SSH components. Ten native tests, fresh-profile installer fixtures,
GC UI review/status tests and isolated Git submission/update tests pass.

The installer reads saved Workbench NET API preferences and refuses missing or
disabled settings. It does not silently configure the editor. Normal editor
script-authorization prompts can still occur; suppressing them belongs to MCP.
A live test in the existing development profile is not a clean-machine test.

Old GC version directories/archives are retained as historical evidence, not
active dependencies. The active GC installation record and managed bootstrap
select the standalone package. Separately installed MCP adapters and editor
preferences are preserved. Uninstall revokes GC loading before preserving any
modified startup files; it never unloads a live DLL.

Exclude all Lobby2 `external` development/package material from Workshop release
staging. Never make either repository resolve a sibling source path or publish
another project's executable into its own product.
