---
name: arch-scan
description: >
  Recursively analyze the codebase and generate nested Mermaid architecture diagrams
  with structured annotations. Includes dependency mapping (NuGet packages, infrastructure,
  runtime pinning) for .NET projects.
when_to_use: >
  architecture diagram, mermaid scan, generate architecture, codebase diagram,
  scan architecture, arch scan, architecture scan, refresh architecture, arch diagram,
  visualize architecture, dependency map, map dependencies, dep map, what depends on what,
  package dependencies, infrastructure dependencies, runtime versions, docker images.
allowed-tools: Read, Write, Edit, Glob, Grep, Bash, Agent
---

# Arch-Scan Skill

Recursively analyze a scope of the solution and generate perspective-driven Mermaid architecture diagrams with structured annotations. Output lives in `lode/{domain}/architecture/`.

---

## Scope Argument

The user passes a scope as `$ARGUMENTS`. A scope is one of:

| Scope form                | Meaning                                                | Lode domain            |
|---------------------------|--------------------------------------------------------|------------------------|
| A single project name     | One `src/` project (e.g. `MyApp.Core`)                 | kebab-case of the name |
| A comma-separated list    | A related group of projects treated as one domain      | caller-supplied name   |
| `full`                    | All `src/` projects (`kind: "src"`)                    | per-project            |

Resolve project names and paths dynamically — never hardcode them:

```
scripts/solution-inventory.sh --json
```

Filter to `kind == "src"` (skip `test`/`bench`). Each entry gives `name` and `path` (the `.csproj`); the source directory is that file's parent. If no scope is provided, list the available `src` projects from the inventory and ask which to scan.

---

## Workflow

### Step 1 — Explore the Scoped Source

For each source directory in the scope:

1. `Glob("src/{Project}/**/*.cs")` to get all C# files
2. `Grep` for key patterns: `class`, `interface`, `record`, `enum` declarations
3. Read the main entry points: `Program.cs`, `*ServiceCollectionExtensions.cs`, `*Command.cs`
4. Read `lode/{domain}/summary.md` if it exists — for prior context
5. Scan for dependency/infrastructure artifacts (feeds `dependency-map`, `external-integrations`, `runtime-pinning` perspectives):
   - `Glob("src/{Project}/**/*.csproj")` — NuGet manifests
   - `Glob("**/Directory.Packages.props")` — Central Package Management
   - `Glob("**/appsettings*.json")` — infrastructure config (exclude `appsettings.Test*.json`)
   - `Glob("**/Dockerfile*")` — runtime images
   - Read `global.json` if present — SDK version pin

Build a mental model of:
- Major components (namespaces / subdirectories)
- Entry points (CLI commands, API controllers)
- External dependencies (HttpClient, database, Playwright, Ollama, etc.)
- Data flow (what calls what)
- Storage systems (SQLite, file system, in-memory caches)
- NuGet package landscape (first-party, Microsoft, third-party; see [`references/dependency-scanning.md`](references/dependency-scanning.md)), plus the infra/runtime artifacts from the scan above

### Step 2 — Select Applicable Perspectives

Evaluate each perspective against the codebase. Only generate perspectives that apply.

| Perspective              | When to create                                                    | Mermaid type      |
|--------------------------|-------------------------------------------------------------------|-------------------|
| `overall-architecture`   | Always                                                            | `graph TB`        |
| `data-flow`              | Project has data processing, DB access, or file I/O               | `flowchart TD`    |
| `dependency-map`         | Project has 3+ internal project references OR NuGet packages to classify | `graph LR` |
| `command-surface`        | Project has CLI commands (System.CommandLine)                      | `graph LR`        |
| `pipeline`               | Project has multi-stage processing (extraction, conversion, indexing) | `flowchart TD` |
| `external-integrations`  | Project calls external APIs or services, or has infra deps in config | `graph LR`     |
| `state-transitions`      | Project has stateful workflows (fetch states, processing states)  | `stateDiagram-v2` |
| `storage`                | Project uses 2+ storage systems (SQLite, file system, etc.)       | `classDiagram`    |
| `runtime-pinning`        | Dockerfiles or `global.json` exist -- SDK pins, target frameworks, images | `graph LR` |

Print the selected AND skipped perspectives before generating:
- "Selected perspectives: overall-architecture, data-flow, pipeline, external-integrations, storage"
- "Skipped perspectives: dependency-map (fewer than 3 internal deps), state-transitions (no stateful workflows)"

