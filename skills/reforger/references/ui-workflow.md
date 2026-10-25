# Reforger UI: task, concept, implementation, critique

Use this workflow for player-facing UI work only. The target is the simplest complete interface for the player's task, with consistent presentation and understandable interactions. Visual decoration is secondary. A narrow wording or focus fix needs the relevant checks, not a fresh redesign; open and preserve an applicable existing reference. For new UI or substantial redesign, complete the concept stage before production edits.

## 1. Establish the task and evidence

Identify who uses the UI, what they are trying to accomplish, how they enter it, what information they need to decide, and how they finish or leave. State the supported input devices, viewport/UI-scale conditions, and gameplay context. Reuse known project requirements rather than asking again.

Inspect the affected addon and its dependencies, existing theme/components, and the nearest base-game equivalent. Search related authored mods for useful patterns; inventory all relevant addons for a cross-mod audit, but do not repeat a global audit for each button change. Distinguish production, experiments, copies, and generated files. Record what was actually inspected, including absent coverage. Use [research findings and source entry points](ui-research.md) to start, then verify against the installed version with `pak_search`, `pak_search_text`, and `pak_read`. Preserve source/archive revision and distinguish cached source from live behavior.

Trace the owning widget/component APIs, their base classes, real usage, and referenced layouts/input configurations in installed sources. Source can inform design but cannot validate the running game. Check current connected MCP capabilities before promising runtime tests.

Classify the surface deliberately: an informative display/HUD should not capture gameplay input; a menu supports interaction; a dialog requests a response. Inspect an existing screenshot of the affected screen, or capture a baseline through MCP. A source file or mockup is not that baseline. Record missing live evidence without manufacturing a substitute.

## 2. Remove unnecessary information and decisions

Write the normal player journey as a short sequence: entry → understand current state → choose/change → act → see result → return. Add only relevant branches: no data, invalid choice, waiting, failure, lost permission, cancellation, changed target. A HUD has a reading/recognition journey instead of a primary button.

Before layout or image generation, give **every visible element** a reason. Keep a compact table in the reference brief:

| Element or repeated group | Player question/action served | Why here and why visible now | State/data owner | Keep, combine, defer, remove |
|---|---|---|---|---|

Include headings, descriptions, badges, dividers and icons as well as controls. Repeated rows can share a rationale; repeated messages cannot hide behind one. Remove anything whose only justification is filling space or looking technical. A divider can clarify grouping; a badge must communicate useful state. Do not remove needed context merely to reduce a widget count.

- Give each fact one main home. A title, subtitle, card and footer should not all repeat the same instruction. Retain repetition only when it prevents a concrete error, such as identifying the affected target at a consequential action; document why.
- Start additions inside the existing composition. Reuse a row, header or control before adding a new container. Preserve existing backgrounds and faction accents when they support contrast, grouping or identity; reducing duplicate labels does not imply removing those surfaces. Do not turn a small state change into a new dashboard section.
- When one recognizable control can both show and change a state, prefer that control over a separate status label and action button. Preserve clear hit areas, focus and permission feedback. For example, an authorized commander can toggle a readiness check icon beside the faction and CO names; duplicate Ready/Not ready text and a Mark ready button are unnecessary when the icon already communicates the state.
- Show controls relevant to the current task/state. Put genuinely occasional options behind a clear disclosure, without hiding the common next step or making users hunt for essential context.
- Prefer one visually dominant next action per task, with subordinate alternatives and an obvious exit. Label actions with their outcome; identify target, direction, units and scope where ambiguity matters. Selecting a target must not silently execute a separate action.
- Use the appropriate control: boolean toggle, exclusive choice, multiple selection, range, text or browsable/searchable list. Define whether edits apply immediately or on confirmation. Do not add an Apply button to an immediate setting without a reason.
- Separate current state, intended action and confirmed result. Show waiting honestly, keep recoverable input on failure, and explain what enables a disabled action. Do not announce success from a dispatched request.
- Keep implementation details, IDs and debug counters out of player UI unless needed by that user for this task. Put secondary explanation near the relevant control or in accessible help; do not hide essential instructions in hover-only content.

Record a small theme vocabulary from the project: surface, primary/secondary text, accent/selection, warning/error; type hierarchy, spacing, borders, control sizes and state treatments. Prefer neutral surfaces and one main accent. Additional semantic colors need consistent meanings and non-color cues. Reuse shared styles instead of inventing slightly different colors per widget. Simple does not mean tiny text or missing focus feedback.

## 3. Generate and critique the visual target

Reuse a suitable saved concept for minor revisions. Otherwise **generate an image of the proposed UI in its intended context**, after the journey and element rationale are settled. Use the image-generation capability and its available instructions. Prefer a real MCP screenshot as the visual input for an overlay, preserving the gameplay context; for a full-screen menu show the actual intended viewport composition. Label an invented background as illustrative, never game evidence. Include actual proposed wording and representative data lengths, rather than placeholder dashboards.

The prompt must specify the task, screen type, hierarchy, approved elements, theme, viewport, selected/focused state, primary action and exit, plus exclusions from the element audit. Show additional state frames only when one image cannot explain a material interaction. An interactive visualization can help reason through navigation; it does not replace the saved image or actual game testing. For motion, add labeled storyboards or video and verify timing later with actual motion evidence.

Respect any explicit model preferences for creative direction and review; disclose unavailable model/media controls instead of claiming that setup. Do not switch models implicitly.

Save actual media outside runtime import folders, normally `Art/References/<ui-name>/concept-v1.png`, with `brief.md` recording prompt, tool/source, exact active version, element rationale, theme, defining traits, relevant states and eventual runtime paths. Preserve earlier versions. Open the saved media, pass it back to tools accepting references, and show it to the user. Continue authorized work without an extra approval pause.

