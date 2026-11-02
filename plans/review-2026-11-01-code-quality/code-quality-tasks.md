# Code Quality Tasks

*Updated: 2026-11-01T14:57:00Z*

All findings applied. `scripts/check.sh` passes. `scripts/internal/cr-target.sh --self-test` passes.

## Phase 1: Make the file list complete and honest

- [x] `scripts/internal/cr-target.sh:128` Untracked files never reach `files`. Fix: union with `git ls-files --others --exclude-standard`, then `sort -u`. Severity: High. Source: all five reviewers
- [x] `scripts/internal/cr-target.sh:159` A single-file path target reports `empty`. Fix: test `[[ -f "$PATH_ARG" ]]` and emit the path directly. Severity: High. Source: code-reviewer, functionality attacker, code-review
- [x] `scripts/internal/cr-target.sh:105` `gh pr view --json files` stops at 100 files. Fix: page `gh api repos/{owner}/{repo}/pulls/<n>/files`. PR #117 now reports 143 files. Severity: High. Source: code-review
- [x] `scripts/internal/cr-target.sh:128` A git failure reads as an empty target. Fix: capture the status and call `err` on a failure. Severity: Medium. Source: security-reviewer

## Phase 2: Make the record complete

- [x] `scripts/internal/cr-target.sh:56` The record drops the flags. Fix: emit `perf`, `test_quality`, `no_plan`, and `critical_only`. Severity: Medium. Source: code-review
- [x] `.claude/skills/mr-sparkle/SKILL.md:81` `reviewers` mixes agents with skills. Fix: emit `agents` and `skills` as separate arrays and route each to its own tool. Severity: Medium. Source: code-reviewer, functionality attacker
- [x] `scripts/internal/cr-target.sh:180` The core reviewer list existed in the script and in the table. Fix: the script owns both sets, and SKILL.md says so. Severity: Medium. Source: quality attacker, code-review

## Phase 3: Make the modes agree

- [x] `scripts/internal/cr-target.sh:229` Text mode exits 1 on an empty target. Fix: add `exit 0`. Severity: Medium. Source: code-reviewer, quality attacker, functionality attacker
- [x] `scripts/internal/cr-target.sh:70` `default_branch` can return an unresolvable ref. Fix: verify the stripped name and fall back to `origin/<name>`. Severity: Medium. Source: functionality attacker
- [x] `scripts/internal/cr-target.sh:145` The days and commits modes duplicate `scripts/diff.sh`. Fix: call `scripts/diff.sh --files`. Severity: Medium. Source: quality attacker, functionality attacker, code-review
- [x] `scripts/internal/cr-target.sh:47` A path with a space fails the re-split. Fix: split only `days N`, `commits N`, and `critical only`. Severity: Medium. Source: functionality attacker, quality attacker, security-reviewer
- [x] `scripts/internal/cr-target.sh:98` PR mode emits no diff source. Fix: emit `diff_command`. Severity: Medium. Source: functionality attacker, code-review
- [x] `scripts/internal/cr-target.sh:62` `cd "$ROOT"` breaks a relative path from a subdirectory. Fix: resolve the path argument before the `cd`. Severity: Medium. Source: quality attacker, functionality attacker
- [x] `.claude/skills/mr-sparkle-dotnet/SKILL.md:67` `packages.lock.json` never dispatches the skill. Fix: add the lock file to the routing case. Severity: Medium. Source: code-review

## Low findings

- [x] `fd` option injection. Fix: `fd -t f . -- "$PATH_ARG"`. Unknown `-` flags now fail.
- [x] List numbering, grammar, and the stale "three things" intro in `mr-sparkle-dotnet/SKILL.md`.
- [x] Bare `cr-target.sh` name and the stale "roster above" reference in `mr-sparkle/SKILL.md`.
- [x] Usage record omits `dotnet`, `ui`, and the new fields. Fix: the usage line lists every field.
- [ ] Two `jq` processes per transform. Fix: one `jq -R -s` call each.
- [x] `shallow` was emitted and never acted on. Fix: a step in `mr-sparkle/SKILL.md`.
- [x] A credentialed PR URL reached `err`. Fix: the message names no reference.
- [x] `PR_NUMBER` was a global. Fix: declared `local`.
- [x] `days 0` and `commits 0` were accepted. Fix: positive-integer validation with a clear error.
- [x] No behavior test. Fix: `--self-test` builds a scratch repo and asserts four forms.
- [ ] Deferred: a file name that contains a newline still splits `files`. Fixing it needs NUL-delimited git output through every field. Severity: Low. Source: security-reviewer
