# Workbench 1.9 maintenance profile

The exact 1.9.0.37 executable (SHA256 456a3e4e3b55f47d89c09e4364df78d9a0b1f7348bdc2354cce3b34e1db70b28) supports project context, focus policy, script authorization and the resource-leak assertion policy. Other native editor operations remain explicitly unavailable on 1.9. The complete legacy 1.8 profile remains separate. This is not a full editor migration.

## Native evidence

The Arma-derived constructor at RVA 1ea720 calls the Workbench constructor 2088f0, then installs application vtable 2c1c2f8. The base constructor sets singleton 3e881e8. The first live validation caught an initial base-class-vtable assumption before dispatch; the corrected derived-class table matches both instructions and live readback.

Authorization retains handler offset 12b0, VM subobject offset 12c0, decision offset 12cc and Yes-to-All value 8000. Handler vtable is 2c3ac08; setter 2d0420 writes [rcx+1c]. The new leak site tests latch 3fb2360 and sets it after the per-site ignore response; code windows 1aaa691/1aaa6a7/1aaa6cc are checked against the verified binary.

Project owner getter is 1f07120. Loaded array/count c8/d4, available d8/e4, current index120 and addon strings20/28/30/38 retain their layouts. The public Workbench.GetAbsolutePath wrapper now calls resolver2019c0. Its output is an engine-owned string: copying into the old raw-buffer ABI would corrupt the observation. The new callback copies bounded UTF-8 bytes from the returned reference, releases that reference via atomic refcount decrement and allocator1e95e70, and rejects overflow after cleanup.

Qt6Core and qwindows retain their exact hashes. The owning-thread callback queue and focus import-slot guards remain in force. Focus is independently usable on the two explicitly admitted executable versions; no blanket acceptance of future builds is enabled.

Ghidra query evidence identifiers:
- derived constructor: 281c2495a8324cfa823ee8f17b25b735
- base constructor: 697d3112de4a4e35bc1d1ce93472c5d8
- authorization construction: 7600b507d9c04f4a804a9137535e2447
- setter: daf9836b240d47138e8b3d9923a4a540
- leak instructions: ea36ee7d523c4575879000a3d9b73c43
- project loaded/available: 0224b3070a954c9196be63b1b5bcf72f, cdd83b2e22564e42a7c3d518e4ffdc4f
- current project: 391ed067fbd14a8b849dcc81e8f72507
- project owner: 8b7fa51c892e4ffa9bbbdeb0dd5117c6
- ID/title: b051297841cd4a3e97bda43db714267f, 0e41f0a659f5416a81f6ecdff001f4b6
- path wrapper/resolver/ownership: cb3f8f2933064023b025316178eccd76, 94dd6b2618654cc38ddc6afd21f9f008, 8c3eac7059484b2a95c243e1a9d2f3fb

These are inferred native observations, not public ABI guarantees. Exact executable and module hashes, code-byte checks, object guards and live tests bound their use.

## Repeatable validation

Offline native callback and profile tests:

~~~powershell
dotnet run --project tests/WorkbenchMaintenance -c Release --artifacts-path work/maintenance-tests
dotnet run --project tests/WorkbenchFocus -c Release --artifacts-path work/focus-tests
dotnet run --project tests/NativeHookUpdate -c Release --artifacts-path work/discovery-tests
~~~

Focused live acceptance against the existing shared HTTP MCP server:

~~~powershell
dotnet run --project tests/WorkbenchMaintenance -c Release --artifacts-path work/maintenance-tests -- --live configuration.json <Workbench-PID> <new-evidence-directory>
~~~

Exit 0 and report.json passed=true certify only this four-feature scope. The test toggles and restores focus policy, activates the already authorized Yes-to-All policy and per-site assertion suppression, validates native readback and callback cleanup, checks project identities/paging/mounted roots, and verifies that unported operations reject dispatch. It does not compile, save, switch projects or enter Play.

The live fixture expects an open 1.9 project with at least two distinct addons, readable declarations and at least one loose mounted addon. Missing fixtures fail explicitly. Status success alone does not establish permission-dependent script execution or provoke a real resource-leak event; these scenarios remain outside this scoped test.

The broad [MCP acceptance gate](mcp-acceptance.md) still reports full-tool coverage separately and refuses overall certification while other workflows are untested.

## Update workflow

Native hook inventory now selects the baseline executable's source profile. A limited 1.9 baseline exports only its admitted maintenance RVAs; legacy editor addresses are not misinterpreted as current hooks. Unknown or unspecified profiles fail closed. The frozen build runs callback, focus and discovery regression suites before publishing.
