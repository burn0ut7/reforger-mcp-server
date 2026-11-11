# Lobby2 Briefing Content and Action Design

## Status

Design, research, and implementation-status document.

The first data-backed text slice is implemented. `GC_Briefing` is an add-on-owned
`BaseContainer` authored on `GC_LobbyManager`; it supports automatic plain or rich
text detection, one-based order/index identity, and a faction-key allowlist. The manager
replicates the complete current set in its JIP snapshot and broadcasts reliable
server-authoritative upserts and removals during a session. The briefing UI now
renders this replicated data.

The heterogeneous document-block model and general data-driven action definitions
described below are still future slices. The first authored briefing is a labeled
showcase of every currently supported native and Lobby2 formatting tag. It also
contains an `<action id="debug_print">...</action>` proof tag inside a normal
paragraph. The renderer turns that paragraph into wrapping text runs and overlays
a real button on only the tagged phrase. Clicking it requests the allowlisted
`debug_print` action and writes `GC | Briefing action clicked` to the console.
The showcase and second briefing demonstrate `<marker name="Alpha">...</marker>`.
It captures the marker entity name and renders an interactive phrase. Clicking
first pans to the visible replicated marker, then zooms after that pan completes.
For this prototype, the containing paragraph must otherwise be plain text, while
supported native rich-text formatting may be nested inside the custom tag label.

### Current scrolling contract

- The body content height is measured from the active plain, rich, or interactive
  renderer; it is not padded with a fixed scrollable height.
- Mouse-wheel input over the briefing body is consumed by its scroll component
  and never propagates to map zoom.
- A bottom `V` indicator is visible only while content overflows and additional
  content remains below the current scroll position.
- A non-overflowing body is locked and reset to the top.

### Current image contract

- Reforger's native `<image set="..." name="..." scale="..." />` tag remains
  available for small inline imageset icons. Its scale is relative to the text
  line height, so it flows with surrounding glyphs.
- Lobby2's existing `<img ... />` syntax can also join an inline paragraph by
  adding `inline="true"`. This supports packaged textures and imageset quads:
  `<p><img resource="{GUID}path/icon.edds" inline="true" width="24" /> Text beside the icon.</p>`.
  Inline width and height are optional bounds from 16 through 96. With neither,
  the image is fitted to a 24-unit line-height icon. The aspect ratio is always
  preserved, and the icon wraps with the surrounding paragraph content.
- Lobby2 adds `<img ... />` for larger block images placed between rich-text
  sections. Multiple block images may appear in one briefing body.
- A packaged texture uses
  `<img resource="{GUID}path/image.edds" width="512" height="256" />`.
- An imageset quad uses
  `<img resource="{GUID}path/icons.imageset" name="QuadName" width="128" height="128" />`.
- Width and height are optional layout-unit bounds from 16 through 2048. With
  neither value, the native image size is used. With one value, aspect ratio is
  preserved from that dimension. With both, the image fits inside the requested
  box without distortion. Every image is additionally clamped to the current
  document width and centered with vertical separation from adjacent text.
- Block-image bodies retain native rich-text formatting before, between, and
  after images. They cannot yet contain Lobby2 custom interactive/decoration
  spans (`action`, `marker`, `link`, `u`, `s`, `strike`, or `size`) in the same body;
  malformed or unsupported combinations show a safe validation message.
- Images must resolve to packaged Resource Database assets. Remote URLs and local
  filesystem paths are not loaded.

### Current dynamic scenario contract

- The reserved Lobby2 body token `<scenario />` renders the default `SCENARIO`
  briefing tab.
- `GC_BriefingScenarioComponent` owns Scenario row creation, environment
  formatting, icons, change subscriptions, fallback timing, and teardown. The
  parent briefing view only selects the renderer and measures its content.
- It reads the base-game time-and-weather manager for current weather name and
  description, wind speed and direction, date, military time, sunrise, sunset,
  and moon phase.
- Pre-match weather, wind, and date-time preview changes refresh immediately
  through the base-game time-and-weather change invokers. Weather transitions
  and wind override changes use the corresponding replicated invokers.
- A 30-second fallback updates minute-level clock progression and catches
  naturally changing wind values. It runs only while the Scenario tab is
  selected in the attached lobby UI; selecting another briefing or leaving the
  lobby removes both the callback subscriptions and timer.
