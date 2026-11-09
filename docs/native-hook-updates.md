# Reproducible native hook discovery and builds

`scripts/update-native-hooks.ps1` freezes the working sources, runs maintenance tests,
builds a separate Windows x64 framework-dependent server, and compares a known
Workbench analysis with an updated analysis. It uses the existing pinned Ghidra
backend; no second disassembler or address-guessing runtime is introduced.

The result is a research/build bundle, **not an automatically approved native profile**.
The runtime still rejects unrecognized binary hashes. The script does not overwrite
Steam files, restart Workbench, deploy a server, change the configured data owner,
or turn candidate positions into native calls.

## Run after an update

1. Preserve the previously supported `workbench.json` analysis manifest before
   preparing the update. Preserve its referenced project and `Engine.exe` snapshot.
   The dispatcher source must still contain that baseline SHA-256 guard.
2. On the existing MCP server, call `engine_targets`, then `engine_prepare` for
   `workbench` when stale. Poll that job until `ready`. Preserve warnings and errors.
   Do not start a second server against the same data directory. With no server
   running, the existing `--config <path> --prepare workbench` command is equivalent.
3. Run from the source checkout in PowerShell 7:

```powershell
./scripts/update-native-hooks.ps1 `
  -Configuration ./configuration.json `
  -BaselineManifest C:/Evidence/previous-workbench.json `
  -CandidateManifest C:/Evidence/current-workbench.json `
  -OutputDirectory C:/Evidence/update-run-001
```

Use a new output directory each time. The configuration supplies the installed
Workbench path, Ghidra/JDK paths and query deadline (maximum 600 seconds). The two
manifests must refer to complete Ghidra 12.1.4 projects and hash-matching snapshots;
the candidate must match the installed executable. A stale or missing input fails.
The snapshot build requires .NET 10 and its locked dependencies. It requires the
.NET desktop and ASP.NET runtimes to run; this is a maintainer build, not the
self-contained release package from `scripts/package.ps1`.

For cold preparation of the 1.9 Workbench binary, the 30-minute analysis budget
was insufficient in the observed installation. The example configuration now
allows 3600 seconds. The tested local retry also raised `javaHeapMegabytes` from
4096 to 6144 after checking available memory. Change preparation settings before
starting the job and restart the owning MCP server to load them, preserving its
transport/data directory and leaving Workbench running. Retain timed-out job
evidence; never present an incomplete project as ready. A completed analysis can
still contain warnings/errors and requires per-hook inspection.

## Evidence produced

- `source/`, `source-hashes.json`: frozen source inputs, including uncommitted work.
- `dotnet-info.txt`, `tests.txt`, `build.txt`: toolchain and build/test outcomes.
- `server/`, `server-hashes.json`: separate server binaries and per-file hashes.
- `discovery/inventory.json`: explicit Workbench RVAs with source file, line and context.
- `discovery/baseline/` and `candidate/`: exact Ghidra request, response, console and execution evidence.
- `discovery/sources.json`: source identities used for discovery.
- `discovery/run.json`: completed `review_required` or failed status, binary identities and match counts.
- `discovery/CandidateHooks.cs.txt`: unique positions as inert C# constants. The `.txt`
  suffix intentionally prevents inclusion in any server build.

Discovery exit code zero means evidence generation succeeded, **not that the new
Workbench is supported**. `runtimeApproved` remains false, including when every
hook has one candidate. Failed runs retain diagnostics and do not deploy anything.
Exact addresses are RVAs relative to each identified module, never reusable absolute
process addresses. The live dispatcher still relocates from the loaded module base.

## Matching and coverage

The `masked-function-v1` recipe fingerprints complete Ghidra-defined function
bodies. It retains instruction positions, lengths, opcodes, non-address immediates,
registers and resolved internal control-flow topology. Only operands with resolved
memory references are masked; string targets are included as semantic anchors.
Code hooks use the matched function plus the instruction/byte offset. Data hooks
follow the same operand/reference kind from matched inbound code anchors (up to
eight anchors, scanning up to 128 references). All candidates from these anchors
are retained as a union, so conflicting anchors produce ambiguity instead of a vote.
At most 32 positions and 16 evidence records are displayed per hook; total candidate
counts remain exact. Defined function coverage is limited by Ghidra's analysis.

The inventory recognizes explicit `image + RVA`, `exe.BaseAddress + RVA`, owning
`Call(RVA)`, `CheckCode(exe, RVA)`/guard arrays, history-call alternatives and the
assertion-latch constant. It is intentionally **not a C# semantic parser** and does
not certify that every native dependency has been inventoried. Future source
expression forms require extending the inventory and its regression tests.
Qt/system modules, embedded layout offsets, virtual slots and instruction-emitter
ABI details remain separate review items. Source hashes bind that review to exact
files; an update during discovery invalidates the run.

Matching is conservative: changed functions can remain unresolved; short accessors
can be ambiguous. Unique structural identity still cannot prove the identity of a
changed external call target, correct ownership or a safe calling convention.

## Promote a profile

Use the result to update the isolated dispatcher only after reviewing native owners,
external call targets, singleton/vtable identities, settings/field layouts, Qt ABI,
thread dispatch, teardown and allocation lifetime. Resolve every ambiguity relevant
to the enabled capability using the existing engine search/function/reference tools.
Do not replace just the executable hash or use the first candidate.

Follow the full [native adapter validation procedure](workbench-native-reload.md#updating-after-workbench-changes):
harmless main-thread observation, reload success/failure/recovery, editor operations,
runtime adapter handshake, lifecycle and cleanup tests, then independent review.
The existing source build/package workflow produces deployable binaries after those
changes. This discovery workflow deliberately does not self-approve or hot-load them.

For a locator regression test, point both manifests at the same baseline and use
a local configuration whose Workbench executable is its preserved snapshot. Every
unique candidate must reproduce its original RVA. Ambiguous and unresolved entries
remain visible. Compare two builds with the same source, SDK, locked dependencies,
RID and flags using their DLL hashes; path mapping removes output-folder differences.
Timestamps in logs and machine-specific runtime apphosts are not a bit-for-bit release
reproducibility guarantee.
After deployment, run the [live MCP acceptance gate](mcp-acceptance.md). Offline discovery success does not establish runtime compatibility.
Multi-profile sources select hook inventory by baseline executable SHA256. The scoped [1.9 maintenance profile](workbench-maintenance-profile.md) excludes unported legacy editor addresses. Frozen builds run the maintenance callback, focus and discovery regression suites.
