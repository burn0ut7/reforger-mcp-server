# Release preparation

The public source contains the C# server, its Java native-analysis adapter, the authored Enforce runtime adapter, configuration example, current guides and dependency notices. Installed game files, extracted scripts, decompiled output, Ghidra projects, personal configuration and validation captures stay local. Users supply their own installations and data.

## Before publishing

Run these maintainer steps from a source checkout; the binary ZIP does not include the build/test scripts.

1. The owner has explicitly chosen to distribute this project without a project license. Do not add a `LICENSE` or describe the project as open source. Keep all dependency licenses and notices in `THIRD-PARTY`. Run `pwsh -File scripts/check-release.ps1` before publishing.
2. Review `git status --short --untracked-files=all` and the proposed staged diff. The source audit checks tracked files even when ignored, plus normal untracked candidates, against an explicit file allowlist. It also checks local Markdown links, this builder's private home path and selected credential patterns. It is a guard, not a comprehensive secret or copyright scanner, and does not audit prior Git commits or staged bytes that differ from working files. Review any new file before extending the allowlist; do not force-add ignored data.
3. Build `dotnet build ReforgerMcp.slnx -c Release`. Validate the affected workflows for changes being released; retain exact binary hashes and results under `.temp/evidence/`. Optional local harnesses under `.temp/tests/` are not a public build prerequisite. See [validation](system-validation.md) and individual tool guides. Older live test modes can require specific disposable fixtures or retained research setup binaries; they are not a universal unattended release suite.
4. Run `pwsh -File scripts/package.ps1`. It runs the source audit, publishes a self-contained Windows x64 package, copies explicit documentation/configuration files and dependency notices, checks for private/game data and broken documentation links, and writes a ZIP plus SHA-256 checksum under `.temp/artifacts/`.
5. Smoke-test that exact package through stdio in a clean client configuration. Choose a release version/tag and describe the supported Workbench binary profile and known limitations in the release notes. Publish only the reviewed source and selected ZIP/checksum, never the whole workspace.

The server currently supports the exact Workbench profile documented in [native reload](workbench-native-reload.md). Saved project context does not include unsaved buffers or effective override order. The separately observed reload resource-leak assertion remains unresolved as recorded in [runtime limitations](workbench-runtime.md); do not claim this cleanup fixed it. This preparation does not certify every tool on every game build or constitute publication.

## Existing Git history must remain private

The local history includes obsolete compiled binaries and fixtures explicitly labeled as copied game-source excerpts (for example, the former `tools/fixtures/lexer/workbench_basic_code_formatter_excerpt.c` and `tools/fixtures/parser/game_building_provider_excerpt.c`). Removing files from today's tree or adding ignore rules does not remove earlier commits. Do not publish this repository's existing history. Prepare a fresh public repository from the reviewed current source without `.git` or ignored data, or separately review and sanitize history before publication. This cleanup has not rewritten history or pushed anything. Local inventory evidence is under `.temp/local/evidence/release-cleanup-20261021/history-review-candidates.txt`.

## Local housekeeping

All repository scratch material belongs under ignored `.temp/`: optional tests, evidence, research, caches, downloaded analysis runtimes and package output. The entire directory may be deleted after stopping processes using it. It is excluded from the public solution and release source allowlist. Historical results and custom test fixtures will be lost; required product outputs can be rebuilt from public source and separately installed prerequisites.

See [workspace layout and rebuilding](workspace.md) for recovery commands and the native Ghidra data-directory constraint. `.codex/` remains an ignored tool-managed settings directory; it is not a product dependency. Private configuration is supplied by each user. The authored runtime adapter and dependency notices remain public source.
