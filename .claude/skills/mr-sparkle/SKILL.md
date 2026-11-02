---
name: mr-sparkle
description: >
  Union code review. Reviews a PR, branch, commit range, day range, path, or the working
  tree with every available reviewer in one batch. Merges the findings, drops unsupported
  Critical and High claims in a defender pass, and produces a severity-grouped report and
  plan. Review only: it never edits code and never posts a PR comment. Use when the user
  says "mr sparkle", "/mr-sparkle", "sparkle review", "union review", "code review",
  "full review", "deep review", "pre-merge review", "review this PR", or
  "review my branch".
argument-hint: "[PR number | PR URL | branch | days N | commits N | path] [--perf] [--test-quality] [--no-plan] [critical only]"
user-invocable: true
---

# Mr. Sparkle

Run every available reviewer on one target, then merge the findings into one report. A defender pass drops the claims that the code does not support.

**Review only.** This skill never edits code, runs no fix, and posts no PR comment. It writes two kinds of artifact: the report, and a plan directory for findings at High or above. To apply findings, use `/review-apply`.

Every reviewer here is self-contained: its full instructions live in this skill. No external plugin supplies a reviewer that this skill needs. The vendored text stays in `references/`. Never copy a reviewer's instructions into this file.

## Roster

Core reviewers, every run:

| Reviewer                   | Kind             | Covers                                            |
| -------------------------- | ---------------- | ------------------------------------------------- |
| `security-reviewer`        | vendored agent   | Security flaws, secrets, unsafe patterns          |
| `code-reviewer`            | vendored agent   | Quality, maintainability, severity-rated findings |
| `adversarial-review`       | vendored agent   | Attacker and defender pass over three angles      |
| `code-review`              | skill, built in  | Correctness bugs at a chosen effort level         |
| `ponytail:ponytail-review` | plugin skill     | Over-engineering and what to delete               |

Conditional reviewers, when `scripts/internal/cr-target.sh` adds them to `skills`:

| Reviewer                      | Kind         |
| ----------------------------- | ------------ |
| `mr-sparkle-dotnet`           | repo skill   |
| `interfaces:better-interface` | plugin skill |

Left out on purpose:

- `security-review` (built in). It duplicates `security-reviewer`.
- `simplify`. It edits code.

`scripts/internal/cr-target.sh` owns both sets. The tables above describe them and do not define them. Edit the script, never the tables.

## Vendor a Reviewer

A vendored reviewer is one file in `references/`. Three files exist:

- [references/security-reviewer.md](references/security-reviewer.md)
- [references/code-reviewer.md](references/code-reviewer.md)
- [references/adversarial-review.md](references/adversarial-review.md)

Run a vendored reviewer as one subagent, `subagent_type: "general-purpose"`. Give the subagent the reference file path, the target description, and the delivered diff path. The subagent reads the file and follows it.

Run `adversarial-review` in the main context instead. It launches its own subagents, and a subagent cannot launch one.

A vendored reviewer returns severity-rated findings. Never let it write a file.

## Resolve the Target

Run `scripts/internal/cr-target.sh --json "$ARGUMENTS"`. It resolves every argument form and never treats a flag as a target. Never derive a base, a file list, or a reviewer set by hand.

It returns `kind`, `target`, `base`, `files`, `file_count`, `empty`, `agents`, `skills`, `dotnet`, `ui`, `shallow`, `perf`, `test_quality`, `no_plan`, `critical_only`, and `diff_command`.

1. Stop and say so when `empty` is true.
2. Say so and continue with care when `shallow` is true. History is incomplete and the base may be a graft boundary.
3. Pass `target`, `base`, and `files` to every reviewer unchanged.
4. Run the agents in `agents` as subagents, and the skills in `skills` through the Skill tool. Run no other reviewer.
5. Take the diff with `diff_command`. Deliver it through `scripts/internal/cr-diff-deliver.sh <difffile>`. Follow the strategy it prints. Never paste a large diff into more prompts than the strategy names.
6. Carry `perf`, `test_quality`, `no_plan`, and `critical_only` into the phases that use them.

A returned `kind` of `path` is component mode. It has no diff. Group the returned files into slices of 40KB or less at file boundaries, and pass each slice an explicit path list with the instruction to read only those files.

[references/implementation.md](references/implementation.md) holds the sub-slicing algorithm and the component-mode detail.

## Run the Reviewers

