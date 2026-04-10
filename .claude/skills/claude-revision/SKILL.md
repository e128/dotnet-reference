---
name: claude-revision
description: >
  Periodic health check for the entire Claude Code configuration — agents, skills, CLAUDE.md, and lode
  memory. Optionally fetches latest Anthropic guidance (web research is skippable with --no-web),
  reviews all agents for model/tool/memory optimization, reviews all skills for token efficiency,
  audits CLAUDE.md files, checks agent memory health and git tracking, and writes a dated revision log
  to lode/ for cross-session continuity. Run monthly or after bulk agent/skill changes.
  Triggers on: claude revision, periodic audit, agent health check, skill optimization, claude config
  review, monthly claude audit, revision audit, config health, claude maintenance, agents audit,
  skills audit, claude audit.
argument-hint: "[--no-web] [--agents-only] [--skills-only] [--self] [--archive]"
---

# Claude Revision

Periodic health check for the Claude Code configuration. Reviews agents, skills, CLAUDE.md, and lode
memory against the latest official guidance. Writes findings to the revision log for cross-session
continuity — the log at `lode/infrastructure/claude-revision-log.md` is this skill's persistent memory
(skills have no `memory:` field; lode is the only cross-session store).

## Scope Flags

- `--no-web` — skip Phase 1 (web research); fast local run
- `--agents-only` — run Phases 0, 2, 5–6 only
- `--skills-only` — run Phases 0, 3, 4, 5–6 only
- `--self` — include `claude-revision` itself in Phase 3 skill review (normally skipped)
- `--archive` — move older entries in the revision log to an archive file before running

## Phase 0: Load Last Run Context

Read `lode/infrastructure/claude-revision-log.md` if it exists. Surface:
- Date of last run and deferred items
- Agent/skill counts (to detect additions or removals since last run)

If the file doesn't exist, this is the first run — proceed without prior context.

**Archive check:** Count entries in the `## Runs` section. If the log exceeds 200 lines, warn the user
and suggest running with `--archive` to move older entries to `claude-revision-log-archive.md`. If
`--archive` flag is present, do the archive before proceeding.

## Phase 1: Web Research _(skip with `--no-web`)_

Spawn `sme-researcher` agent with these targets:
- `https://github.com/anthropics/claude-code/blob/main/CHANGELOG.md` — **primary source**: version history with all new features, breaking changes, and deprecations. Filter entries since the last revision log date.
- `https://code.claude.com/docs/en/sub-agents` — agent config (model, tools, memory, maxTurns)
- `https://code.claude.com/docs/en/skills` — skill architecture, trigger patterns
- `https://platform.claude.com/docs/en/agent-sdk/overview` — SDK patterns
- `https://github.com/anthropics/skills` — official skill examples: frontmatter conventions, trigger phrasing, file structure, and reference implementations
- `https://github.com/anthropics/skills/commits/main/` — recent commits: detect new skills, structural changes, or pattern shifts since the last run date

Compare against the last revision log entry date (from Phase 0) to determine if guidance has changed
since the last run. Report new or changed fields, features, or best practices. Report "No new guidance
since YYYY-MM-DD" if unchanged.

## Phases 2–4: Run Directly (No Sub-Agents)

Run in sequence. Direct bash/grep — no Explore agents.

### Phase 2: Agent Review

Run these bash commands directly (no sub-agent):

1. Count and list agents:
   `ls .claude/agents/*.md | wc -l` → agent count
   `ls .claude/agents/*.md` → agent list

2. Extract all frontmatter fields in one pass:
   `rg -l "" .claude/agents/ | xargs rg "^(model|memory|maxTurns|isolation|tools):" --with-filename`

3. Flag agents with `isolation: worktree` but no explicit `model:`:
   `rg -l "isolation: worktree" .claude/agents/ | xargs -I{} sh -c 'grep -L "^model:" {} && echo "  → no model"'`

4. Find iterating agents (have Agent/Bash tool) missing maxTurns:
   `rg -l "Agent|Bash" .claude/agents/ | xargs rg -L "maxTurns:"`

Evaluate only what the grep output reveals against these criteria:

