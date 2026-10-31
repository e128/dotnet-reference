# Mr. Sparkle: Pipeline Detail

How to deliver the diff to a reviewer and how to review a target that has no git diff.

## 1. Diff Delivery

Choose the strategy by per-reviewer slice size with `scripts/internal/cr-diff-deliver.sh <difffile>`. The script prints one of three strategies.

| Slice size | Strategy                                                   |
| ---------- | ---------------------------------------------------------- |
| 30KB or less | Pass the diff inline in the reviewer prompt.             |
| 30KB to 40KB | Write `.claude/tmp/cr-<reviewer>.diff` and let the reviewer read it. |
| Above 40KB | Split at file boundaries, then merge the sub-slice findings under the one reviewer name. |

Never deliver a diff from `/tmp`. A subagent cannot read that path.

Delete `.claude/tmp/cr-*.diff` after the run.

### Sub-Slicing Algorithm

1. Split the diff into per-file hunks on `^diff --git`.
2. Pack the files greedily into sub-slices of 40KB or less. Never split one file across two sub-slices.
3. Name the sub-slices sequentially: `cr-<reviewer>-1.diff`, `cr-<reviewer>-2.diff`.
4. Spawn one reviewer instance per sub-slice, with the same prompt, the same severity rules, and the same diff constraint.
5. Merge the findings from every sub-slice under the original reviewer name.
6. When one file alone exceeds 40KB, give it its own sub-slice and let the reviewer do its best.

Why 40KB: a subagent has a limited tool budget. A larger diff costs turns on pagination and never reaches the analysis phase.

### Diff-Scoping Constraint

Include this text verbatim in every reviewer prompt.

```text
IMPORTANT: Review ONLY the diff provided below. Do not use Read or Grep
to investigate files beyond these changes. If a changed line calls into
existing code that looks suspicious, note it as "adjacent concern, not
in diff" but do NOT spend tool calls reading that code. Your tool budget
is limited; spend it analyzing the diff, not exploring the codebase.
```

Without this constraint, a reviewer reads whole files to learn context and exhausts its budget before it reports.

## 2. Component Mode

Use this mode for a path argument, after a bulk fix, or when the git diff is empty but a review is needed. No diff exists, so the file list is the input.

1. Enumerate the files with Glob, for example `src/E128.Analyzers/**/*.cs`.
2. Estimate the total size. Group the files into slices of 40KB or less at file boundaries.
3. Give each slice an explicit file list and this instruction: read only these files and review them for the named concerns.
4. Pre-grep the known search patterns once in the main context. Pass the hits inline to the reviewer. This saves each reviewer the same grep.
5. Merge the findings the same way as a diff review.

## 3. Plan Files

`/review-apply` and the `analyzer-review-miner` agent read this shape. Keep it.

```text
plans/review-{YYYY-MM-DD}-{slug}/
  {slug}-plan.md      findings table, then the phase descriptions
  {slug}-tasks.md     one checkbox per finding
  {slug}-context.md   raw reviewer output, diff summary, vectors, dedup report
```

A task line is one self-contained checkbox.

```text
- [ ] `src/Path/File.cs:42` Null check missing before use. Fix: add an ArgumentNullException guard. Severity: High. Source: code-reviewer
```

Build the slug from the target: `security`, `concurrency`, `code-quality`, or `general`. Stamp every file with `scripts/ts.sh`.
