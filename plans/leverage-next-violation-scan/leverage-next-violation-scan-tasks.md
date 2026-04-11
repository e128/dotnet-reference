# Tasks: leverage-next-violation-scan
*Created: 2026-04-11T14:33:35Z*

## Phase 1: Baseline

- [ ] Read `.claude/rules/dotnet-anti-patterns.md` — confirm all 4 anti-pattern regex targets
- [ ] Run `rg "DateTime\.Now|new HttpClient|async void|\.GetAwaiter\(\)\.GetResult\(\)" src/` — record count (expected 0)
- [ ] Confirm `scripts/help.sh` has no `violation-scan` entry
- [ ] Confirm `scripts/` directory has no existing scan script

## Phase 2: Implement

- [ ] Write `scripts/violation-scan.sh` with these capabilities:
  - Source `lib.sh`
  - Parse flags: `--json`, `--claude` (scan .claude/ prose too), `--fix` (future stub)
  - Run ripgrep patterns for each of the 4 anti-patterns against `src/**/*.cs`
  - With `--claude`: also scan `.claude/skills/*/SKILL.md` and `.claude/agents/*.md`
  - Group output by pattern name
  - Exit non-zero if any violations found
  - `--json` flag: emit `{"violations": N, "by_pattern": {...}, "files": [...]}` structure
- [ ] Add `violation-scan.sh` to `scripts/help.sh` catalog with description "Scan for .NET anti-patterns and rule violations."
- [ ] Add keyword shortcut entry to `.claude/rules/keyword-shortcuts.md`:
  - `scan violations` / `check anti-patterns` → `scripts/violation-scan.sh`

## Phase 3: Verify

- [ ] `bash -n scripts/violation-scan.sh` — syntax check passes
- [ ] `shellcheck scripts/violation-scan.sh` — no warnings
- [ ] `scripts/help.sh | grep violation-scan` — appears in catalog
- [ ] `scripts/violation-scan.sh` — exits 0 on clean codebase
- [ ] `scripts/violation-scan.sh --json | jq .` — valid JSON
- [ ] `scripts/violation-scan.sh --claude` — scans .claude/ without error
- [ ] `scripts/check.sh --no-format` — full suite still passes
