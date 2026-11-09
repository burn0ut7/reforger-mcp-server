# Background Workbench operation

Current compatibility: see the [validated 1.9 maintenance profile](workbench-maintenance-profile.md). The historical 1.8 provenance below remains relevant to that separate profile.

The MCP automatically enables a process-local background policy after the configured
Workbench has a main window. Native tool dispatch also checks installation before
running an editor operation. No Windows-wide foreground or taskbar settings change.

`workbench_focus_policy(processId, action)` accepts `status`, `enable` and `disable`.
Status observes installation and per-API suppression counters. Disable permits an
explicit focus-dependent exception; enable restores suppression. Neither action
activates a window. A disabled policy survives MCP restart and remains disabled
until explicitly enabled or Workbench exits. A fresh Workbench process defaults to
enabled. Ordinary tool calls never silently disable it.

When another application is foreground, the policy intercepts Qt's requests to
activate, focus or raise Workbench windows. Show/placement requests use
non-activating display, and positioning preserves geometry while preventing
activation and z-order changes. Explicit taskbar flashing is suppressed, including
when Workbench is foreground; requests to stop existing flashing pass through.
Clicking Workbench or selecting it through Windows remains possible. Once it is
foreground, its normal activation, focus and positioning requests pass through.
Dialogs remain visible to MCP inspection and can still block an operation.

## Scope and limits

This is the installed Qt Windows platform's import boundary, not a global ban on
Windows activation. Initial startup before attachment, separate game processes,
non-Qt native dialogs and OS-initiated activation are outside its coverage.
Background maximize/restore requests use a non-activating normal display, so those
presentation requests do not preserve maximization. Existing flashing is not
automatically cleared at attachment. A foreground change racing an individual
native call is possible; foreground sampling cannot prove absence of all brief
transitions. Fullscreen Play and third-party native plugins need separate coverage.

Use status counters alongside the operation's outcome and foreground observations.
Counters count intercepted calls, including harmless show/position requests, not
distinct prevented interruptions. They are cumulative, independently sampled and
are not an atomic snapshot across all APIs. `workbench_script_status` includes the
last automatic/manual policy result; use policy status for fresh counters.

## Owning implementation and evidence

`NativeWorkbench.Focus.cs` verifies Workbench 1.8.0.13 and the installed Qt 6.8.3
`platforms/qwindows.dll` before touching its eight aligned import slots. Workbench
SHA-256 is the existing native profile's
`7dd36ca1f662b0c13669a4f2f7f10e3c461c2906b021e742272d64b3ab7e9798`;
qwindows SHA-256 is
`12577a7c4f2230bbea53f279573d7e3d1a08d631197855481e1e1580f61bb877`.
PE import metadata identifies these slots:

| API | Import RVA |
|---|---|
| SetForegroundWindow | `0x90e88` |
| SetActiveWindow | `0x90e98` |
| SetFocus | `0x90ec8` |
| BringWindowToTop | `0x90ed0` |
| SetWindowPlacement | `0x90ee0` |
| FlashWindowEx | `0x90ef0` |
| ShowWindow | `0x90f08` |
| SetWindowPos | `0x91240` |

The matching upstream [Qt 6.8.3 source](https://github.com/qt/qtbase/blob/v6.8.3/src/plugins/platforms/windows/qwindowswindow.cpp)
shows `requestActivateWindow` calling foreground/focus APIs, `alertWindow` calling
FlashWindowEx, and show/position paths using Win32. This source explains behavior;
the installed PE metadata and exact hashes establish the patched locations. No
decompiled private function ABI is assumed. Microsoft documents that a denied
[foreground request can itself flash the taskbar](https://devblogs.microsoft.com/oldnewthing/20080512-00/?p=22363),
so suppressing explicit FlashWindowEx alone would be insufficient.

The C# implementation emits small x64 wrappers, registers unwind information,
publishes import pointers with compare/exchange and verifies readback. It patches
neither system DLLs nor files on disk. Unknown hooks or changed bytes are refused.
The wrappers consult the live foreground PID on each call rather than a polling
cache. Original arguments pass through when disabled or already foreground.
WINDOWPLACEMENT is copied before alteration; caller memory remains unchanged.

Code, unwind tables and control data occupy 12 KiB until Workbench exits. Disable
uses a one-byte control flag; code is never freed while another thread could be
executing it. Import anchors and exact regenerated wrapper bytes allow a restarted
MCP to adopt the installation. Partial/uncertain installation is reported explicitly
and can require a Workbench restart after preserving work. After an install-thread
timeout, import pages remain writable rather than revoking access under a possibly
running publisher; the incomplete installation is refused on subsequent inspection.

## Verification

Run `dotnet run --project tests/WorkbenchFocus -c Release` for executable wrapper
checks: activation returns/counters, foreground and disabled pass-through,
non-activating show/minimize mappings, seven-argument positioning, copied placement
and flash-stop handling. These run in the test process and do not touch Workbench.
Live validation must additionally exercise production MCP operations with another
application foreground and verify both completion and foreground observations.

On 2026-10-31, the Release build and 38 checks passed (36 executable wrapper checks
plus two monitor checks for an exited/nonexistent configured PID). Independent
review reproduced those checks and approved the implementation after the monitor
was corrected to contain process-discovery failures.

Production HTTP MCP evidence is retained locally in
`.temp/local/evidence/focus-20261031/`. With Workbench PID5000 and outside foreground
PID5152, the current Lobby2 world reopened successfully, Script Editor opened and
closed through advertised native actions, and the SetForegroundWindow suppression
counter increased from zero to one. Neither native approximately 5-ms observations
nor client approximately 2-ms observations contained Workbench. ShowWindow and
SetWindowPos interception counters also increased. Invalid actions and stale PIDs
were rejected. Disable survived a subsequent native status operation and MCP host
restart; enable restored suppression without replacing the retained hooks.

The first reload returned `reload_failed` on a concurrently edited addon's reserved
keyword error. After that source changed externally, a new reload completed with
native success in 3,238 ms, including successful Play stop, without taking focus.
Resource/configuration errors remained in the log; reload success is not a claim
that the addon is error-free. Play was then restored successfully in 906 ms with
the same outside foreground; the original world remained open and the temporary
Script Editor window was closed. No addon files were changed by this work.

Live explicit FlashWindowEx requests and manual user switching were not exercised;
their wrapper behavior was tested locally. World Editor itself stayed open to
preserve the session; creating that editor from closed state is not a live test
claim. Foreground sampling cannot exclude shorter transitions. The broader engine
analysis started during investigation was cancelled without publishing incomplete
results after Qt source and installed import metadata resolved the implementation.

