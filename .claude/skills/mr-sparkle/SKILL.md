---
name: mr-sparkle
description: >
  Union code review. Runs every available reviewer as one batch, merges their findings,
  and drops unsupported Critical and High claims in a defender pass. Review only: it
  never edits code and never posts a PR comment. Use when the user says "mr sparkle",
  "/mr-sparkle", "sparkle review", "union review", or wants every reviewer on one change.
argument-hint: "[PR number | PR URL | branch] [critical only]"
user-invocable: true
---

# Mr. Sparkle

Run every available reviewer on one change, then merge the findings into one report. A defender pass drops the claims that the code does not support.

**Review only.** This skill never edits code, runs no fix, and posts no PR comment. To apply findings, use a separate fix step, such as `/review-apply`.

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

Conditional reviewers, when the diff matches:

| Reviewer                      | Kind         | Run when the diff touches                                     |
| ----------------------------- | ------------ | ------------------------------------------------------------- |
| `mr-sparkle-dotnet`           | repo skill   | `.cs`, `.csproj`, `.props`, `.targets`, solution files        |
| `interfaces:better-interface` | plugin skill | UI files (`.tsx`, `.jsx`, `.vue`, `.svelte`, `.css`, `.html`) |

Left out on purpose:

- `security-review` (built in). It duplicates `security-reviewer`.
- `simplify`. It edits code.

To change the set, edit the tables. Keep names only, with no copied reviewer text.

## Vendor a Reviewer

A vendored reviewer is one file in `references/`. Three files exist:

- [references/security-reviewer.md](references/security-reviewer.md)
- [references/code-reviewer.md](references/code-reviewer.md)
- [references/adversarial-review.md](references/adversarial-review.md)

Run a vendored reviewer as one subagent, `subagent_type: "general-purpose"`. Give the subagent the reference file path, the target description, and the delivered diff path. The subagent reads the file and follows it.

Run `adversarial-review` in the main context instead. It launches its own subagents, and a subagent cannot launch one.

A vendored reviewer returns severity-rated findings. Never let it write a file.

## Resolve the Target

1. Read `$ARGUMENTS` for `critical only`.
2. For a PR number or URL, use that PR. Get its branches with `gh` or `az`, whichever the repo uses.
3. For a branch name, diff it against the default branch.
4. With no argument, review the working tree. Diff against the merge base with the default branch. When that is empty, use the last commit.
5. Stop and say so when the diff is empty.
6. List the changed files, then pick the conditional reviewers.

Build one target description: the PR or branch, the base branch, and the changed files. Pass the same description to every reviewer.

Deliver the diff through `scripts/internal/cr-diff-deliver.sh <difffile>`. Follow the strategy it prints. Never paste a large diff into more prompts than the strategy names.

## Run the Reviewers

1. Send every agent call in one message so they run in parallel. Ask each for severity-rated findings with `file:line` references.
2. Invoke each roster skill with the Skill tool and the target description. Start `adversarial-review` first, because it is the longest.
3. Invoke `code-review` with the effort level `high` and the target. Never pass `--comment` or `--fix`, because this skill reviews only.
4. When a plugin reviewer does not resolve, because the plugin is absent or renamed, skip it, note it in Reviewer Notes, and continue. A vendored reviewer is always present.

## Merge

1. Collect the findings from every reviewer.
2. Deduplicate. When two reviewers report the same `file:line`, keep one entry and list every reviewer that found it. Agreement raises confidence.
3. Keep the highest severity assigned to an issue.
4. Sort by severity, then file, then line.
5. Keep spec findings in their own section. They answer a different question from defects.
6. Keep simplification findings in their own section. They are not defects.

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

## Failure Handling

- A reviewer that fails or returns nothing is a gap, not a pass. Name it in Reviewer Notes.
- Never present a partial review as complete.
- When a reviewer slice exceeds its context, split the diff by file and merge the sub-slice findings under the one reviewer name.
