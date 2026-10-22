# Direct Workbench launch

The same C# MCP server exposes two launch/status tools for all compatible clients. No scripted bridge, UI automation, native injection, Java or Ghidra is used by these two tools. Separate [script commands](workbench-scripts.md) and [world opening](workbench-worlds.md) share one version-checked in-process dispatcher, with the same configured executable. These launch tools use accessibility only for read-only status and readiness checks. Separate [control/dialog tools](workbench-windows.md) inspect and invoke native controls.

In your `configuration.json`, set `workbench.executable` to the installed Workbench executable and `workbench.addonDirectories` to the directories containing its dependencies. Include the **game installation's** `addons` directory, plus any mod dependency roots. Paths resolve relative to the configuration file, with the same environment/`${Documents}` expansion as other settings. Commas in directory paths are rejected because Workbench treats them as separators. The directories must exist.

Workbench launch is independent of `targets.workbench`, which configures native analysis only. Neither setting substitutes for the other. `--check-config` checks the launch paths without starting Workbench. No installation paths are discovered or guessed.

## Calls

1. Call `workbench_status`.
2. If stopped, call `workbench_open_project` with `{"projectPath":"D:/Mods/MyAddon/addon.gproj"}`.
3. Inspect `workbench_status` and the editor before continuing.

The project path must be absolute and name an existing `.gproj` file. The server passes arguments directly using .NET `ProcessStartInfo.ArgumentList`, with no shell:

```text
ArmaReforgerWorkbenchSteamDiag.exe -gproj "<project.gproj>" -addonsDir "<dependency root>,<another root>"
```

The result `started` confirms only process creation. Dependency resolution and script initialization can still fail or display a dialog. The tool does not parse or validate project contents, compile mods, wait for readiness, or claim the project loaded.

Status reports `not_configured`, `unavailable` (missing launch inputs and no observed instance), `stopped`, or `running`. Each instance includes its PID, executable path, window title and Windows responsiveness observation when readable. Missing configuration inputs remain visible even when an instance is running. A responsive error dialog is still a running process. `projectReadiness` is always `unverified`; titles do not prove the active project. `lastLaunch` identifies this server session's requested project and process exit code, if exited, **not** the currently open project. It is lost on server restart; existing processes are still detected.

Any observed process with the configured executable name or an `ArmaReforgerWorkbench` prefix blocks a new launch, including other installations and unreadable instances. The launch tools never switch projects, close editors, kill an editor on disconnect, or automatically retry. A named Windows-session mutex serializes the check/start operation across cooperating MCP server processes. It cannot prevent someone independently launching Workbench at precisely the same time, and it does not inspect other Windows sessions reliably.

After an uncertain response, inspect status and the editor before another explicit request. A cancelled call before process creation does not launch; cancellation after creation does not kill Workbench. The server itself never kills the editor. **A client that forcefully terminates the entire server process tree can also terminate Workbench launched by that server.** This includes disposal of the C# SDK stdio test client in the current SDK version. Normal stdin-EOF shutdown and client-request cancellation are distinct from forced process-tree termination.

## Research and scope

The official [Startup Parameters](https://community.bistudio.com/wiki/Arma_Reforger:Startup_Parameters#Workbench) documents `-gproj` for selecting the project and `-addonsDir` for additional dependency search roots. [Resource Manager options](https://community.bistudio.com/wiki/Arma_Reforger:Resource_Manager:_Options) specifically notes that startup-parameter launches must locate dependencies in those roots.

Research on 2026-10-20 used official search results and the saved browser capture of Startup Parameters, revision **379133**, captured **2026-10-20 10:56 EDT**. Direct page retrieval returned HTTP 403. The capture is historical evidence, not live editor state.

Other documented options include `-wbModule=<module> -run` for opening editors, `-profile`/`-logsDir` for storage locations and `-wbSilent` for initialization followed by exit. They are outside the launch interface: opening a project needs only `-gproj` and dependency roots. Arbitrary arguments, project creation, memory hooks and project-readiness detection remain outside its scope.
