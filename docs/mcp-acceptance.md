# Live MCP update acceptance

Run after starting the intended server package on the configured shared HTTP endpoint:

~~~powershell
./scripts/test-mcp-features.ps1 -Configuration ./configuration.json -OutputDirectory ./work/acceptance-run-001 -ExpectedServerDllSha256 <published-ReforgerMcp.dll-sha256>
~~~

Use a new directory each time. The command connects to the existing authenticated server using the official MCP client; it does not launch a second data-store owner. It builds in a separate directory and never replaces the running server. Tokens are not written into reports.

report.html is the readable tool matrix; report.json is the automation result. Exit 0 means acceptance passed, 1 means a failed probe, contract inventory mismatch or connection error, and 2 means incomplete coverage or unverified deployment identity. Automation must require exit 0 and updateSucceeded=true. Name coverage is compared to an explicit 58-tool inventory: newly advertised or missing tools fail the gate. Saved schemas are evidence, not automatic schema compatibility proof.

The current suite is intentionally an incomplete acceptance gate: it runs safe positive smoke checks for catalogue search/read/extraction (with byte hash verification), log reading, capture (PNG dimensions and ordered timestamps), native analysis queries and Workbench observations. Every other tool remains visible as blocked or not_tested. Passing a smoke check proves only the stated assertion, not all options or all related workflows.

The four maintenance capabilities now have a [scoped 1.9 profile and focused live tests](workbench-maintenance-profile.md). Other native editor operations remain unsupported on 1.9. The broad suite continues to report untested workflows and does not grant full update approval.

## Remaining acceptance fixtures

Before this suite can certify a full update, implement isolated positive workflows for:
- analysis preparation, job polling and cancellation against an owned disposable target/data store; never cancel another job;
- catalogue replacement against fixture roots, preserving the user's catalogue;
- script compilation/reload and current compile diagnostics;
- project lifecycle, window actions and terminal polling with returned knownRevision;
- world/entity creation, inspection, edits, save/reopen, prefab inheritance and undo/redo readback;
- Play/runtime invocation and multiplayer, including cleanup and restoration of settings.

The historical .temp/tests harnesses provide examples but use stale tool contracts and launch another stdio server. They are not invoked by this command. Do not run mutation tests on the user's open world or interpret an expected rejection as positive capability coverage. Native fixture work is blocked until a validated hook profile supports the installed binary.

Disk hashes identify the observed listener executable's adjacent DLL before and after the run, not loaded memory. Both observations must agree on PID, process start time, executable path and DLL hash before deployment identity is marked verified. Supply the expected published DLL hash to prevent attributing an older deployment's result to a new candidate. Keep the listener stable during testing; inspect the recorded PID and start time. Evidence contains local paths, log snippets and screenshots and should be reviewed before sharing.

The offline update-native-hooks.ps1 command remains discovery/build only. Its candidates are not deployment approval. Run this acceptance command as the explicit post-deployment gate; neither command automatically bypasses unsupported native hashes.


