---
name: learn
description: >
  Researches a topic from the web and adds it to the project's lode/ documentation.
  Use /learn <topic> to research official docs and persist findings. Supports --skill flag
  to output as a SKILL.md instead.
  Triggers on: learn about, research topic, add to lode from web, learn this, deep research.
argument-hint: "<topic> [--skill [--global]]"
allowed-tools: Read, Glob, Grep, Bash, Write, Edit, Agent
---

# Learn Skill

Orchestrates research via the `sme-researcher` agent and persists findings.

**Arguments:** `$ARGUMENTS`

## Bounds

Research official docs until the main claims have a source, then stop. Tell sme-researcher in the prompt to return as soon as it has them.

## Steps

### 1. Parse arguments

- Extract `<topic>` and optional flags (`--skill`, `--global`) from arguments.
- Normalize topic to **kebab-case** for file naming.
- If topic is empty, show usage examples and stop.

**Extended invocation: suggest improvements to an existing skill.**
The format `/learn <topic> and suggest improvements to the '<skill-name>' skill` is fully
supported. After writing the lode draft, read the named SKILL.md and present a diff-style
list of improvements drawn from the research findings. Ask "Apply these improvements?" before
making any changes. This pattern works for lode output only (not `--skill`).

### 2. Determine output target

Check if `lode/` exists (`Glob: lode/lode-map.md`):

| Condition | Output target |
|-----------|--------------|
| `lode/` exists, no `--skill` flag | **lode/** (default) |
| `--skill` flag provided | **SKILL.md** (project-local or `--global`) |
| No `lode/` and no `--skill` flag | **Prompt user**: create lode/, create as skill, or create as global skill |

**If lode/ exists:** Read `lode/lode-map.md` to identify any existing lode files related to the topic. Skim relevant ones so the researcher can focus on gaps, not already-documented ground.

### 3. Research via sme-researcher

Spawn the `sme-researcher` agent with this prompt, within the bounds above:

> Research `<topic>` for use in this project. Focus on official documentation. {output-specific instructions, see below}
>
> If any related lode files exist, note them and focus on gaps not yet covered.

**For lode output:** Instruct sme-researcher to return synthesized findings without writing files. Include: "Do NOT write files. Return your synthesized findings with source URLs and scrape dates so I can draft them into lode/tmp/."

**For skill output:** Instruct sme-researcher to return findings without persisting. Include: "Do NOT write files. Return your synthesized findings with source URLs and scrape dates so I can format them as a SKILL.md."

**Empty response handling:** The sme-researcher agent frequently completes its
research but returns an empty body on the first call (only agentId + usage metadata visible).
This is normal behaviour. If the Agent result contains no findings text:
1. Automatically resume the agent with `SendMessage` to its returned `agentId`
2. Prompt: "Please provide your complete synthesized findings on `<topic>`. Include all source URLs and key recommendations you researched."
3. Do NOT ask the user: handle the resume transparently.

### 4. Write draft (lode path) or skill output (--skill path)

**For lode output:** Write findings to `lode/tmp/<topic-kebab-case>.md` as a draft. Follow lode file conventions (timestamp, relative links). Then show the user a summary of the draft and ask:

**250-line enforcement:** Before writing, estimate content volume. If a single file would
exceed 250 lines, split at a natural topic boundary into two focused sub-files, write both
to `lode/tmp/` and promote both together. Never write a lode file over 250 lines.

**Mermaid diagrams:** Include Mermaid diagrams only where they add genuine architectural
clarity (e.g., data flows, state machines, pipeline stages). Do NOT add Mermaid to
config/reference docs where tables and code blocks are clearer.

> Draft saved to `lode/tmp/<topic>.md`. Promote to permanent lode?

On user approval:
1. Move the file from `lode/tmp/` to the appropriate location based on topic domain:
   - .NET patterns/tools -> `lode/dotnet/<topic>.md`
   - Infrastructure/tooling -> `lode/infrastructure/<topic>.md`
   - Cross-cutting concerns -> `lode/<topic>.md` (root level)
2. Add the new entry to the matching directory section of `lode/lode-map.md`
3. Delete the tmp draft

Never create `lode/research/`: research findings are integrated into domain-specific directories.

If the user declines, leave the draft in `lode/tmp/` for later review. Note: `lode/tmp/` is git-ignored, so drafts will not be committed.

**For skill output:** Write findings directly to skill location:

**Location:**
- Default: `.claude/skills/<topic-kebab-case>/SKILL.md`
- With `--global`: `~/.claude/skills/<topic-kebab-case>/SKILL.md`

If a file already exists at that path, warn the user before overwriting.

**SKILL.md format:** Load `${CLAUDE_SKILL_DIR}/assets/skill-template.md` for the output structure. Adapt sections to fit, omit empty ones, add others if warranted. Rules:
- `name`: max 64 chars, lowercase + numbers + hyphens only
- Body: under 500 lines, imperative language, version-specific
- **Never fabricate APIs or features not found in sources**

### 5. Confirm

```
Learned: <topic>
Sources: <count> URLs scraped
Saved to: <path>   (or: Draft in lode/tmp/<topic>.md — awaiting promotion)
```

## Troubleshooting

- **Topic maps to an existing lode file**, read the existing file first to identify gaps. The researcher should focus on what is not yet documented, not re-document existing content
- **No lode/ directory found and no --skill flag**, prompt the user for their preferred output: create lode/, create as skill, or create as global skill
- **--global flag used without --skill**: global output only applies to skill files. Prompt for clarification or treat as `--skill --global`
