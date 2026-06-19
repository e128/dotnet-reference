---
name: repo-map-wiki
description: >
  Build a pyramid-structured wiki for a product area by exploring its source repos.
  Produces Tufte Dracula HTML docs with Mermaid diagrams.
when_to_use: >
  build wiki, create repo-map, document product area, build knowledge base for repos.
argument-hint: "<product-area> [--target <output-path>] [--repos <repo-path>] [--format md|html]"
allowed-tools:
  - Read
  - Write
  - Glob
  - Grep
  - Bash
  - Agent
  - Edit
  - TaskCreate
  - TaskUpdate
---

# Repo-Map Wiki Builder

Build a multi-audience, pyramid-structured wiki for a product area by investigating its
source repositories. The output is a self-contained documentation set under `docs/` that
routes readers by role to the right depth of information.

**Default output: Tufte Dracula HTML** — self-contained `.html` files with inlined CSS,
client-side Mermaid rendering, and sortable tables. Pass `--format md` for plain markdown.

**Arguments:** `$ARGUMENTS`

---

## Output Format

**HTML (default).** Self-contained `.html` files with the Dracula-Tufte theme — inlined
CSS/JS, `<pre class="mermaid">` diagrams with click-to-zoom, sortable booktabs tables, and
relative-link nav. Read [references/html-format.md](references/html-format.md) at generation
time and follow its skeleton exactly.

**Markdown (`--format md`).** Same structure and content; `.md` files with standard fenced
Mermaid blocks.

---

## Convention Profile Detection

Before generating, check if the target already has a `docs/` structure:

1. **Existing-docs profile** — if `docs/index.html` (or `docs/index.md`) exists at the target path:
   - Use the existing `docs/` layout (overview/, architecture/, engineering/, guides/, reference/)
   - Integrate generated files into the existing tree -- do not duplicate or overwrite
2. **Fresh profile** — if no `docs/` directory exists:
   - Create a fresh `docs/` tree with the full structure below

---

## What This Produces

A `docs/` directory of 15–20 files across five sections: `overview/` (executive, strategy,
product), `architecture/` (system, data-flow, storage, deployment, integration-patterns,
security), `engineering/` (getting-started, codebase-map, testing-strategy), `guides/`
(configuration), and `reference/` (glossary, cli-commands), plus `index.html` as the pyramid
entry point. The per-file content requirements are in
[references/file-conventions.md](references/file-conventions.md).

**Per-service docs** fold into `architecture/{service}.html` (internal architecture) or
`engineering/{service}-ops.html` (operations) — there is no separate `services/` directory.

---

## Execution Steps

### 1. Parse Arguments

Extract from `$ARGUMENTS`:
- `<product-area>` — name of the product (default: this repo, `E128.Reference`)
- `--target <path>` — where to write the docs (default: `docs/`)
- `--repos <path>` — where the source repos live (default: `.`, this repo)
- `--format <md|html>` — output format (default: `html`)

If `<product-area>` is empty, default to documenting this repository (`E128.Reference`) in
self-documentation mode (see step 3). Only ask the user to pick when `--repos` points at a
directory containing multiple cloned repos.

### 2. Prepare HTML Skeleton

If `--format html` (default): read [references/html-format.md](references/html-format.md)
and use its canonical skeleton for every output file. The skeleton includes all CSS, JS,
Mermaid, and table sorting inline -- no external files needed.

If `--format md`: skip this step.

### 3. Investigation Phase (Parallel)

**Self-documentation mode.** When product-area is `./` (or the current `E128.Reference` repo),
skip external repo exploration. The investigation agents can read existing `lode/` knowledge
files (start with `lode/lode-map.md`, `lode/summary.md`, `lode/terminology.md`) and any
existing `docs/*.md` files directly -- the repo is already documented. Convert existing `.md`
files to `.html` rather than regenerating content from scratch. Investigation agents in this
mode should focus on summarising `lode/` sections rather than exploring unfamiliar source trees.