| Check       | Criteria                                                                                     |
| ----------- | -------------------------------------------------------------------------------------------- |
| Model       | `haiku` = read/search/count/parse; `sonnet` = reasoning/writing/cross-ref; `opus` = rare orchestration only |
| Tools       | Allowlist present? Unnecessary tools increase attack surface and context load                 |
| maxTurns    | Iterating agents (build→fix→test loops) need an explicit limit to prevent runaway            |
| memory      | Agents that learn codebase-specific patterns across sessions should have `memory: project`    |
| Overlap     | Does it duplicate another agent? Should it be merged or differentiated?                      |
| Description | Clear trigger keywords? Unambiguous when to use vs. similar agents?                          |

Do not read individual agent files unless a specific field is missing from the grep output.

Return: table of `(agent, lines, model, memory, issues)`. Flag issues HIGH/MEDIUM/LOW.

### Phase 3: Skill Review

Run these bash commands directly (no sub-agent):

1. Count skills and get line counts:
   `fd -e md "(SKILL|WORKFLOW)" .claude/skills | xargs wc -l | sort -rn | head -20`

2. Flag skills over 250 lines for review — read only those files:
   `fd -e md "(SKILL|WORKFLOW)" .claude/skills | xargs wc -l | awk '$1>250 {print}' | sort -rn`

3. Check for stale agent references in skills:
   Get current agent names: `ls .claude/agents/ | sed 's/\.md//'`
   Then grep skills for any `subagent_type` or agent-name references and cross-check against the
   actual agent list. Flag references to agents that no longer exist:
   `rg "subagent_type|agent" .claude/skills/ --with-filename | grep -v "sub-agent\|sub_agent"`

Read individual skill files only for skills flagged as over-size or containing stale references.

Evaluate flagged skills against these criteria:

| Check     | Criteria                                                                                           |
| --------- | -------------------------------------------------------------------------------------------------- |
| Size      | Over 200 lines? Identify content suitable for lode/ or a referenced sub-file                       |
| Triggers  | Too broad (always fires)? Too narrow (never fires)? Missing key phrases?                           |
| Overlap   | Duplicates content in another skill or CLAUDE.md?                                                  |
| Staleness | References removed files, old APIs, or outdated patterns?                                          |
| Type      | Operational (workflow steps) vs Reference (knowledge injection) — both valid; never flag reference skills for lacking workflow steps |

Return: table of `(skill, lines, type, issues)`. Flag issues HIGH/MEDIUM/LOW.
Skip `claude-revision` itself unless `--self` flag is provided or user explicitly requests it.

### Phase 4: CLAUDE.md Audit

Run these bash commands directly (no sub-agent):

1. Check keyword-shortcuts or routing rules for stale agent references:
   `rg "agent" .claude/rules/ --with-filename`
   Extract agent names from output → verify each exists in `.claude/agents/`

2. Check CLAUDE.md and rules for references to removed scripts/agents:
   Get current agent names: `ls .claude/agents/ | sed 's/\.md//'`
   Get current script names: `ls scripts/*.sh 2>/dev/null | xargs -I{} basename {}`
   Grep CLAUDE.md and rules for any agent or script name that doesn't exist on disk.

3. Check for volatile counts in rules files:
   `rg "[0-9]+ errors? in [0-9]+ days" .claude/rules/`

Only read the full `CLAUDE.md` or `~/.claude/CLAUDE.md` if a specific issue is found by grep.

Evaluate findings against these criteria:

| Type             | Flag When                                                        |
| ---------------- | ---------------------------------------------------------------- |
| Redundant        | Duplicates a skill, lode entry, or the other CLAUDE.md           |
| Verbose          | Long explanation where a short rule works; unnecessary examples   |
| Memory candidate | One-off learning that belongs in lode/ instead                   |
| Conflict         | Contradicts a skill or project convention                        |

Return: table of `(file, line, type, finding, recommendation)`.

### Phase 5: Local Health Checks

Run directly (fast — no sub-agent). Two parts:

**5a. Agent Memory Health**

1. Glob `.claude/agent-memory/*/MEMORY.md`
2. For each file: check line count — flag any over 200 lines
3. Cross-check agent frontmatter: which agents declare `memory: project`? Do all have a MEMORY.md? Any with a MEMORY.md but no `memory:` flag?
4. **Git tracking**: run `git ls-files --others --exclude-standard .claude/agent-memory/` to find untracked MEMORY.md files. Untracked files exist only on this machine and are lost on clone or CI.

If untracked files found, report them and offer to `git add` them before moving on.

**5b. Scripts Relevance Check**

1. Discover all project scripts dynamically:
   `ls scripts/*.sh 2>/dev/null | xargs -I{} basename {} .sh`
   If a `scripts/help.sh` (or equivalent catalog script) exists, use its output instead.