Critique the concept before building: can the player identify what matters, the next action, its target/result and the way back? Is any text redundant? Are hierarchy, legibility, grouping and control semantics coherent? Remove unnecessary elements and regenerate when needed. A beautiful image with a confusing journey fails. Check generated text and impossible controls. Record intentional changes to the reference; do not quietly move the target to excuse a poor implementation. If media is unavailable, report the concept gap and stop dependent visual production; prose alone does not satisfy this stage.

## 4. Build with Reforger's existing UI mechanisms

Read the relevant official guides, not every UI guide:

| Need | Guide / implementation cue |
|---|---|
| Surface, layout, menu registration | **Layout Creation**; verify resource identity, preset/config/class linkage and input context in the installed sources. |
| Sizing, anchoring, focus, clipping | **Resource Manager: Layout Editor**; inspect widget hierarchy and navigation, then test actual viewport behavior. |
| Ordinary controls | Existing WLib prefabs/components; **Modular Button Component Tutorial** for state effects and event propagation. Avoid parallel custom click/focus systems. |
| Confirmation or task dialog | **Dialog Configuration Tutorial**; consider configurable presets and a separate content layout before a bespoke dialog framework. |
| Input and contextual help | **Input Manager**, **Widget Tooltip Setup**; use named actions, appropriate contexts and available device hints. |
| Player-facing strings | **Mod Localisation**, **String Editor**; use project string tables, build runtime tables, and test resolved strings and longer text. |

Trace which component owns each value. For example, `SCR_ButtonTextComponent` writes its configured text into its child; editing that child's initial text can be overwritten. Read the actual component/API before editing. Shared components are preferable to many local overrides, but do not change a shared base's scope unintentionally.

Keep layout responsible for presentation, UI controller for presentation state, and gameplay services/authority for the action. A hidden or disabled button is not a permission check. Use stable target identities behind display names; refresh eligibility when data changes. Subscribe and unsubscribe input/events in the corresponding lifecycle, release focus/cursor and owned widgets, and restore gameplay after close. Gate project-owned shared overrides on the active mode and test isolation when applicable.

Use native sizing/layout containers where appropriate; retain readable text, wrapping and scrolling instead of shrinking everything to fit. Preserve selection/focus through list refreshes deliberately. Use event-driven updates for discrete state; timers/animation may need bounded updates. Avoid rebuilding unchanged widget trees every frame. Follow [scripting conventions](scripting-style.md) for touched code.

## 5. Run the screenshot and interaction loop

Define observable pass criteria **before** judging output. Match the task and reference, including concrete labels/scope, important geometry, visible states, supported viewport/input conditions and relevant performance baseline. Do not invent arbitrary scores or choose thresholds after results.

Use [Workbench guidance](workbench-workflow.md). All game/editor operations, navigation and captures go through Reforger MCP. Discover supported contracts; a window action is not automatically a game-widget interaction. A typed runtime invocation can test a handler but cannot prove hit areas, focus traversal or that a player can reach it. Missing input/capture capabilities leave those checks unverified; do not substitute desktop injection. Repair missing capabilities in their owning MCP implementation when authorized, and preserve project-specific test fixtures in the optional test addon.

Iterate: **implement → open actual UI → exercise journey → capture → compare → critique → fix → recapture**. Inspect the full screen at its real viewing size as well as detail crops. Capture representative initial, focused/selected, pending, result and failure states as relevant. Test a nominal and constrained supported viewport/UI scale; long names/text, empty and populated/scrolling lists; bright/dark gameplay backgrounds for overlays; keyboard/mouse and gamepad where supported. Record unsupported coverage, not a pass.

Check entry discoverability, logical focus order, visible selection versus focus, scrolling, back/cancel, input-mode changes, safe dismissal and reopen. Verify one activation causes one intended action, targets/scopes match, errors recover, and gameplay input returns. Check client/server result and permissions for networked actions. Test mode changes and unrelated-mode behavior for shared overrides. Repeated open/close must not accumulate listeners, widgets or duplicate actions. Measure UI update/frame/loading costs against a comparable baseline when the change affects those costs.

Save original MCP captures and receipt/log/state evidence outside runtime imports. Record build/source revision, project/mode, viewport/UI scale, input device, steps and state for each comparison. Preserve failures. Screenshots prove appearance, sequences or motion evidence show transitions, and state/log checks establish effects; none alone proves everything.

For each critique, name the element, expected behavior/trait, observed issue, player impact and evidence. Compare with the **exact active concept**, not memory. Check composition, proportions, spacing, typography, colors, control states and wording. Intentional native adaptations need a stated reason and review. Fix material mismatches and rerun the affected journey; do not add explanatory text to compensate for a confusing control.

## 6. Independent acceptance

After implementing and validating substantive work, ask a fresh independent reviewer (`fork_turns="none"`) to critique the whole result. Supply the user's request, complete changes, active saved media, relevant references and raw evidence, including failures and limitations, without the implementer's opinions or preferred verdict. Let the reviewer decide what needs deeper examination.

The review should address whether the UI serves the player, makes sense to use and justifies its complexity. For visual judgments, open the saved concept and actual rendered evidence and explain whether the defining traits are preserved. Missing screenshots, interaction or motion evidence is a verification gap, not a pass; generated previews and source inspection cannot establish those results.

Work through the feedback, validate changes, and return the updated work and evidence to the same reviewer until approved. A concise handoff links the active concept, final screenshots and test results, explains intentional departures, and states remaining limitations. Do not claim the UI is good merely because it compiles or looks better than before.
