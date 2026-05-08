# Claude Revision — Report Template

## Phase 6 Report Format

```
## Claude Revision — [DATE]

**Agents:** N reviewed | **Skills:** N reviewed | **Last run:** [date or "first run"]

### Doctor Check (Phase 0b)
[clean / N errors, N warnings — details below]

### Description Budget (Phase 0c)
Total: N chars / 8,000 budget | Truncated: N | At risk: N
| Entry | Chars | Status |
|-------|-------|--------|

### Web Guidance (Phase 1)
[New guidance / No changes since DATE]

### Agent Health — N agents
| Agent | Lines | Model | Memory | Issues |
|-------|-------|-------|--------|--------|

### Skill Health — N skills
| Skill | Lines | Type | Issues |
|-------|-------|------|--------|

### CLAUDE.md
| File | Line | Type | Finding | Recommendation |
|------|------|------|---------|----------------|

### Scripts Relevance — N scripts checked
| Script | Referenced | Assessment | Recommendation |
|--------|------------|------------|----------------|

### Memory Health
| Agent | MEMORY.md | Lines | Git tracked | Issues |
|-------|-----------|-------|-------------|--------|

### Lode
[Stale entries / All current]

---
HIGH: N  |  MEDIUM: N  |  LOW: N
```

Present the report. **Do not apply any changes yet.** Then ask:
> "Which items would you like to address? (IDs, 'all high', 'agents only', or 'skip')"

## Log Entry Format

After the user responds (or skips), append to `lode/infrastructure/claude-revision-log.md`:

```markdown
### [YYYY-MM-DD]
- Doctor: [clean / N issues]
- Description budget: N/8000 chars | Truncated: N | At risk: N
- Agents: N | Skills: N | Memory files: N
- Web guidance: [new items found / no changes since DATE]
- HIGH: N | MEDIUM: N | LOW: N
- Actions taken: [list or "none"]
- Deferred: [list or "none"]
```

If the file doesn't exist, create it with this header first:

```markdown
# Claude Revision Log
*Updated: [timestamp]*

Persistent memory for `/claude-revision`. Each run appends one entry.
Read at Phase 0 to recover last-known state and deferred items.

## Runs
```
