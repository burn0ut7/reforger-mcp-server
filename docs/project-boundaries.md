# Project boundaries

Reforger MCP owns generic Reforger MCP capabilities. Lobby2 owns its addon,
GC Tools, custom Workbench integration, and all external dependencies installed
through GC Tools. The GC website owns its web application. These are independent
products, not components of a single MCP distribution.

Generic MCP tools may be used during development of either external project.
That development relationship must not become a source, build, installation,
or runtime dependency.

## Existing violations requiring migration

Inspection on 2026-11-13 found these boundaries are not yet satisfied:

- companion/lobby2/tools contains the production GC Tools UI and workflows
  inside the MCP repository.
- installer/workbench and scripts/package-workbench-integration.ps1 build
  Lobby2's external dependency distribution here. The package script publishes
  the complete ReforgerMcp project into GC Tools' helper payload.
- The GC installer uses ReforgerMcp.exe for installation and Workbench startup.
  Moving the installer files alone would not remove that runtime dependency.
- GCWorkbench.UI.csproj links src/ReforgerMcp/RuntimeNet.cs directly into
  GC Tools. This cross-project source link must be removed.
- Lobby2's existing external/workbench/GCWorkbench.zip was produced by that
  coupled pipeline. Documentation changes do not alter the archive or already
  installed copies.

## Required migration outcome

Move Lobby2-owned production sources, build scripts, installer, assets and their
tests to Lobby2 ownership, preserving unfinished changes. Implement its required
Workbench installation/startup behavior as a standalone Lobby2 component.
Separate any generic MCP functionality from GC-specific behavior without creating
cross-repository source links or a dependency on the MCP server executable.

Rebuild and inspect the Lobby2 distribution to confirm it contains no MCP server,
MCP SDK, MCP configuration or source dependencies. Validate installation and
GC Tools on a machine/profile without Reforger MCP installed. Verify the MCP
server independently builds and works without Lobby2 or the website.

Do not remove currently installed components blindly: preserve user settings and
authored work, distinguish separately installed MCP components from GC-owned
files, and replace the GC package through its owning installer.
