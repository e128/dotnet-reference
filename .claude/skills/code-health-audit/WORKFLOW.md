---
name: code-health-audit
description: >
  Unified code health audit combining four analysis modes: CRAP score analysis (coverage risk),
  dead code detection (unused types + orphaned packages), duplication scanning (copy-paste detection),
  and suppression review (pragma warning audit). Run one mode or all. Produces severity-grouped
  findings tables with actionable remediation suggestions.
  Triggers on: code health, crap analysis, crap score, dead code audit, find dead code, unused types,
  dup-scan, code duplication, duplicate code, find duplicates, DRY violations, review code suppressions,
  audit pragma warnings, pragma warning disable, suppression cleanup, code hygiene, tech debt audit.
argument-hint: "[crap | dead-code | duplicates | suppressions | all] [project-name]"
---

# Code Health Audit

Unified skill for four code health analysis modes. Run one or all.

## Modes

| Mode | Command | What it finds |
|------|---------|---------------|
| `crap` | `/code-health-audit crap [Project]` | High-risk methods via CRAP score (CC² × (1−cov)³ + CC) |
| `dead-code` | `/code-health-audit dead-code` | Unused public types + orphaned NuGet packages |
| `duplicates` | `/code-health-audit duplicates` | Copy-pasted code blocks + repeated helper patterns |
| `suppressions` | `/code-health-audit suppressions` | `#pragma warning disable` directives — proposes fixes over suppression |
| `solid` | `/code-health-audit solid [Project]` | SOLID principle violations — SRP, OCP, LSP, ISP, DIP |
| `all` | `/code-health-audit all` | Run all five in sequence |

If no mode specified, default to `all`.

---

## Mode: SOLID Analysis

### Workflow
1. Identify the target project (from argument or ask)
2. Read all `.cs` source files in the project (exclude test projects)
3. For each public class/interface, check:
   - **SRP**: Multiple unrelated method groups, mixed concerns (orchestration + data access, auth + notification)
   - **OCP**: Switch/if-chains on type discriminators requiring modification for new variants
   - **LSP**: Overrides that throw `NotSupportedException`/`NotImplementedException`, narrow base contracts
   - **ISP**: Interfaces with >7 methods, implementers with no-op methods
   - **DIP**: `new` instantiation of service types, concrete dependencies instead of abstractions
4. Present findings grouped by principle (SRP → OCP → LSP → ISP → DIP)
5. Suggest refactoring pattern for each finding (Extract Class, Replace Conditional with Polymorphism, etc.)

### Key Rules
- Balance with YAGNI — don't flag single-implementation interfaces or simple utility classes
- Don't flag DI guard patterns (`?? throw`) as SRP violations
- Classes under 100 lines with cohesive methods rarely violate SRP — skip them
- See [SOLID Design Principles](lode/coding-standards.md#solid-design-principles) for the full reference

---

## Mode: CRAP Analysis

See [references/crap-formula.md](references/crap-formula.md) for the correct formula, C#-specific complexity counting (async/await IL inflation, LINQ), and coverage thresholds.

### Workflow
1. Identify the target project (from argument or ask)
2. Read all `.cs` source files in the project
3. For each public method, count decision points (CC): `if`, `else if`, `case`, `&&`, `||`, `?:`, `??`, `?.`, `catch`, `when`, `for`, `foreach`, `while`
4. Estimate branch coverage from the test suite (read test files, map to source methods)
5. Apply formula: `CRAP = CC² × (1 − cov/100)³ + CC`
6. Present methods sorted by CRAP score, grouped into risk bands (Low <5, Moderate 5-30, High >30)
7. Suggest highest-ROI test additions for Moderate-band methods

### Key Rules
- Use **source-level CC**, not IL-level (avoids async state-machine inflation)
- CC > 30 = refactor mandatory (no amount of testing rescues it)
- `??` throw patterns in DI guards inflate CC but aren't real business complexity — note this

---

## Mode: Dead Code

### Workflow
1. Enumerate all public types (class, record, struct, interface, enum) in `src/` projects
2. Grep for references outside the declaring file across `src/` and `tests/`
3. Types with 0 external references = dead candidates
4. Filter out: DI-registered types, reflection-used types, IVT-shared types
5. Present findings grouped by project
6. On approval: `git rm` dead files, verify build + tests, check for orphaned NuGet packages

### Key Rules
- Never delete from test projects (reflection/discovery usage)
- Always check DI registration before flagging (`services.Add*`, `builder.Services`)
- Verify build after each project's deletions

---

## Mode: Duplicates

### Workflow
1. **Token scan**: Run CPD if available (`pmd cpd --minimum-tokens 60 --language cs`). If CPD not installed, skip to structural pass.
2. **Structural pass**: Spawn agents to find repeated private helpers, identical test setup patterns, and repeated LINQ/guard patterns
3. Merge and rank findings by `lines × copies`
4. Present ranked table with suggested extraction targets
5. On approval: extract one at a time, build-verify after each

### Key Rules
- Extracted code must be semantically identical to all copies
- Test helpers stay in test projects
- New public types require explicit approval

---

## Mode: Suppressions

See [references/rule-catalog.md](references/rule-catalog.md) for rule details and [references/suppression-templates.md](references/suppression-templates.md) for remediation plan format.

### Workflow
1. Grep all `#pragma warning disable` directives across `*.cs`
2. Catalog by rule category (CA/IDE/CS/SA) with counts and locations
3. Categorize: fixable violations, editorconfig candidates, test exceptions, legitimate suppressions
4. Prioritize: P1 (correctness/security) → P2 (design/async) → P3 (style → editorconfig) → P4 (keep + document)
5. Propose fixes for each suppression (fix root cause > suppress)

### Key Rules
- **Default stance: fix, don't suppress**
- Move recurring test suppressions to `tests/.editorconfig`
- All suppressions require explicit user approval per CLAUDE.md

---

## Output Format (all modes)

```markdown
## Code Health Audit — [Mode]

| # | Finding | Severity | File:Line | Suggestion |
|---|---------|----------|-----------|------------|
| 1 | ... | HIGH | ... | ... |

### Summary
- Findings: N (H high, M medium, L low)
- Actionable: N items
```

## Checkpointing

State file: `.claude/tmp/code-health-audit/state.md`
Intermediate results: `.claude/tmp/code-health-audit/{mode}-findings.md`