- Unchanged row text and icons are not reassigned during fallback checks.
  Missing icons use a temporary generic scenario icon without changing the
  authoritative scenario state.

### Current backend contract

- Authors enter one briefing-content string with no text-format enum. The UI uses
  rich rendering only when it finds a recognized native or Lobby2 tag; otherwise
  it falls back to normal text. Untagged text surrounding valid tags remains
  ordinary text inside the rich document.

- `SetBriefing(order, ...)` creates or completely replaces the briefing at that
  one-based order/index on the server.
- `UpsertBriefing(...)` accepts a populated `GC_Briefing` on the server.
- `SetBriefingVisible(order, visible)` shows or hides an existing briefing on the
  server without replacing its content or faction targeting.
- `RemoveBriefing(order)` removes a briefing on the server.
- Authors do not provide a separate briefing ID. The authored `order` is the
  stable lookup and selection key, while the backing array remains an
  implementation detail whose positions may shift after removal or filtering.
- Mutations are rejected on non-authority instances and invalid payloads are
  rejected before broadcast.
- An empty faction-key array is visible to every player, including factionless
  players.
- A populated faction-key array is visible only when the player's current
  authoritative base-game faction key appears in that array.
- A factionless player cannot match a populated faction-key array.
- `Show At Game Mode State` is authored with the extended `SCR_EGameModeState` enum
  (`PREGAME`, `SLOTTING`, `BRIEFING`, `GAME`, or `POSTGAME`). It is the earliest
  phase from which the briefing is shown. `Invert Game Mode State` reverses that
  threshold rule.
- The runtime visibility bool remains replicated but is intentionally hidden
  from prefab authors. Server scripts control it through
  `SetBriefingVisible(order, visible)`.
- Effective visibility requires the authored game-mode state, runtime visibility,
  and faction eligibility to all pass. The replicated base game-mode state is
  the authoritative visibility input.
- UI selection is local presentation state and is not replicated.
- Briefing title buttons use single selection. Selecting a title shows its body,
  selecting another switches the body, and selecting the active title again
  collapses the view to its topic rail without changing replicated briefing
  visibility.
- The briefing view is authored as a reusable layout and instantiated by the
  separate `Lobby_Preview.layout`. It is not owned by `Lobby_Slotting.layout`.
- The reusable view is host-screen agnostic: it resolves only widgets inside its
  own layout root and subscribes directly to the Lobby2 briefing backend. Preview,
  the dedicated Briefing screen, or another Lobby2 screen may instance the same
  layout without adding briefing behavior to that screen's menu class.
- Preview hosts the view as a bounded left-side panel rather than a fullscreen
  overlay: a compact topic rail opens an adjacent readable document column while
  leaving most of the map visible.
- The reusable view owns its compact and expanded widths. With no selected topic,
  it contracts to the 287-pixel topic rail; selecting a topic expands it to 720
  pixels for the document column, and selecting that topic again contracts it so
  the host screen regains the unused space.
- Visibility is authored per briefing, replicated in JIP snapshots, and included
  in reliable mid-session briefing updates. Hidden briefings remain replicated but
  are omitted from the visible UI list.
- The backend currently accepts up to 32 briefings; the first UI slice has four
  authored title-row slots and displays the first four visible entries.

## Goal

Lobby2 briefings should support highly customizable, formatted mission information and interactive links. A briefing author should be able to select a supported action, pass parameters to it, and let Lobby2-owned script code perform navigation or dispatch further events.

The flexibility boundary is intentional:

- Briefing content may request a registered action and supply data.
- Briefing content must not select an arbitrary class, method, script file, or expression to execute.
- The registered handler remains responsible for validation, lifecycle, multiplayer authority, and the resulting behavior.

This produces nearly the same authoring flexibility as direct script invocation without turning mission content into executable code.

## Research findings

### Native rich-text support

Reforger's `RichTextWidget` is a multiline `TextWidget` that parses an HTML-like markup language. It is not a general HTML renderer or embedded browser.

The documented formatting includes:

- bold and italic text;
- foreground color by RGBA, hexadecimal value, or supported color name;
- SDF-font outline and text/image shadow;
- packaged font resources;
- left-, center-, and right-aligned paragraphs;
- headers with predefined or explicit scale;
- explicit line breaks;
- images from packaged image sets;
- Unicode codepoints;
- representations of configured input actions and individual keys.