Always log a reason for each skipped perspective — this enables quality audits and retros.

### Step 3 — Generate Diagrams with Recursive Drill-Down

For each selected perspective:

#### 3a. Generate the top-level diagram

Create a Mermaid diagram following the template conventions (see Diagram Conventions below). Each node represents a major component.

#### 3b. Classify elements

For each element in the diagram:
- **Leaf**: single file, trivial wrapper, or external dependency — stop here
- **Group**: directory with multiple sub-components — drill down

#### 3c. Recursive drill-down (groups only)

For each group element:
1. Read the corresponding source directory
2. Generate a sub-diagram showing internal structure
3. Classify each child as leaf or group
4. Recurse into child groups (max depth: 3 levels)
5. Write annotations for each element

#### 3d. Write annotations

For each element (leaf or group), write structured annotations:

```markdown
### {Element Name}
**Description:** {what it does and which files it covers}
**Context:** {why it exists, design rationale}
**Constraints:** {limitations, invariants}
**Concerns:** {risks, tech debt, known issues}
**TODOs:** {planned improvements}
**Notes:** {other observations}
```

Omit empty fields — only include fields with meaningful content. At minimum, **Description** and **Context** are always populated.

#### 3e. Dependency-specific enrichment (for applicable perspectives)

When generating `dependency-map`, `external-integrations`, or `runtime-pinning` perspectives, perform additional scanning per [`references/dependency-scanning.md`](references/dependency-scanning.md):

- **dependency-map:** Get the canonical project list with `scripts/solution-inventory.sh --json` (filter `kind == "src"`), then parse each project's `.csproj` for `PackageReference` entries. Classify packages (Microsoft/Azure, first-party, third-party). Build per-project package tables, a cross-project dependency heat map (packages shared by 2+ projects, sorted by count), and a cross-project dependency summary. Include service integration detection (`.Api.Client` patterns).
- **external-integrations:** Grep `appsettings*.json` for infrastructure patterns (SQL, Redis, RabbitMQ, etc.). Distinguish confirmed (config pattern found) vs inferred (package dep only). Add infrastructure summary table.
- **runtime-pinning:** Collect `global.json` SDK pin, `TargetFramework` from `.csproj`, Docker `FROM` instructions, CI pipeline SDK tasks. Build a runtime version matrix. Flag unpinned images and SDK mismatches.

### Step 4 — Validate Each Diagram (structural self-check)

Before writing, verify each Mermaid code block passes a structural self-check:

1. The first non-empty line matches the expected diagram type keyword for the selected perspective (e.g. `graph TB`, `flowchart TD`, `stateDiagram-v2`, `classDiagram`, `sequenceDiagram`)
2. Every `-->` or `---` edge has a label (see Diagram Conventions)
3. Any `classDef` blocks precede the nodes that use `:::` class assignment
4. Parentheses, brackets, and quotes are balanced
5. No node ID appears in a `classDef` line (common cut-paste error)

If any check fails, fix the diagram before proceeding. Do not write syntactically invalid diagrams to files.

> **Note:** The `mcp__claude_ai_Mermaid_Chart` MCP is not available in this environment. This structural self-check is the substitute.

### Step 5 — Write Output Files

See [references/output-template.md](references/output-template.md) for the per-perspective markdown template and HTML companion generation rules.

### Step 6 — Update Lode Index

After writing all perspective files, update `lode/lode-map.md` to add entries for the new `architecture/` files under the relevant domain section. If the domain maintains its own sub-map file (`lode/{domain}/lode-map-{domain}.md`), update that too.

### Step 7 — Report to User

After completing all work, report a structured summary:

```
arch-scan: {scope} — done

Output:
  {list of .md files written}
  {list of .html files written, if any}

Perspective: {perspective name}
Projects scanned: {N}
Internal edges: {N ProjectReferences}

{For dependency-map:}
Top shared dependencies:
  1. {package} — {N projects}
  2. {package} — {N projects}

Infrastructure: {comma-separated list}

Hub projects (highest fan-in):
  {project} — {N} dependents
  {project} — {N} dependents

{For other perspectives:}
Key findings: {1-3 bullet points}
```

---

## Diagram Conventions

See [references/diagram-conventions.md](references/diagram-conventions.md) for node styling, labels, element IDs, edges, size limits, and diagram direction.
