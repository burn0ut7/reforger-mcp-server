# Workbench 1.9 maintenance profile

The exact 1.9.0.37 executable (SHA256 456a3e4e3b55f47d89c09e4364df78d9a0b1f7348bdc2354cce3b34e1db70b28) supports project context, focus policy, script authorization and the resource-leak assertion policy. The editor migration below adds individually verified capabilities; the complete legacy 1.8 profile remains separate. Unported editor operations reject dispatch.

## Editor migration

The full Reload owner is RVA `22ca80`. Its application state byte moved from `+164e` to `+1656`; the singleton, derived vtable and reload busy latch remain those verified below. Ghidra query `8e809ce51bd24b88bb6afb1344ff836f` records the full lifecycle, including validation, editor vetoes, module teardown/recreation and the completion marker. Reload does not call an isolated compiler.

Qt actions other than array insertion use only the unchanged, independently hash-guarded Qt modules. They are admitted on 1.9 through the existing owning-thread dispatcher. Array insertion additionally calls engine reflection and remains unsupported. Native `QWidget::close` preserves the normal close/veto/prompt lifecycle; it is used for an explicit project change, followed by the existing MCP process-launch capability. No forced termination is used.

Initial live evidence: empty World Editor close `04cf44b4e64b4252a588d4217b260f1b` and main editor close `4421115caaa6494fb3f2c29a4c45c63a` completed on thread 24624 with allocations released and unchanged foreground. The requested test project launched as PID 48680. Reload `93f02d31b8c94524926b5f41afb03c6d` completed on thread 7536 with native result 1, allocation release and unchanged foreground. Its fresh log cycle 20324–20411 in `logs_2026-11-08_23-36-40` had no SCRIPT errors; eight unrelated missing menu preset GUI errors remain recorded. These observations establish only the exercised lifecycle and compilation, not gameplay or all Qt actions.

The Play path retains descriptor count `+24`, instance virtual slot `+d8`, World Editor state subobject `+a0`, Play-state slot `+98` and saved-document path `+120/+50`. Its versioned getter/name are `219cf0/2c2f5c0`, primary `WorldEditorQt` vtable is `2c34418`, and action `28a2d0` calls lifecycle `28a3b0`. The action now includes the editor's Escape-key handling, which is preserved by calling the owning action. It still re-reads the actual Play flag and reports an unachieved transition honestly. Evidence queries: `802d7224107943ce975df30248e90f17`, `6c8c6c3b00f04c518b36bb51f1a2c436`, `f6ec97b6c20f4eae92b3bd0625e58270`, `025a5725e2564c49be79363a7ce7b046`, `97a4b92770ed474aab2f8d65fb47aa9e`, `fd23a667fdf543da987872b05b1a5000`.

World opening uses the same implementation with versioned module creation `21af40`, full resource opening `28e660`, dirty-mask owner `1aba050`, ResourceName construction/destruction `1e98cb0/273f70` and allocator pool `3d45a90`. Filesystem owner `1efd690` now returns an owned string from slot `148`; the callback bounds/copies/releases it before opening. File existence remains slot `150` with mount selector `-1`. Missing files, mismatched extension, oversized paths, Play and dirty documents are rejected before replacement. Prefab definition inspection and edits remain unported; 1.9 accepts only world `.ent` requests. Evidence: `18731d482537415383e4cdbaed035734`, `edb7e230f5e34c8f9820d467f77b3cb8`, `4ef56626b77347a886a9eea8f1c5a86b`, `c187a9487bb5450d8d21a9a0078d657a`, `58a1940827a849eebb98cddbcef2fcc1`, `5040dcb7da7c4a7aa74b98770a589581`, `f608105ae80b4d6d81e9bd8ef04fe144`.

Runtime setup follows the existing owner contract with settings offset `+1608`, BaseSource vtable `32b7828`, enabled/port IDs at `3bd65c0/3bd65c4` (values 8/9), accessors `5956b0/5956f0`, allocator `1e953c0`, NET API start/stop `2343e0/234920`, and selected-field save/serialization `236440/236520`. It uses the same owned-string profile resolver as project observation and does not change unrelated settings. Evidence: `767cf41ce5734a07b586cb78635f1e44`, `93e78f75e239497bb0cc4caf6985f0ef`, `e200f82292fd4a49b315886e200caa9c`, `825c2a8051934af88722f5dd68cb335d`, `e8cba69e0f3d43028798b7d787e06039`, `4d2587760b5f42a4a8baca1e405dc7c1`, `e1bbfcf266f44858ba5cdec91972697b`, `07dd44b7e7f744708e7341e03f668dbd`, `812942d08b7c4f03a4171d475f4a9d40`.

Setup preserves the legacy idle-application guard at the new state offset, including its initial read. Installation separately observes the owning Play flag, admitting an absent editor or confirmed stopped Play before writing adapter files. Active or uncertain Play is rejected. Ordinary runtime calls and their status handshake remain available during Play.

Live extension evidence on the same process: setup observation `a7bf1f053dfe4352b04af8cacca2b7a8` returned `ready`, matching profile/build handshake, PID-owned port 5776 and successful built-in compilation without changing settings or files. World opening `4366088a0102451e9424702d94524a06` created the editor and returned the exact mounted world path, native result 1, owning thread and released allocations. Reload `67ee5d57e9754119b09f32c4d99db079` confirmed stopped Play before reloading with that editor open. Subsequent Play changed false to true and preserved foreground. The reload/restored-world log contains resource GUID, obsolete world-field, menu-preset and tool-leak diagnostics; native operation success is not a clean-project certification. Live settings mutation/persistence and rejected-path cases are separate from these observations; synthetic callback tests cover their ABI and cleanup paths.

The final canonical host passed the 20-check maintenance/read-only Play fixture (`work/editor19-live-final/report.json`). Installation during active Play was rejected before writes (`9c0fb2dd8165404b9a67409b3bc436e0`); adapter and installation-manifest timestamps remained unchanged. Native stop `fd437df31416419f8f79b3e63fc547fa` confirmed the transition. An idempotent enable through the native settings/save owner returned `ready` with unchanged port/value (`950dad3cf49148699e14a9e5a1cc45b5`). A missing world returned `open_failed` while reporting the unchanged loaded world, and a `.et` request rejected before dispatch. Port reconfiguration and persistence across Workbench restart were not exercised live in this migration.

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

Exit 0 and report.json passed=true certify the maintenance scope and read-only Play observation. The test toggles and restores focus policy, activates the already authorized Yes-to-All policy and per-site assertion suppression, validates native readback and callback cleanup, checks project identities/paging/mounted roots, and observes an existing or absent World Editor. It does not compile, save, switch projects or enter Play. Offline callback tests additionally exercise both exact profiles' Play state, dirty-world, missing-editor and incompatible-vtable guards.

The live fixture expects an open 1.9 project with at least two distinct addons, readable declarations and at least one loose mounted addon. Missing fixtures fail explicitly. Status success alone does not establish permission-dependent script execution or provoke a real resource-leak event; these scenarios remain outside this scoped test.

The broad [MCP acceptance gate](mcp-acceptance.md) still reports full-tool coverage separately and refuses overall certification while other workflows are untested.

## Update workflow

Native hook inventory now selects the baseline executable's source profile. A limited 1.9 baseline exports only its admitted maintenance RVAs; legacy editor addresses are not misinterpreted as current hooks. Unknown or unspecified profiles fail closed. The frozen build runs callback, focus and discovery regression suites before publishing.