Launch 2-3 Explore agents in parallel to gather raw data. Each agent should investigate
deeply and report back structured findings. The goal is to understand:

**Agent 1 — Core Architecture:**
- Solution files (.sln), project files (.csproj)
- Entry points (Program.cs, Startup.cs)
- API controllers — list each with route, namespace, key actions
- DI/IoC registration
- Middleware pipeline
- Configuration patterns (appsettings, Key Vault, environment variables)
- CLAUDE.md or README.md files in repos

**Agent 2 — Data & Integration:**
- Data access patterns (ORM, connection strings, stored procedures)
- Messaging infrastructure (queues, topics, message types)
- External service integrations (auth providers, email, analytics)
- Background processing (WebJobs, Functions, Workers, Quartz, Hangfire)
- Real-time communication (SignalR, WebSockets)

**Agent 3 — Frontend & Supporting Repos:**
- Frontend framework, package.json, routing
- Supporting services (AD agents, extraction tools, etc.)
- Infrastructure-as-code (Terraform, Bicep, ARM)
- Automation scripts (PowerShell, Bash)
- CI/CD pipeline configuration

### 4. Synthesis & Planning

After agents return, synthesise findings into a content plan:

1. **Identify the product's purpose** — one-paragraph description of what the platform does
2. **Count key metrics** — repos, controllers, connectors, modules, message types
3. **Map business modules** — what domain areas exist (e.g., ARM, Audit Trail, Certifications)
4. **Identify service boundaries** — which repos/projects form logical services
5. **Determine per-service doc placement** — architecture docs for structural analysis,
   engineering docs for operational/runbook content

Create tasks to track each file being written.

### 5. File Generation (Parallel Batches)

Generate files in parallel batches. Each file follows the conventions in
[references/file-conventions.md](references/file-conventions.md) (content requirements)
and [references/html-format.md](references/html-format.md) (HTML skeleton and tag patterns).

Generate `index.html` first, then fan out one agent per section: overview (3), architecture
(6+ service pages), engineering (3+ ops pages), guides + reference (remaining). The
architecture agent is the long pole — it carries the most Mermaid diagrams.

For each file:
1. Copy the canonical HTML skeleton from [references/html-format.md](references/html-format.md)
2. Fill in `<title>`, `<meta>` tags, `<h1>`, and subtitle
3. Adjust `<nav class="wiki-nav">` links based on file depth (e.g., `../index.html` from subdirectories)
4. Populate `<main>` with content sections using the tag patterns in html-format.md
5. Verify all internal links use `.html` extensions and correct relative paths

### 6. Verification

After all files are written:
- `Glob` to confirm all expected files exist
- Spot-check cross-references between docs (links in nav and body point to real files)
- Open `docs/index.html` in a browser if possible to verify rendering
- Report final file count and structure to user

**`.md` link false positives.** A naive `rg '\.md"'` will flag `.md` strings inside Mermaid
diagram labels, JSON examples, and fenced code blocks as broken navigation links. These are
content references, not navigation links, and require no fix. Only flag `.md"` occurrences
inside `href=` attributes.

---

## File Conventions

See [references/file-conventions.md](references/file-conventions.md) for per-file content requirements, Mermaid diagram type guidance, and quality checklist. The content requirements are format-independent -- they apply to both HTML and markdown output.

---

## Lode Integration

The generated `docs/` tree is a presentation layer; `lode/` remains the authoritative
memory (see CLAUDE.md). Cross-link from lode summaries to docs, summarise rather than
duplicate lode content, and timestamp generated files with `scripts/ts.sh` (ISO 8601 UTC).

## Troubleshooting

See [references/troubleshooting.md](references/troubleshooting.md) for common issues and recovery tips.

## Self-Improvement

If a run surfaces a recurring failure, user correction, or convention gap, spawn the
`skill-self-updater` agent with a short feedback payload. Skip when the run was clean.

