---
name: readme-auditor
description: >
  Audits all README.md files for staleness against current repo state and auto-fixes
  drift. Checks analyzer rule tables, script tables, version snippets, and project
  tables. Used by /yeet and /readme-check as a callable agent (avoids the Skill
  context-replacement issue).
  Triggers on: audit readmes, readme drift, fix readme tables.
tools: Read, Edit, Bash, Glob, Grep
maxTurns: 15
---

Audit all README.md files for staleness and auto-fix drift.

## Workflow

### 1. Discover READMEs and projects

```bash
scripts/solution-inventory.sh --json
```

Gives the README inventory (`readmes`), the solution file, and every project with its
`path` and `packable` flag in one call.

### 2. Audit each README

**src/\*/README.md (packable projects — `projects[].packable == true`):**
- Verify `<Version>` in the `.csproj` matches any install snippet in the README
- Run `scripts/readme-table-diff.sh --analyzer --json` — deterministic set-diff of the rule table against analyzer source. Act only on a non-empty `missing_from_readme` / `extra_in_readme` / `code_fix_mismatches`; `drift: false` means every diagnostic ID is present and the "Code Fix" column matches actual `CodeFixProvider` coverage

**scripts/README.md:**
- Run `scripts/readme-table-diff.sh --json` — deterministic set-diff of documented vs. on-disk scripts (public + `internal/`). Act only on a non-empty `missing_from_readme` / `extra_in_readme`
- Read the README and verify documented flags match actual script `--help` / argument parsing

**Root README.md:**
- Cross-reference project table against `solution` + `projects[].path` from step 1
- Cross-reference script table against `scripts/help.sh`
- Check .NET version against the global.json SDK pin (`scripts/sdk-version.sh`) or `Directory.Build.props`

### 3. Apply fixes

Apply corrections directly with Edit. Only ask for approval on structural changes
(adding/removing entire sections).

### 4. Report

```
README Audit: N files checked, M fixes applied
  - src/E128.Analyzers/README.md — updated rule table (2 new rules)
  - scripts/README.md — PASS
  - README.md — updated project table
```

## Rules

- **Use `rg` for content searches, `fd` for file discovery** — never use `grep` or `find`
- **Auto-fix drift** — apply corrections directly for data-driven tables
- **Don't restructure** — match each README's existing style
- **Analyzer README is NuGet-facing** — accuracy is critical
