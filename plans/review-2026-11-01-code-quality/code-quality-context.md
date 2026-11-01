# Code Quality Review Context

*Updated: 2026-11-01T14:57:00Z*

## Target

`scripts/internal/cr-target.sh --json` resolved the working tree against
`8290cf8`. Six files, 243 insertions, 27 deletions. The resolver itself could
not see the new file, because `git diff --name-only` omits untracked paths. The
review added `scripts/internal/cr-target.sh` to the delivery by hand.

## Diff Summary

| File | Change |
| ---- | ------ |
| `scripts/internal/cr-target.sh` | new, 230 lines |
| `.claude/skills/mr-sparkle/SKILL.md` | the argument table and the hand-derived steps replaced by one call |
| `.claude/skills/mr-sparkle-dotnet/SKILL.md` | step 1 reads `files`, and the NuGet step was added earlier |
| `.claude/skills/mr-sparkle/references/implementation.md` | component mode reads `files` |
| `.claude/rules/deterministic-scripts.md` | one routing row |
| `scripts/README.md` | one table row |

## Reviewers Run

| Reviewer | Result |
| -------- | ------ |
| `security-reviewer` | 1 Medium, 6 Low. One Low was a false positive, see below |
| `code-reviewer` | 2 Medium, 5 Low. Verdict COMMENT |
| `adversarial-review` | 3 attackers, then 1 defender. 3 High, 1 Medium, 1 Low confirmed |
| `code-review` at effort `high` | 10 findings, 3 High |
| `ponytail:ponytail-review` | 2 defects, both confirmed |

## Dedup Report

The untracked-file gap was found by all five reviewers. The single-file path
gap was found by three. The numbering gap was found by four. The numbers in the
tasks file name the primary source and the agreeing reviewers.

The defender lowered two claims. The raw-git duplication fell from High to
Medium, because `scripts/diff.sh` has no `--base` flag for the working-tree and
branch modes. The `fd` option injection fell from Medium to Low, because the
file name cannot contain a slash, so the command is one bare token.

## False Positive

`security-reviewer` claimed that `[[ -n "$BASE" ]] && printf` aborts the script
under `set -e` in path mode. It does not. Tested directly: the script exits 0
and prints the file list. The line does set a status of 1 when the list is
empty, which is finding 5, and that is a separate defect.

## Vectors Run

- `scripts/lint-shell.sh scripts/internal/cr-target.sh`: pass.
- `scripts/readme-table-diff.sh`: no drift.
- `scripts/help.sh`: lists `internal/cr-target.sh`.
- Six argument forms run against the real repo: PR number, PR URL, `days N`,
  `commits N`, a path, a branch, and no argument.
