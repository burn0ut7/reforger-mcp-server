# Enforce Script conventions

Use installed declarations and callers for current signatures and integration. These authoring conventions apply to Enforce, including generated scripts; C# and Java retain their own conventions.

## Formatting

- Actual tabs, displayed as four spaces; Allman block braces.
- Spaces around binary operators and inheritance/`foreach` colons, after commas and control keywords: `if (ready)`, `for (int i; i < count; i++)`. Calls: `UpdateState(value)`.
- Method separator: `//` followed by 96 hyphens. Use `//!` documentation where the contract needs explanation.
- Constructor parentheses: `new TAG_Request()`. Empty arrays may use `{}`.
- One statement per line except `for` headers; guard returns on their own indented lines. Simple unbraced guards are fine; brace multi-statement or ambiguous branches.
- Blank lines between logical steps, no trailing whitespace; split long expressions naturally. Check formatter support for Enforce before applying it.

## Names and structure

| Element | Convention |
|---|---|
| Class/file | Existing creator tag + PascalCase: `TAG_SupplyBudget.c`; reserve `SCR_` for Bohemia. |
| Component/entity | Suffix `Component` / `Entity`. |
| Enum/value | `TAG_ESupplyState` / `AVAILABLE`. |
| Method | Descriptive PascalCase. |
| Local/parameter | Descriptive camelCase. |
| Instance/static field | `m_` / `s_`, type prefix, PascalCase. |
| Constant | `MAX_SUPPLY_COUNT`, no member/type prefix. |

Type prefixes: `b` bool, `i` int, `f` float, `s` string/ResourceName/LocalizedString, `e` enum, `v` vector, `a` array/Curve, `m` map. Objects, typename, set and Color have no extra type letter. Necessary globals use `g_` and a creator tag.

Tag new members in `modded class`; preserve inherited names/signatures and serialized, RPC, reflection and public contracts. Coordinate consumer changes for intentional renames.

Prefer protected implementation details. Group attributed fields, other instance fields, static fields, then constants; within each, public/protected/private. Method order: general methods, EOnFrame, EOnInit, constructor, destructor where present. Avoid empty lifecycle scaffolding.

Prefer one main class/enum per file; small companion classes may share it. Use the owning script module—gameplay and Workbench extensions have different homes.

## Readability check

Inspect the saved output, including generated scripts: formatting, names, module placement, clear control flow and comments explaining non-obvious intent. Keep scope narrow and extract meaningful repeated behavior. Preserve evaluation order, side effects, reference strength, lifecycle/event masks and authority during style edits. Note compatibility exceptions. Compile changes and select proportionate tests using the [Workbench workflow](workbench-workflow.md); readability alone does not establish correctness.