Lobby2 adds the common inline decorations that native RichText does not expose:

- `<u>underlined text</u>`;
- `<s>struck text</s>` or `<strike>struck text</strike>`;
- `<size scale="1.25">scaled inline text</size>`, bounded to `0.75` through
  `2.0` times the briefing body size.

Important constraints:

- Bold, italic, outline, and full Unicode behavior depend on SDF font support.
- A font named in markup must be a packaged font resource. Missing fonts fall back to the system font.
- Forcing a font disables language-specific substitution and can prevent localized glyphs from rendering. It should be exceptional rather than the default.
- Inline images come from packaged image sets. The rich-text format does not load remote images.
- Markup tags must be properly nested and must not overlap.
- Long, segmented, wrapped text can be expensive to refit. Runtime resizing and frequent text replacement increase that cost.

The base game also contains `SCR_RichTextTags.TagColor`, which emits the documented `<color rgba="...">...</color>` form. This confirms that the markup is used by game script, not only by the layout editor.

### Plain-text support

Plain text is a first-class briefing format. Authors must not be required to write rich-text markup for ordinary paragraphs, titles, labels, or action captions.

Lobby2 should use one document system and one text-block model rather than separate plain-document and rich-document systems. Each text block contains one `content` value and may optionally declare its format:

```text
- type: text
  content: Enemy forces occupy the radio station.

- type: text
  format: rich
  content: <b>Warning:</b> Enemy forces occupy the radio station.
```

If `format` is omitted, the block is plain text. Rich text is opt-in with `format: rich`. This default keeps simple authoring simple and prevents ordinary text containing markup-like characters from unexpectedly changing presentation.

This is still one rendering pipeline. The renderer may use a normal text widget for plain blocks and a rich-text widget for rich blocks, or safely encode plain content before passing it to a rich-text widget. That is an internal rendering choice, not a second content system.

Do not infer rich text merely because the content contains angle brackets or resembles a supported tag. Automatic tag detection makes typos, technical notation, localization, and runtime-supplied strings ambiguous. The declared format determines whether markup is interpreted.

### Rich-text actions are display-only

The rich-text `<action>` element inserts the text or icon for a configured input binding. The `<key>` element similarly inserts a key representation. Neither element is an interactive hyperlink and neither invokes script.

The documented rich-text format does not provide:

- an HTML-style `<a href>` element;
- an `onclick` attribute;
- a callback associated with an individual text span;
- JavaScript, CSS, or arbitrary HTML;
- embedded Enforce Script expressions;
- Markdown parsing.

### Lobby2 interactive tags

Lobby2 preprocesses its HTML-style tags before the remaining content is
given to `RichTextWidget`. They are Lobby2 document syntax, not native Reforger
rich-text elements:

```text
<action id="debug_print">click here</action>
<marker name="Alpha">view entity Alpha</marker>
<link url="https://community.bistudio.com/wiki/Arma_Reforger">Arma Reforger Wiki</link>
<u>underlined text</u>
<s>struck text</s>
<size scale="1.35">larger text</size>
```

Reforger also defines a native `<action>` tag for displaying an input binding.
The attribute disambiguates the two forms: Lobby2 preprocesses
`<action id="...">...</action>`, while native `<action name="..." />` markup is
left for `RichTextWidget` to render.

Native formatting may be nested inside a Lobby2 label as long as the markup is
properly nested:

```text
<action id="debug_print"><color name="orange"><b>click here</b></color></action>
<marker name="Alpha"><color rgba="255,152,50,255">view entity Alpha</color></marker>
<size scale="1.35"><u><color name="orange"><b>larger underlined text</b></color></u></size>
```

The action `id` and marker `name` are limited to 64 characters. Link URLs are
limited to 512 characters and must use `http://` or `https://`, without spaces,
control characters, quotes, angle brackets, or backslashes. An empty label uses
the associated value as its visible label. One custom inline paragraph may contain
multiple action, marker, link, underline, strike, and size spans. Text between those
spans must be plain, while native rich-text formatting may be properly nested
inside each span. Underline, strike, and size wrappers may be nested around each
other. Formatting elsewhere in the briefing remains supported before and after
that paragraph. A malformed Lobby2 tag is not forwarded to Reforger's native
rich-text parser; the view shows a safe validation message instead.