2. Check which scripts are referenced across config in one pass — build a single rg alternation
   pattern from the discovered script names, then search:
   `rg -l "<script1>|<script2>|..." .claude/rules/ .claude/skills/ .claude/agents/ CLAUDE.md`
   (Substitute the actual script names discovered in step 1)

3. For scripts with zero reference hits, read only their header (~25 lines) to understand purpose.

4. Evaluate against two criteria:
   - **Redundant**: does Claude Code now offer this natively (e.g., built-in git tools, IDE diagnostics, web fetch)? Flag HIGH.
   - **Orphaned**: does the script serve a real purpose but simply lacks a keyword shortcut or help entry? Flag MEDIUM.

5. Return: table of `(script, referenced, assessment, recommendation)`.

**5c. Lode Check**

Check last-modified dates for key lode infrastructure files:

```bash
for f in lode/summary.md lode/terminology.md lode/practices.md lode/lode-map.md; do
  [ -f "$f" ] && git log --format="%ad %s" --date=short -1 -- "$f"
done
```

Also check any lode files that document agents, skills, or infrastructure:

```bash
fd -e md . lode/ | xargs git log --format="%ad %H %s" --date=short -1 -- | sort | head -20
```

Flag entries where last commit date is older than 30 days, or where the file's content references
agents/skills that no longer exist.

## Phase 6: Report & Log

Report sections: header (agents/skills reviewed, last run date), Web Guidance, Agent Health table (agent, lines, model, memory, issues + severity), Skill Health table (skill, lines, type, issues + severity), CLAUDE.md table (file, line, type, finding, recommendation), Scripts Relevance table (script, referenced, assessment, recommendation), Memory Health table (agent, MEMORY.md, lines, git tracked, issues), Lode status, severity totals (HIGH/MEDIUM/LOW).

Present the report. **Do not apply any changes yet.** Then ask:
> "Which items would you like to address? (IDs, 'all high', 'agents only', or 'skip')"

After user responds, append a run entry to `lode/infrastructure/claude-revision-log.md` with: date, agent/skill/memory counts, web guidance status, severity counts, actions taken, deferred items. If the file doesn't exist, create it with a header explaining its purpose.

## Guidelines

- **Phases 2–4 use direct bash/grep, not sub-agents** — sub-agents burn 90–150 KB per phase on JSONL transcripts; direct grep is 1–3 tool calls and returns exactly the fields needed
- **Don't auto-fix** — present findings, wait for user direction
- **Phase 5a git check is mandatory** — untracked MEMORY.md files silently vanish on clone
- **Phase 5b scripts check: prefer removal over orphan** — if a script is both unreferenced AND its functionality is now native to Claude Code, flag HIGH for removal; if it just lacks a shortcut, flag MEDIUM and suggest adding one
- **Reference skills are complete as-is** — do not flag knowledge-only skills for lacking workflow steps

## User Input

$ARGUMENTS

## Self-Improvement (Mandatory)

This skill must get better with every use. The revision log at `lode/infrastructure/claude-revision-log.md` IS this skill's persistent memory — always append to it at Phase 6.

1. **Always write the Phase 6 log entry** — Even if no changes were made, the dated entry creates a timestamp baseline for detecting drift. Never skip Phase 6.
2. **Record deferred items explicitly** — When the user declines to fix a finding, log it under "Deferred" in the Phase 6 entry with the original severity so it resurfaces next run.
3. **Capture new check criteria** — If a new pattern was found that should be checked in future runs (e.g., a new agent frontmatter field, a new lode convention), document it in SKILL.md Phases 2–5 criteria tables.
4. **Note guidance changes** — When Phase 1 web research finds a new Anthropic best practice, append a concise note to `lode/infrastructure/claude-code-maintenance.md` alongside the revision log entry.

The goal: each revision run recovers prior context instantly from the log and builds on previous findings rather than re-discovering the same issues.

## Troubleshooting

- **Web fetch fails** — use `--no-web` for fast local-only run
- **Sparse grep results** — no matches means the field is absent; note and continue
- **First run** — normal; no prior context; log created at Phase 6
- **Untracked MEMORY.md files** — run `git add .claude/agent-memory/`
- **Reference skills flagged for missing workflow** — reference skills are complete as-is; re-check classification
- **Log too large** — archive older runs to `claude-revision-log-archive.md`
