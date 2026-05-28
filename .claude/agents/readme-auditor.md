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

### 1. Discover READMEs

```bash
fd README.md --type f --exclude obj --exclude bin --exclude .git
```

### 2. Audit each README

**src/\*/README.md (analyzer projects):**
- Verify `<Version>` in the `.csproj` matches any install snippet in the README
- Run `scripts/analyzer-stats.sh --json` to get all diagnostic IDs and code fix coverage
- Verify every diagnostic ID appears in the rule table
- Verify the "Code Fix" column matches fix provider coverage

**scripts/README.md:**
- Run `scripts/help.sh` and compare every script in the README against actual output
- Verify `internal/` script table matches `ls scripts/internal/*.sh`

**Root README.md:**
- Cross-reference project table against the solution file
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