`RichTextWidget` exposes content sizing, line width, line visibility, and elision behavior in addition to inherited text behavior. It does not expose a documented character-range hit-test or clicked-span callback. Deriving a clicked word from cursor coordinates would therefore be fragile and inaccessible to controller users.

### External browser links

The engine declaration `PlatformService.OpenBrowser(string url)` is available. The base game uses it from `SCR_BrowserLinkComponent`, which can resolve a backend link item or open a configured URL from a button component.

Lobby2 therefore renders `<link>` labels as explicit inline button widgets and
opens validated addresses through the platform browser service. They visually
flow with the surrounding rich text but are not native rich-text hyperlinks.

### Current Lobby2 briefing surface

Lobby2 currently authors four briefing-title rows in `UI/layouts/Briefing/Lobby_Briefing_Titles.layout`. `GC_BriefingTitlesComponent` registers those rows by fixed widget name, and `GC_BriefingTitleRowComponent` owns their selection and visual state.

That is sufficient for a first briefing-body slice. A later customizable-section slice should replace the fixed registration list with data-driven row creation rather than expanding the hard-coded pattern.

## Decision: formatted documents plus registered actions

The briefing is modeled as a document made of presentation blocks and action blocks.

Suggested block categories:

| Block | Purpose | Suggested rendering surface |
| --- | --- | --- |
| Text | Plain paragraphs by default; optionally rich headings, warnings, inline icons, and formatted values | Text or rich-text widget selected by the renderer |
| Image | Maps, diagrams, photographs, larger illustrations | Image widget in a bounded block |
| Divider or spacer | Document structure | Layout widget |
| Action link | Link-like interactive command | Button widget styled as text |
| Action button | Prominent interactive command | Standard button widget |

A simple conceptual document could contain:

```text
section: situation
title: SITUATION
blocks:
  - type: text
    content: Enemy radio traffic originates from Hill 62.
  - type: text
    format: rich
    content: <color name="orange"><b>Warning:</b></color> Expect resistance.
  - type: actionLink
    label: VIEW HILL 62 ON MAP
    action: focus_map_target
    parameters:
      targetId: hill_62
```

This is a data-model example, not a committed Reforger config syntax.

## Action dispatch model

### Request shape

An authored action request should contain:

- a stable action identifier;
- a collection of serializable parameters;
- optional presentation metadata such as label, icon, tooltip, and confirmation text.

At runtime, Lobby2 adds context that content is not allowed to forge, such as:

- the source briefing and section;
- the local player/controller identity;
- the active Lobby2 phase;
- the source widget;
- whether execution is occurring on a client or authority.

### Registered handlers

The dispatcher resolves the stable action identifier to a Lobby2-owned registered handler. A handler may:

- change local briefing selection;
- open or focus another Lobby2 panel;
- focus a known map target;
- request a permitted server-authoritative operation;
- invoke a Lobby2-owned event or `ScriptInvoker` for decoupled listeners;
- return a handled, denied, unavailable, or invalid result.

Handlers should register when the owning Lobby2 UI or service becomes active and unregister during teardown. The dispatcher and its subscriptions must not remain active when Lobby2 is closed, unloaded, or not the selected game mode.

### Parameters

Parameters provide flexibility, but each handler must define and validate its own schema. Validation should cover:

- required and optional keys;
- expected value types;
- ranges and maximum lengths;
- supported identifiers;
- whether a target currently exists;
- whether the action is legal in the active phase;
- whether the caller has authority or permission.

Unknown parameters should normally be rejected or ignored according to the handler's declared policy. Missing required parameters must fail without partially executing the action.

For common actions, prefer stable identifiers over raw state:

- use `targetId: hill_62` instead of trusting arbitrary world coordinates;
- use `sectionId: objectives` instead of a widget name;
- use `roleId` or another authoritative reference instead of copying faction or group ownership into briefing data.

### Why arbitrary script invocation is excluded

Briefing data must not contain values such as:

```text
class: SomeClass
method: RunAnything
script: scripts/Game/SomeFile.c
expression: GetGame().Something()
```

