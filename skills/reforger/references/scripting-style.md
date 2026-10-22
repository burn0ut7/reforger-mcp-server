# Enforce Script coding conventions

Read before authoring or revising Reforger scripts, including generated code and examples. These are Enforce conventions, not C#/.NET or Java rules. Readable output is part of completion.

## Sources and judgment

Based on the official wiki's captured revisions (browser capture 2026-10-20; checked 2026-10-22): [Conventions, revision 371014](https://community.bistudio.com/wiki?title=Arma_Reforger:Scripting:_Conventions&oldid=371014), [Best Practices, revision 370955](https://community.bistudio.com/wiki?title=Arma_Reforger:Scripting:_Best_Practices&oldid=370955), and [Do's and Don'ts, revision 370911](https://community.bistudio.com/wiki?title=Arma_Reforger:Scripting:_Do%27s_and_Don%27ts&oldid=370911). Conventions' *Spacing*, *Method* and *Example* sections and the paired Do/Don't snippets provide concrete examples. These revisions establish the guide's provenance, not compatibility with every installed game build.

Use Conventions for style, Best Practices for readability judgment, and installed sources for current signatures and integration. Read paired examples with their labels. Some wiki examples omit tags/separators or contain unrelated inconsistencies; follow the explicit convention rather than reproducing every detail of a snippet. Check current indexed guidance when a rule or API is uncertain. Do not substitute C#, SQF, or DayZ assumptions.

## Formatting

- Indent with actual tabs, displayed as four spaces. Use Allman braces: opening and closing block braces on their own lines.
- Put spaces around binary operators and inheritance/`foreach` colons. Put a space after control keywords and `for` semicolons: `if (isReady)`, `for (int i; i < count; i++)`. Calls use `UpdateState(value)`; do not pad the inside edges of parentheses.
- Use the official method separator: `//` followed by 96 hyphens, as in the example below. Document with `//!` and Doxygen tags when the contract needs explanation.
- Use parentheses with `new`: `new TAG_Request()`. Empty arrays may use `{}`. Do not turn initializer braces into statement blocks.
- Readability defaults for this skill: one statement per line (except a `for` header), a space after commas, blank lines between logical steps, and no trailing whitespace. Put guard returns on their own indented lines. Simple unbraced guards are supported by official examples; use braces for multi-statement or ambiguous branches.
- Split long expressions or argument lists at logical boundaries. Do not invent a mandatory line-length limit or compress logic to meet one. Do not run a generic C/C++ formatter over Enforce without checking language support and its result.

## Names and organization

| Element | Convention |
|---|---|
| New class/file | Existing unique creator tag and PascalCase: `TAG_SupplyBudget` in `TAG_SupplyBudget.c`; do not appropriate Bohemia's `SCR_`. |
| Component/entity | End the name with `Component` / `Entity`; use singular combined nouns, e.g. `TAG_NotificationComponent`. |
| Enum | `TAG_ESupplyState`; values such as `AVAILABLE`, `IN_USE`. |
| Method | Descriptive PascalCase, e.g. `TryConsumeSupply`. |
| Local/parameter | Descriptive camelCase, e.g. `requestedAmount`; `i` is fine for a short loop index. |
| Instance/static field | `m_` / `s_`, then the applicable type prefix and PascalCase: `m_iSupplyCount`, `s_bEnabled`. |
| Constant | `MAX_SUPPLY_COUNT`; no member/static/type prefix. |

Type prefixes: `b` bool, `i` int, `f` float, `s` string/ResourceName/LocalizedString, `e` enum, `v` vector, `a` array/Curve, `m` map. Objects, typename, set, and Color have no extra type letter: `m_Owner`, `m_AllowedTypes`. Avoid globals; necessary globals use `g_` and a creator tag.

In a `modded class`, tag newly introduced methods and fields to avoid collisions. Preserve the original class name and exact inherited callback/override signatures. Never rename a serialized attribute, RPC, reflection target, registered JSON field, resource reference, or public API solely for style without checking and updating its consumers. Document compatibility exceptions; a naming cleanup can change behavior.

Prefer protected implementation details and expose intentional public operations. Follow the official declaration grouping: attributed fields, other instance fields, static fields, constants; within each group public, protected, private. Method ordering is general methods, EOnFrame, EOnInit, constructor, destructor where present. Do not add empty lifecycle methods to fill this order.

Prefer one main class/enum per file; closely related small helpers and companion ComponentClass declarations can share a file. Use the module that owns the behavior: gameplay normally belongs under `scripts/Game`, while Workbench extensions belong in their configured editor module. Do not move all `.c` files into Game just because a gameplay example lives there.

## Readability and behavior

Choose names that explain purpose, explicit known types, narrow variable scope, and straightforward control flow. Use early exits when they reduce nesting. Extract repeated meaningful behavior, not a framework for a small task. Explain non-obvious intent, ownership, units, authority, or constraints in comments; avoid narrating obvious assignments.

Style edits must preserve evaluation order, side effects, strong/weak references, lifecycle/event masks, and multiplayer authority. Do not reorder checks, cache changing values, remove initialization, or add `ref` as cosmetic cleanup. Verify such changes separately when they are actually needed.

## Example

Original style illustration, not a compiled gameplay integration. Replace `TAG` with the established addon tag. It shows tabs, separators, descriptive names, a guard and an explicit public contract without engine dependencies.

```c
class TAG_SupplyBudget
{
	protected int m_iRemainingSupplies = 12;

	//------------------------------------------------------------------------------------------------
	//! Returns the number of supplies still available.
	int GetRemainingSupplies()
	{
		return m_iRemainingSupplies;
	}

	//------------------------------------------------------------------------------------------------
	//! Consumes a positive amount only when sufficient supplies remain.
	//! Returns false without changing the budget when the request is invalid.
	bool TryConsumeSupplies(int requestedAmount)
	{
		if (requestedAmount <= 0 || requestedAmount > m_iRemainingSupplies)
			return false;

		m_iRemainingSupplies -= requestedAmount;
		return true;
	}
}
```

## Before delivery

Inspect the saved code, including emitted scripts rather than only their generator:

1. Check tabs, Allman blocks, operator/control spacing, method separators, and readable statement/paragraph breaks. EditorConfig controls whitespace defaults; it does not enforce the full convention.
2. Check names, type prefixes, creator tags, visibility, file/module placement, and preserved external contracts. State concrete exceptions.
3. Read each changed method as a human: is its purpose clear, are branches easy to follow, and do comments explain the non-obvious parts? Compare relevant official examples without importing unrelated scaffolding.
4. Inspect the diff for accidental behavior changes. Compile and exercise changed behavior through the supported Workbench workflow when editing production scripts; report unavailable checks explicitly. A formatting pass alone does not establish runtime correctness.
