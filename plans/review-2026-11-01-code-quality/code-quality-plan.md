# Code Quality Review: cr-target.sh and the mr-sparkle prose

*Updated: 2026-11-01T14:57:00Z*

Target: the working tree against `8290cf8`. Six files. 243 insertions, 27 deletions.
Verdict: request changes. Three High findings, all confirmed by the defender.

The change adds `scripts/internal/cr-target.sh` and points the mr-sparkle skills
at it. The script is now the single owner of review-target resolution.

## Findings

| # | File | Line | Problem | Severity |
| - | ---- | ---- | ------- | -------- |
| 1 | `scripts/internal/cr-target.sh` | 128 | `git diff --name-only` omits untracked files, so a new file escapes the review | High |
| 2 | `scripts/internal/cr-target.sh` | 159 | `fd` rejects a single-file search root, so a file target reports `empty` | High |
| 3 | `scripts/internal/cr-target.sh` | 105 | `gh pr view --json files` caps at 100 files and truncates silently | High |
| 4 | `scripts/internal/cr-target.sh` | 180 | The script hardcodes the core reviewer list that SKILL.md says the table owns | Medium |
| 5 | `scripts/internal/cr-target.sh` | 229 | Text mode exits 1 on an empty target, and JSON mode exits 0 | Medium |
| 6 | `scripts/internal/cr-target.sh` | 70 | `default_branch` strips `origin/` and can return a ref that does not resolve | Medium |
| 7 | `scripts/internal/cr-target.sh` | 98 | PR mode emits no diff source, and the base ref may not be fetched | Medium |
| 8 | `scripts/internal/cr-target.sh` | 56 | The JSON record drops the flags that later phases need | Medium |
| 9 | `scripts/internal/cr-target.sh` | 145 | Range modes duplicate `scripts/diff.sh` with divergent validation | Medium |
| 10 | `scripts/internal/cr-target.sh` | 47 | The argument re-split breaks a path with a space | Medium |
| 11 | `scripts/internal/cr-target.sh` | 128 | `2>/dev/null \|\| true` hides a git failure as an empty target | Medium |
| 12 | `.claude/skills/mr-sparkle/SKILL.md` | 81 | `reviewers` mixes agents with skills, and step 2 says to invoke each with the Skill tool | Medium |
| 13 | `.claude/skills/mr-sparkle-dotnet/SKILL.md` | 67 | `packages.lock.json` never dispatches this skill, so the NuGet step cannot run | Medium |
| 14 | `scripts/internal/cr-target.sh` | 62 | `cd "$ROOT"` breaks a relative path passed from a subdirectory | Medium |

## Phase 1: Make the file list complete and honest

Findings 1, 2, 3, 11. Every one is a silent wrong scope, and the skill stops or
reviews the wrong set without a message. Fix all four in one edit.

1. Append `git ls-files --others --exclude-standard` to the working-tree list.
2. Test `[[ -f "$first" ]]` before the `fd` call and emit the path directly.
3. Replace the `gh pr view --json files` call with the paginated `gh api` form.
4. Capture the status of each git call and call `err` on a failure.

## Phase 2: Make the record complete

Findings 4, 8, 12. The caller must not re-derive anything the script can emit.

1. Emit `perf`, `test_quality`, `no_plan`, and `critical_only` in the record.
2. Emit `agents` and `skills` as two arrays, not one `reviewers` array.
3. Pick one owner for the core reviewer list and delete the other copy.

## Phase 3: Make the modes agree

Findings 5, 6, 9, 10, 7, 14.

1. End text mode with `exit 0` after the file list.
2. Fall back to `origin/<name>` when the local branch does not resolve.
3. Route the days and commits modes through `scripts/diff.sh --files`.
4. Split only a leading `days N` or `commits N`, and keep other words whole.
5. Emit a `diff_command` field for PR and branch mode.
6. Resolve a path argument against the caller's directory before any `cd`.
