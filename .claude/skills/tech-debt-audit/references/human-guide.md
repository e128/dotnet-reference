# Tech Debt Audit — Human Guide

Installation, usage, philosophy, and adaptation notes. Not part of the audit protocol.

## Installation

Project-level install (just this repo):

```bash
mkdir -p .claude/skills/tech-debt-audit/references
# Place SKILL.md at .claude/skills/tech-debt-audit/SKILL.md
# Place references/ at .claude/skills/tech-debt-audit/references/
```

## Usage

In Claude Code, in the .NET repo you want audited:

```
/tech-debt-audit                    # Full repo audit
/tech-debt-audit src/Core           # Scope to specific module
```

Output goes to `plans/TECH_DEBT_AUDIT.md`. First run takes 5–20 minutes depending on repo size; subsequent runs in repeat-run mode are faster.

## Philosophy

Most "code review" prompts produce a bulleted list of generic best-practice violations dressed up as findings. This skill is built to avoid that failure mode. Three design choices do most of the work:

**Forced orientation before judgment.** Phase 1 isn't optional decoration. Without a real mental model of the architecture, every Phase 2 finding is just pattern-matching against generic heuristics. Reading `git log` for churn data is what surfaces the files that *actually* have debt versus the files that just look messy.

**File:line citations on every finding.** This is the single biggest quality lever. A finding without a citation is a vibe. Vibes don't get fixed.

**The "looks bad but is actually fine" section is required.** This is the one most people remove when adapting the prompt. Don't. Forcing the model to surface the calls it considered making and chose not to is what separates a real audit from a checklist regurgitation. If that section is empty, the audit is shallow.

The skill also explicitly forbids recommending rewrites and forbids padding categories. Both are common LLM failure modes — rewriting is easier than diagnosing, and padding makes outputs feel thorough when they aren't.

## What you get

`plans/TECH_DEBT_AUDIT.md` looks like this:

```markdown
# Tech Debt Audit — <repo name>
Generated: 2026-05-09

## Executive summary
- 3 CRITICAL, 12 HIGH, 31 MEDIUM, 18 LOW
- Largest debt concentration: src/Core/ (god module, 4 of 3 CRITICAL findings)
- ...

## Architectural mental model
The system is a [...]

## Findings
| ID   | Category           | File:Line                    | Severity | Effort | Description                    | Recommendation         |
| ---- | ------------------ | ---------------------------- | -------- | ------ | ------------------------------ | ---------------------- |
| F001 | Architectural decay | src/Core/Processor.cs:1240  | CRITICAL | L      | 1,400-line god class           | Extract services       |
| ...  |                    |                              |          |        |                                |                        |

## Top 5
1. **F001 — Decompose Core/Processor.cs** ...

## Quick wins
- [ ] F042: Remove unused dep `SomePackage`
- [ ] ...

## Things that look bad but are actually fine
- The deeply nested callback pattern in `src/Legacy/Webhooks.cs` ...
- ...

## Open questions
- Is `src/Experiments/` intentionally untested, or did it fall through?
- ...
```

## Adaptation notes

**Tuning severity calibration.** If the model is over- or under-flagging, edit the dimension tables in `references/dotnet-dimensions.md` to add explicit thresholds. Example: change "god files (>500 LOC)" to ">800 LOC" if your codebase has a higher baseline.

**Adding categories.** The 16 core dimensions are a starting point. Add domain-specific ones for your repo — accessibility for frontend, IaC drift for infra, prompt versioning for LLM apps.

**Mid-audit course correction.** After Phase 1 completes, you can interrupt with: *"Before Phase 2, tell me what surprised you in Phase 1 and what you want to investigate that isn't in the dimensions list."* Worth doing on first run for any new codebase.

## Limitations

This is a static audit, not a security audit. It catches obvious security hygiene issues (hardcoded secrets, SQL injection patterns) but won't replace a real pen test or threat model.

It won't catch business-logic bugs. Those require domain knowledge the model doesn't have.

It can't tell intentional simplicity from accidental simplicity. The "open questions" section exists for exactly this reason — when in doubt, the skill asks rather than assuming.

For very large repos (>200k LOC), even subagent dispatch can produce shallow results. Consider scoping to a module: `/tech-debt-audit src/Core`
