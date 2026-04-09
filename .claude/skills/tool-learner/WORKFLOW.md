---
name: tool-learner
description: >
  Identifies the single highest-leverage new script to add to this project.
  Audits bash invocation history (last 30 days), scores candidates against four criteria,
  proposes ONE winner, and builds it end-to-end on approval — including syntax validation,
  token-efficiency.md entry, and keyword-shortcuts.md wiring.
  Use when the user says "tool learner", "what tool should I build", "highest leverage tool",
  "new script", "what script should I add", or "tool-learner".
---

## Workflow

### Step 1: Gather evidence

Run these two commands — no user interaction yet:

```bash
scripts/session-health.sh --json
# Plus review recent session transcripts for repeated bash patterns
```

Extract the top 30 most frequent raw Bash commands. Filter out:
- Commands already mapped to a `scripts/*.sh` script in token-efficiency.md
- Read-only git commands (`git status`, `git log`, `git diff`) already covered by `scripts/status.sh` / `scripts/diff.sh`
- One-off commands unlikely to recur

The remaining candidates are patterns still being hand-rolled repeatedly.

### Step 2: Score candidates

For each candidate, score 1–5 on:

| Criterion        | Question                                                             |
| ---------------- | -------------------------------------------------------------------- |
| **Novelty**      | Does it abstract a complex bash invocation not yet in any script?    |
| **Compound**     | Does it save tokens AND reduce wall-clock time per invocation?       |
| **User impact**  | Would the user notice the improvement immediately?                   |
| **Automation**   | Does it unblock or improve agent/skill automation?                   |

Multiply scores: `novelty × compound × user_impact × automation`. Rank by result.

### Step 3: Propose ONE winner

Present to the user:

```
## Tool Learner Proposal

**Winner:** `<name>.sh`

**Problem it solves:** [1–2 sentences — what repeated pattern it eliminates]
**Evidence:** [exact command string, frequency from session data]
**Score:** Novelty=N, Compound=N, UserImpact=N, Automation=N → total=N

**Why this beats the runner-up:** [runner-up name + one sentence on why winner wins]

**Implementation outline:**
- Script: `scripts/<name>.sh`
- Interface: `scripts/<name>.sh [flags]`
- Replaces: [the exact bash pattern it eliminates]
- token-efficiency.md entry: [draft rule text]
- keyword-shortcuts.md trigger: [proposed phrase → command mapping]

Build it? (yes / no)
```

### Step 4: Build on approval

If the user says yes:

1. **Write** `scripts/<name>.sh` — full implementation, description comment on line 2 (used by `help.sh`)
2. **Source** `scripts/lib.sh` for shared functions
3. **Validate** syntax: `bash -n scripts/<name>.sh` — fix any errors before proceeding
4. **Make executable:** `chmod +x scripts/<name>.sh`
5. **Verify** it appears in help: `scripts/help.sh | grep <name>`
6. **Add** a rule to `.claude/rules/token-efficiency.md` using the draft text from Step 3
7. **Add** a row to `.claude/rules/keyword-shortcuts.md` for the trigger phrase
8. **Smoke-test** the script with a safe invocation (dry-run, `--help`, or no-arg default)

Report: `Built: scripts/<name>.sh — wired into token-efficiency.md and keyword-shortcuts.md`

### Step 5: Stop

Do not propose additional tools or suggest follow-up work unless the user asks.

## Rules

- **One proposal only.** Never list multiple candidates for the user to choose from — do the scoring internally and surface only the winner.
- **Evidence required.** Every proposal must cite an exact command string and frequency count from session data.
- **Full wiring or nothing.** If you build it, wire all four integrations (script + help + token-efficiency + keyword-shortcuts). Partial delivery is not done.
- **Never propose a script that already exists.** Check `scripts/*.sh` before proposing.
- **Syntax gate is hard.** Do not report done until `bash -n` passes.
