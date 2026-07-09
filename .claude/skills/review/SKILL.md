---
name: review
description: >
  Unified code review: --local (diff review), --pr (adversarial PR review),
  --apply (batch fixes from last review).
when_to_use: >
  code review, review code, audit code, full review, deep review, comprehensive review,
  review pr, review branch, pre-merge review, adversarial review,
  review this branch, check before merge, code review pr, final review,
  fix all findings, apply review, apply all fixes, fix code review issues.
argument-hint: "[--local | --pr | --apply] [args]"
user-invocable: true
---

# Review Skill — Router

You are a dispatch router. Detect the review mode and hand off to the focused sub-skill.

## Mode Detection

Check `$ARGUMENTS` and the triggering message for mode keywords:

| Mode      | Keywords                                          | Dispatches to       |
|-----------|---------------------------------------------------|---------------------|
| `--local` | review change, review code, audit code, full review, recent changes, last N days/commits, `--perf`, `--test-quality` | `/review-local` |
| `--pr`    | review pr, review branch, pre-merge, adversarial, check before merge, `--pr [branch]` | `/review-local --full` (deep pre-merge review) |
| `--apply` | fix all, apply review, apply findings, fix all findings | `/review-apply` |

Default: `--local` if no clear PR or apply keyword. `--pr` takes precedence when a branch/PR number is mentioned; it runs the local review in full mode for maximum coverage before merge.

## Dispatch

Once mode is determined, invoke the target sub-skill with the remaining arguments:

```
Skill("review-local", "{remaining args}")
Skill("review-local", "--full {remaining args}")
Skill("review-apply", "{remaining args}")
```

Keep all review logic in the dispatch targets — this router only dispatches.
