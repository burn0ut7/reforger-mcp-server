# Workspace layout and rebuilding

The downloadable tool must build and package without any existing scratch files.

| Location | Ownership |
|---|---|
| `src/`, `adapters/` | Authoritative server and runtime adapter source |
| `scripts/`, `skills/`, `docs/` | Supported packaging, browser capture, guidance and documentation |
| `THIRD-PARTY/`, configuration example | Required distribution notices and portable setup templates |
| `.temp/tests/` | Optional local harnesses and fixtures; not included in the public solution or download |
| `.temp/local/` | Former `.local/`: historical configs, downloaded runtimes, caches, research and evidence |
| `.temp/artifacts/`, `.temp/evidence/`, `.temp/research/` | Generated packages, historical results and experiments |
| `src/**/bin/`, `src/**/obj/` | Ignored, disposable .NET build output recreated by the SDK |
| `.codex/` | Ignored tool-managed settings; not required to build or run the product |

Everything under `.temp/` is disposable. Stop processes using those files before removing it. No public build or packaging step reads from it; packaging creates new output there. Tests and historical evidence are intentionally not recoverable from the public download. Deleting them loses that history, not the implementation. Keep authored mods and anything you intend to retain outside `.temp/`.

## Rebuild from a clean download

On Windows x64, install the .NET 10 SDK and PowerShell 7. From the source root:

```powershell
dotnet restore ReforgerMcp.slnx --locked-mode
dotnet build ReforgerMcp.slnx -c Release --no-restore
pwsh -File scripts/check-release.ps1
pwsh -File scripts/package.ps1
```

The source audit requires Git and a Git checkout. For a source ZIP, initialize a local repository with `git init` before running the audit/package script; no commit or remote is needed. Restore downloads the dependencies recorded in the NuGet lock file. Packaging creates `.temp/artifacts/package-<timestamp>/`, its ZIP and SHA-256 checksum. Neither operation requires the old `.temp/`, tests, game data, Ghidra or JDK.

## Recreate runtime data

Follow [setup](../README.md#setup), copy `configuration.example.json` to `configuration.json` and set paths for your installations. Check it with `ReforgerMcp.exe --check-config <configuration.json>`. Private settings cannot be inferred from someone else's archived configuration.

- PAK/script indexes: configure installed sources, then call `pak_prepare` through MCP.
- Wiki indexes: call `wiki_prepare`, or capture the official pages and explicitly import them as described in the [wiki guide](wiki-index.md). Recapture depends on the upstream website being available; historical revisions are not guaranteed to remain obtainable.
- Native analysis: install the documented Ghidra and JDK versions, configure native targets, then run `ReforgerMcp.exe --config <configuration.json> --prepare all` or use `engine_prepare` and poll `engine_job`.

The configuration example use `%LOCALAPPDATA%/ReforgerMcp` for generated runtime data. **Ghidra databases cannot be created under dot-prefixed directories, including `.temp/`.** Use the explicit external data directory for active native analysis. That derived data is also rebuildable from the user's installations. Removing it requires stopping its server and explicitly preparing again; it does not trigger a hidden rebuild.

Old configurations, manifests and logs moved into `.temp/` are historical records and may contain their original absolute paths. They are not an active migrated setup. Recreate configuration from the example, update any client command/config paths, and explicitly prepare a fresh store instead of treating relocated native databases as ready. No installed game or user addon files are moved by repository housekeeping.