1. Send every agent call in one message so they run in parallel. Ask each for severity-rated findings with `file:line` references.
2. Invoke each skill in `skills` with the Skill tool and the target description. Start `adversarial-review` first, because it is the longest.
3. Invoke `code-review` with the effort level `high` and the target. Never pass `--comment` or `--fix`, because this skill reviews only.
4. Pass `perf` and `test_quality` from the record to `mr-sparkle-dotnet`. That skill owns the performance lens and the test rubric. It runs the Roslyn vector on every dispatch.
5. When a plugin reviewer does not resolve, because the plugin is absent or renamed, skip it, note it in Reviewer Notes, and continue. A vendored reviewer is always present.

## Merge

1. Collect the findings from every reviewer.
2. Deduplicate. When two reviewers report the same `file:line`, keep one entry and list every reviewer that found it. Agreement raises confidence.
3. Keep the highest severity assigned to an issue.
4. Sort by severity, then file, then line.
5. Keep spec findings in their own section. They answer a different question from defects.
6. Keep simplification findings in their own section. They are not defects.

Use one severity vocabulary: Critical, High, Medium, Low.

The build is ground truth. When a build check reports zero warnings and zero errors, lower an unconfirmed Medium or Low compliance finding to advisory and mark it `(advisory, build clean)`.

## Defend

Spawn one defender subagent after the merge, `subagent_type: "general-purpose"`. Give it the Critical and High findings and the diff. It reads the code, not the other reviews.

For each finding it answers one line.

```text
<file>:<line>: CONFIRMED <evidence> | DROPPED <reason>
```

- Remove a dropped finding from the report.
- List every drop in Reviewer Notes. Do not hide a drop.
- Let the defender lower a severity. It may not raise one, and it may not add a finding.
- Skip the pass when the merge leaves no Critical or High finding, and say so.

## Write the Plan

Write a plan when at least one confirmed finding is High or Critical, unless `--no-plan` is set. `/review-apply` and the `analyzer-review-miner` agent read this output, so keep the shape.

1. Build a slug from the target: `security`, `concurrency`, `code-quality`, or `general`.
2. Create `plans/review-{YYYY-MM-DD}-{slug}/`.
3. Write `{slug}-plan.md` with the findings table and the phase descriptions.
4. Write `{slug}-tasks.md` with one checkbox per finding: `- [ ] \`file:line\` <problem>. Fix: <fix>. Severity: <level>. Source: <reviewer>`.
5. Write `{slug}-context.md` with the raw reviewer output, the diff summary, the vectors run, and the dedup report.
6. Stamp the files with `scripts/ts.sh`.

On a clean run, write no plan.

## Report

Use this shape.

```text
# Mr. Sparkle: <target>

Verdict: <Approve | Approve with comments | Request changes>

## Critical and High
- `<file>:<line>`, 🔴 bug: <problem>. <fix>. (found by: <reviewer>, <reviewer>)

## Medium and Low
- `<file>:<line>`, 🟡 risk: <problem>. <fix>.
- `<file>:<line>`, 🔵 nit: <problem>. <fix>.

## Spec Fit
Findings from the spec pass, or "no spec available".

## Simplifications
Findings from ponytail:ponytail-review, or "Lean already".

## Reviewer Notes
- One line per reviewer: what it covered, what it skipped, what failed to run
- Defended: every dropped or downgraded finding
- Excluded: <count> files and the reason

## Not checked
- <a problem class that no reviewer covered, or "None">
```

Apply these verdict rules.

- `Request changes`: one or more confirmed Critical or High finding.
- `Approve with comments`: Medium findings only.
- `Approve`: Low findings only, or no findings.

`critical only` filters the report to Critical and High. State the filter in the header.

Return these exit codes:

- `0`: no findings, or Low findings only.
- `1`: Medium or High findings.
- `2`: a Critical finding.

## Failure Handling

- A reviewer that fails or returns nothing is a gap, not a pass. Name it in Reviewer Notes.
- Never present a partial review as complete.
- When a reviewer slice exceeds its context, split the diff by file and merge the sub-slice findings under the one reviewer name.
- No git, a shallow clone, an empty range, or no matching files: state the condition, then exit with the matching code. See [references/error-handling.md](references/error-handling.md).

## Improve After Each Run

Refine this skill from every run. See [references/self-improvement.md](references/self-improvement.md).

## References

- [references/review-rubrics.md](references/review-rubrics.md): seven rubric checklists and the test quality rubric
- [references/cross-file-consistency.md](references/cross-file-consistency.md): multi-file consistency rubric
- [references/perf-patterns.md](references/perf-patterns.md): the anti-pattern catalog for `--perf`
- [references/clanker-patterns.md](references/clanker-patterns.md): the AI-slop pattern catalog
- [references/compliance-and-format.md](references/compliance-and-format.md): repo compliance rules and the build tiebreaker
- [references/error-handling.md](references/error-handling.md): failure paths and exit codes
- [references/implementation.md](references/implementation.md): sub-slicing, component mode, and plan file format
