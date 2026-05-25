---
name: lode-capture
description: >
  Capture a single insight into the lode/ documentation store. Routes the insight
  to the correct lode file based on topic, writes it as a current-state fact, and
  timestamps the file. Lightweight alternative to lode-sync for one-off captures.
  Triggers on: save this to the lode, capture this insight, capture to lode,
  remember this for next time, save this knowledge, capture that knowledge.
argument-hint: "<insight to capture>"
---

# Lode Capture

Persist a single insight into `lode/`. One insight, one file, one timestamp.

## Workflow

### 1. Extract the insight

Parse the insight from `$ARGUMENTS` or the preceding conversation context.

### 2. Route to the correct file

| Topic signals                                                       | Target lode file                               |
| ------------------------------------------------------------------- | ---------------------------------------------- |
| C#, .NET, dotnet, async, analyzer, Roslyn, nullable                 | `lode/dotnet/*.md` (find closest match)        |
| Infrastructure, CI, GitHub Actions, hooks, skills, agents           | `lode/infrastructure/*.md`                     |
| Scripts, shell                                                      | `lode/infrastructure/*.md`                     |
| Practices, code style, conventions, patterns                        | `lode/practices.md`                            |
| Dependencies, NuGet, packages                                       | `lode/dependency-policy.md`                    |
| Project-wide, architecture, overview                                | `lode/summary.md`                              |
| Terminology, domain words, definitions                              | `lode/terminology.md`                          |

### 3. Read the target file, find the best section heading

### 4. Check file size before writing

```bash
scripts/lode-guard.sh lode/path/to/file.md
```

If over limit, create a focused sub-file instead.

### 5. Write as a current-state fact under the best section

Style: present tense, concrete code examples for patterns, under 5 lines per capture.

### 6. Timestamp the file

```bash
scripts/ts.sh lode/path/to/file.md
```

### 7. Report

```
Captured to lode/path/to/file.md § Section Name
```

## Rules

- **One insight per invocation** — for batch sync, use the `lode-sync` agent
- **Current-state facts only** — never changelog style ("Added X on date")
- **Never write to MEMORY.md** — lode is the authoritative store
- **Under 5 lines per capture** — if the insight is larger, it needs a dedicated lode file
