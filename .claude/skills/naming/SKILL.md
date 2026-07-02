---
name: naming
description: >
  Manage C# naming rules and bulk-rename violations. --rule edits .editorconfig,
  --migrate bulk-renames IDE1006 violations.
when_to_use: >
  naming rules, show naming rules, edit naming rule, change naming style,
  naming convention, ide1006 rule, naming severity, symbol category, camelCase rule,
  PascalCase rule, prefix rule, _camelCase, s_camelCase,
  naming migration, fix naming, ide1006, bulk rename, rename sweep, fix naming violations.
argument-hint: "[--rule | --migrate] [args]"
---

# Naming Skill

Two modes for managing C# naming conventions. Mode auto-detected from user prompt.

| Mode | Keywords | Behavior |
|------|----------|----------|
| `--rule` | naming rule, naming style, naming convention, severity, symbol category, prefix, camelCase, PascalCase, `_camelCase` | Read/modify naming rules in `.editorconfig` |
| `--migrate` | fix naming, bulk rename, rename sweep, ide1006, naming violations | Spawn `refactoring-specialist` agent to bulk-rename symbols to match `.editorconfig` rules |

Default: `--rule` if no migrate keyword. `--migrate` when "fix naming", "bulk rename", or "rename sweep" is present.

The two modes are sequential steps: change the rule first (`--rule`), then fix violations (`--migrate`).

---

## Mode: --rule (Edit Naming Rules)

Read and modify C# naming style rules in `.editorconfig`. Front-loads current rules as a table, gates changes behind confirmation, verifies with `check.sh`.

### Step 1 — Load and Display Current Rules

Read `.editorconfig`. Extract all naming-related entries — symbol groups, style rules, and severity lines. Present as a table:

| # | Rule Name | Symbol Category | Required Style | Severity | Applicable Kinds |

Parse from these entry types:
- `dotnet_naming_rule.{name}.symbols = {group}` — links rule to symbol group
- `dotnet_naming_symbols.{group}.applicable_kinds = {kinds}` — which symbols the rule covers
- `dotnet_naming_style.{style}.required_prefix = {prefix}` — the style constraint
- `dotnet_diagnostic.IDE1006.severity = {level}` — enforcement level

### Step 2 — Ask for Change

Use `AskUserQuestion` with options:
- Specific rule to modify (from table row numbers)
- Change IDE1006 severity
- Add new symbol category
- View only (no change)

**Never edit .editorconfig without explicit "approved" from `AskUserQuestion`. This is a hard gate.**

### Step 3 — Apply Change

Edit `.editorconfig` at the specific line identified in Step 1. For new symbol categories, add all three entry types (rule, symbols, style) as a coherent block.

### Step 4 — Verify

```
scripts/format.sh --changed
scripts/check.sh
```

If `check.sh` produces new IDE1006 violations, surface them as a table (file, line, message). Don't auto-fix — present the list and suggest `--migrate` mode.

### Step 5 — Report

Report: "Rule updated. Build clean." or list of violations with file:line breakdown. If violations exist, note: "Run `naming --migrate` to fix IDE1006 violations automatically."

---

## Mode: --migrate (Bulk Rename)

The IDE1006 bulk-rename loop runs as the **`refactoring-specialist` agent** — not inline.
It is a multi-file refactor with build loops, so it belongs in isolated context (keeps the
parent session lean) and should use semantic-accurate renames (Roslyn `find_references` /
IDE rename) rather than text search.

Spawn it via the `Agent` tool with `subagent_type: refactoring-specialist`:

```
Agent(subagent_type: "refactoring-specialist",
      prompt: "Bulk-rename all IDE1006 naming violations to satisfy the .editorconfig naming
               rules. Use scripts/check.sh to surface violations and scripts/build.sh to
               verify. {any specifics the user gave, e.g. a target project or symbol}")
```

The agent collects violations, renames each symbol across `src/` and `tests/`, loops the
build until clean, and returns a migration report. Relay its report to the user verbatim —
do not re-run the rename inline.

**When this mode fires:** "fix naming", "bulk rename", "rename sweep", or an explicit
`/naming --migrate`.
