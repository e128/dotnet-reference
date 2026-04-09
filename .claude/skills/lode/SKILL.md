---
name: lode
description: >
  Quick lookup in the lode/ documentation store. Searches for a topic across
  all lode files and returns matching content with context. Fast alternative
  to manually grepping the documentation tree.
  Triggers on: lode search, search lode, find in lode, lode lookup, what does
  lode say about, check lode for, lode docs.
argument-hint: "<search-term>"
---

# Lode Quick Lookup

Fast search across `lode/` documentation. No agents — Grep + Read only.

## Workflow

### 1. Search by content

If `$ARGUMENTS` is provided, grep for it:

```
Grep: pattern=$ARGUMENTS, path=lode/, output_mode=content, context=3, head_limit=30
```

### 2. Search by index (if no grep hits)

If grep returns no results, read `lode/lode-map.md` and search for `$ARGUMENTS`
in the index entries. Report matching files with their descriptions.

### 3. If a single file matches strongly

Read the most relevant file and present the section that matches the query.
Keep output under 40 lines — link to the full file for more detail.

### 4. If multiple files match

List them with the matching lines:

```
## Lode results for "{query}"

### lode/dotnet/testing.md (3 matches)
- L14: Example match line 1
- L28: Example match line 2
- L45: Example match line 3

### lode/infrastructure/ci.md (1 match)
- L92: Example match line

See full files: `lode/dotnet/testing.md`, `lode/infrastructure/ci.md`
```

## Rules

- **No agents** — this must be fast (under 3 seconds of tool time)
- **No edits** — read-only search
- **Truncate long results** — max 40 lines of output, link to files for full content
- **If no arguments**: read and display `lode/lode-map.md` as the index
