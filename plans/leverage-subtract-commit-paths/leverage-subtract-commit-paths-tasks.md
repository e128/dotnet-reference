# Tasks: leverage-subtract-commit-paths
*Created: 2026-04-11T14:33:35Z*

## Phase 1: Baseline

- [ ] Read `.claude/skills/yeet/SKILL.md` — confirm yeet is commit + push and is distinct
- [ ] Glob `.claude/skills/` — find the `/commit` skill and read its `SKILL.md` in full
- [ ] Read `.claude/agents/smart-commit.md` in full
- [ ] Diff the two: list every step in `/commit` SKILL.md that is absent from `smart-commit.md` (the claimed "per-file staging questions")
- [ ] Read `scripts/internal/commit.sh` — confirm both paths call it identically

## Phase 2: Implement

- [ ] If `/commit` has no unique gates vs `smart-commit`:
  - Overwrite `/commit` SKILL.md to be a one-paragraph redirect: "This skill is an alias for the smart-commit agent. Invoke smart-commit for all commit workflows."
  - Add all `/commit` trigger phrases to `smart-commit.md` description
  - Update CLAUDE.md keyword table: "commit" triggers → `smart-commit`
- [ ] If `/commit` has unique gates that are legitimate:
  - Move those gates into `smart-commit.md` as conditional logic
  - Then apply the redirect above
- [ ] Leave `yeet` entirely unchanged

## Phase 3: Verify

- [ ] Grep `.claude/` for "commit" trigger phrases — all route to `smart-commit` or `yeet`
- [ ] `scripts/help.sh` — skill catalog still shows correct entries
- [ ] `confirmation-reducer` dry-run (if runnable) — one fewer file audited
- [ ] CLAUDE.md keyword table — no stale `/commit` entries
