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

### Tufte Dracula HTML (default, `--format html`)

Self-contained `.html` files with Dracula-Tufte (muted) theme. Full spec, skeleton, CSS,
JS, tag usage, and color roles are in [references/html-format.md](references/html-format.md).
Read that file at generation time and follow it exactly.

Key points:
- Each file is self-contained -- no external CSS/JS/images
- Mermaid diagrams use `<pre class="mermaid">` with click-to-zoom overlay
- Tables use booktabs style with `<th data-col>` for sortable columns
- Wiki navigation via `<nav class="wiki-nav">` with relative links
- `<meta>` tags for `wiki-product`, `wiki-section`, `wiki-updated`

### Markdown fallback (`--format md`)

Same structure and content requirements as HTML, but output `.md` files with standard
Mermaid fenced code blocks. Follow [references/file-conventions.md](references/file-conventions.md).

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

A `docs/` directory with 15-20 HTML files organised by audience and depth:

```
docs/
├── index.html                     # Pyramid entry point -- start here
├── overview/
│   ├── executive.html             # C-suite / investor view
│   ├── strategy.html              # Strategic positioning and trade-offs
│   └── product.html               # PM / UX / business analyst view
├── architecture/
│   ├── system.html                # System topology, layers, communication
│   ├── data-flow.html             # How data moves through the system
│   ├── storage.html               # Databases, storage, ORM, schemas
│   ├── deployment.html            # Infrastructure, CI/CD, provisioning
│   ├── integration-patterns.html  # Service communication, messaging, events
│   └── security.html              # Auth, encryption, compliance posture
├── engineering/
│   ├── getting-started.html       # Prerequisites, build, run locally
│   ├── codebase-map.html          # All projects and their purposes
│   └── testing-strategy.html      # Test patterns, CI quality gates
├── guides/
│   ├── getting-started.html       # Setup and first run (if not in engineering/)
│   └── configuration.html         # Configuration reference
└── reference/
    ├── glossary.html              # Terminology and definitions
    └── cli-commands.html          # CLI command reference
```

**Per-service docs** fold into `architecture/` or `engineering/` -- there is no separate
`services/` directory. A major service's internal architecture goes in
`architecture/{service}.html`; its operational details go in `engineering/{service}-ops.html`.

---

## When to Use

- A new product area's repos have been cloned and need documentation
- Onboarding engineers to an unfamiliar codebase
- Building a knowledge layer for an existing repo cluster
- Refreshing stale product documentation from source truth

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

**Batch 1 (entry point):** index.html
**Batch 2 (overview):** executive.html, strategy.html, product.html
**Batch 3 (architecture):** system.html, data-flow.html, storage.html, deployment.html, integration-patterns.html, security.html
**Batch 4 (engineering):** getting-started.html, codebase-map.html, testing-strategy.html
**Batch 5 (guides + reference):** configuration.html, glossary.html, cli-commands.html

**Known-good configuration for 20+ file wikis (4 parallel agents):**
- Agent 1 — overview (3 files): executive, strategy, product
- Agent 2 — architecture (7 files): system, data-flow, storage, deployment, integration-patterns, security, plus any service-specific pages
- Agent 3 — engineering (5 files): getting-started, codebase-map, testing-strategy, and ops pages
- Agent 4 — guides + reference + UX (remaining files)
The architecture agent takes longest when it contains many Mermaid diagrams -- account for ~10-12 minutes per run.

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
project memory (see CLAUDE.md). After generating:

- Cross-link from lode domain summaries to generated docs where useful
  (e.g., `lode/dotnet/summary.md` can point to `docs/engineering/codebase-map.html`).
- Do not duplicate lode content verbatim into `docs/`; summarise and link back.
- Timestamp generated files with `scripts/ts.sh` output (ISO 8601 UTC) to match repo convention.
- Per CLAUDE.md, knowledge belongs in `lode/`, never in `MEMORY.md`.

---

## Troubleshooting

See [references/troubleshooting.md](references/troubleshooting.md) for common issues and recovery tips.

## Self-Improvement

At the end of a run, if the run surfaced a recurring failure, a user correction, or a
convention gap, spawn the `skill-self-updater` agent with a short feedback payload describing
what to change in this skill. Skip when the run was clean — do not spawn with an empty payload.