Direct invocation would make content tightly coupled to implementation names and would bypass validation, phase rules, authority checks, and lifecycle cleanup. It would also make malformed or server-supplied content capable of reaching unrelated game systems.

A registered action can still perform any behavior intentionally exposed by Lobby2. Adding new capabilities means adding a new handler or extending a handler's validated schema, not widening the interpreter.

## Interaction and presentation rules

### Link-like actions

Use real buttons styled as links. A link style should include:

- a distinct normal color;
- hover, focus, pressed, and disabled states;
- controller focus and activation;
- an optional icon indicating navigation, map focus, or external navigation;
- a tooltip or accessible label when the visible label is ambiguous.

The current proof supports one interactive tag inside an otherwise plain `<p>...</p>`
paragraph. That paragraph is rendered as size-to-content word runs in a wrapping
layout. The visible action phrase and transparent button share one bounded overlay
run, so only the phrase is clickable. No screen coordinates or authored offsets
determine its placement; when the phrase approaches the right edge, the wrapping
layout moves it intact to the next line.

Do not position a button by guessing a glyph's coordinates in a monolithic rich
text widget. A supported overlay must own both the visible marker text and the
button in the same layout container. The implemented inline paragraph renderer
uses that custom multi-widget flow rather than guessing positions in a monolithic
rich-text widget.

### External URLs

External URL actions should:

- be explicit rather than disguised as ordinary text;
- accept only approved schemes, normally `https`;
- preferably resolve a known link identifier or validate against an allowlist;
- tell the player that an external browser will open;
- fail gracefully when the platform service is unavailable;
- never accept an unvalidated URL from replicated or server-supplied briefing data.

### Localization and fonts

- Author player-facing strings so they can move to localization resources.
- Keep markup structure separate from translated text when practical.
- Test text expansion, right-to-left presentation where applicable, and non-Latin glyph coverage.
- Do not force a custom font globally unless it contains every required glyph and the localization tradeoff is intentional.
- Package only fonts with suitable redistribution rights.

### Performance

- Put the document in a stable-width scrolling surface.
- Replace content when the selected section changes, not every frame.
- Avoid repeatedly resizing a long wrapped rich-text widget.
- Bound total text length, block count, tag count, and image count for runtime-supplied content.
- Prefer separate image blocks for large images rather than heavily segmenting one rich-text string.
- Cache parsed or resolved document data when profiling shows a need; do not introduce speculative caching in the first slice.

## Multiplayer and authority

Briefing rendering and navigation are normally client presentation. A client may freely change its selected section or local map view.

An action that changes authoritative game state must be treated as a request, not as permission. The authoritative system must independently validate:

- the requesting player;
- the active Lobby2 phase;
- the referenced base-game player, group, faction, character, or mission object;
- permissions and current state;
- replay or repeated-click behavior.

Briefing data must not become a competing source of faction membership, group membership, player ownership, or character affiliation. It should reference stable identifiers and resolve current relationships through the authoritative base-game systems.

Purely local actions should not be replicated. When all clients can derive a document from the same resource and authoritative mission state, replicate the compact state or identifiers rather than the fully rendered markup.

## Supported and unsupported capability policy

### Supported

- Plain text as the default text-block format.
- Documented rich-text markup.
- Packaged fonts and image-set images.
- Data-driven briefing sections and blocks.
- Link-styled and conventional buttons.
- Registered action IDs with validated parameters.
- Lobby2 `<action>` tags with native rich-text formatting inside labels.
- Lobby2 `<marker>` references that resolve a marker visible to the current
  player, pan to it, and then zoom to frame its authored-size footprint.
- Handlers that dispatch further Lobby2-owned events.
- Local navigation and presentation actions.
- Explicit, validated requests to authoritative systems.
- Approved external browser links through an explicit action.

### Not supported

- Arbitrary class or method names in briefing content.
- Script paths or executable expressions in briefing content.
- Reflection-like dispatch based on authored strings.
- Native clickable spans inside `RichTextWidget` unless a future verified engine API provides them.
- Mouse-coordinate guessing over formatted text.
- Coordinate-positioned button overlays on words inside a monolithic rich-text widget.
- JavaScript, CSS, general HTML, or embedded webpages.
- Remote images or arbitrary runtime resource paths.
- Unvalidated external URLs.
- Briefing-owned copies of authoritative faction, group, or player relationships.
