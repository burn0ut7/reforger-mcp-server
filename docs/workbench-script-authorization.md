# Script authorization

Current compatibility: see the [validated 1.9 maintenance profile](workbench-maintenance-profile.md). The historical 1.8 provenance below remains relevant to that separate profile.

MCP Workbench launches include `-scriptAuthorizeAll`. In an already running
supported Workbench, native dispatch calls the same owning function used by that
startup option, on the editor thread. The existing automatic startup native-policy
dispatch also activates it when the editor becomes ready. No recurring AI decision,
per-script list, popup text matching or simulated button click is required.

This deliberately selects Workbench's **Yes to All** policy for all protected
script methods, including container loading, file access and process operations.
It is broader than a LoadContainer exception. Different script names, line numbers
and method names use the same decision. It does not authorize Windows UAC prompts
or unrelated applications, and does not hide compiler errors or VM exceptions.

`workbench_script_authorization(processId, action="status")` reads the current
decision. `action="allow"` activates it explicitly and reads it back. The raw
Yes-to-All decision is `32768` (`0x8000`); `allowAll=true` is reported only after
successful native completion/readback. Status does not change this decision.
Normal native dispatch reasserts the requested automatic policy. Permission lasts
until Workbench exits and survives MCP restarts and script reloads.

An authorization dialog opened before activation must be resolved once. The setter
does not close an already executing dialog. Future MCP launches avoid that startup
gap by passing the flag from process creation. For Workbench launched outside MCP,
the same flag can be added to its launch arguments; without it, automatic attachment
requires a ready editor. No Workbench restart is required to enable the current
ready session.

Bohemia documents the [startup option](https://community.bistudio.com/wiki/Arma_Reforger:Startup_Parameters#scriptAuthorizeAll)
and its equivalence to [Yes to All in the Script Authorization Required dialog](https://community.bistudio.com/wiki/Arma_Reforger:Game_Master:_Image_Generation_Tutorial#Play).

## Native provenance

The implementation uses the existing exact Workbench 1.8.0.13 profile, SHA-256
`7dd36ca1f662b0c13669a4f2f7f10e3c461c2906b021e742272d64b3ab7e9798`.
The original complete Ghidra 12.1.4 archive was preserved and copied into the current
store; executable hash and its `analysisCompleted=true` record were checked. The
analysis retains 101 warnings and 22 errors, so these are scoped findings, not a
claim of complete engine understanding.

The Workbench constructor at RVA `0x1fd200` constructs WorkbenchVMErrorHandler at
`app+0x12b0`. It checks `-scriptAuthorizeAll` via the pointer at RVA `0x3b0a920` and
calls RVA `0x298d00` on that object. That function consists of
`mov dword ptr [rcx+0x1c],0x8000; ret`. The authorization handler at RVA `0x2980f0`
checks the same field through its VMErrorHandler subobject (`app+0x12c0`, field
`+0xc`) before generating any script/method-specific text. `0x8000` returns true;
`0x20000` returns false. The dialog owner at RVA `0x298c00` supplies Yes, Yes to All,
No and No to All and writes its result to that field. Other VM-error handlers are
separate and are not modified.

Runtime guards validate the application profile, handler vtable at RVA `0x2bac1c8`,
the owning function bytes and valid native decision values. The setter is invoked
through the existing temporary Qt-thread dispatcher. No code patch, system-wide
policy change, installed-file modification or permanent injected allocation is
needed for authorization.

Relevant local Ghidra query IDs: `5256abd06f1c41f89eea07e87cdfca4a`
(dialog title), `d6773a443783466098667335b19daea5` (dialog),
`24109390ed4743eba7212affd1535690` and `e432ac3a60944286ac2438e34b20242a`
(handler), `c5207dbeced64b17bf739b3d7bb33e72` (handler instructions),
`fde1e0b95adb4264a7cc3bf18194cbfc` (object construction),
`51fb5ef7a2364754a55af04703b7f7d4` (startup option),
`a2c115637ceb4b6fa50ee00cfad78c14` (setter instructions).

## Validation

The 2026-10-31 Release build passed with zero warnings/errors. Production MCP
readback on Workbench PID5000 reported `allowAll=true`, decision `32768`, on the
expected editor thread with temporary allocations released. It remained true after
script reload. Two temporary explicit read-only methods in the already loaded
MCP test addon, in different source files, successfully loaded a valid container
resource and read an ordinary file outside the profile directory. Both returned
true through the production runtime tool without a permission dialog. Native
world reload and the earlier background focus policy remained functional.

An initial container probe used an absolute path where a ResourceName was required;
it returned false and logged invalid-resource format. That result is retained and
is not counted as a successful container load. The corrected GUID-qualified input
succeeded. Invalid action and stale-process requests were rejected. Independent
review inspected the code, native evidence and live results and approved this scope.

Probe sources and selected MCP records are retained under
`.temp/local/evidence/script-authorization-20261031/`; original call records remain
in `.temp/local/evidence/focus-20261031/`. Temporary probe scripts were removed from
the loaded test addon after execution. Existing resource/configuration warnings
were retained; successful compilation does not establish an error-free addon.
The cleanup reload succeeded, the same world remained open, and Play was restored
with authorization still enabled. Workbench was not restarted.

A new Workbench launch was not exercised in this test pass, to preserve the loaded
project. Startup-flag wiring is supported by official documentation, inspected
constructor code and launch implementation rather than a claimed restart test.

