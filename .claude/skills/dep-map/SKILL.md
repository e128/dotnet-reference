---
name: dep-map
description: "Generates or updates a lode dependency map for a scope. Scans all repos under a path for NuGet, npm/yarn, Maven, Go, Rust, and Python dependencies; detects infrastructure from config files; writes lode/<scope>/dep-map.md with per-repo Mermaid service-layer diagrams and a cross-repo dependency heat map."
when_to_use: "Trigger on: 'dep-map <scope>', 'map the dependencies for <scope>', 'generate a dep map for <path>', 'document the deps for <scope>', 'create a lode dependency doc for X', 'update the dependency map for Y', 'build a dependency map'."
argument-hint: "<scope-label> [<path>]"
arguments: scope path
allowed-tools: Read, Glob, Grep, Write, Edit, Bash
---

# dep-map

Generates or updates a structured lode dependency document for a scope. Scans all repos
under the given path for package dependencies (NuGet, npm/yarn, Maven, Go, Rust, Python),
detects infrastructure from config files, and writes `lode/<scope>/dep-map.md` with
per-repo Mermaid service-layer diagrams and a cross-repo dependency heat map.

## Usage

```
/dep-map auth
/dep-map payments src/services
/dep-map platform /path/to/repos/platform
```

`$scope` = scope label (used for lode path and headings). `$path` = scan root (optional).

---

## Path Resolution

| Concept     | Resolved as                                                                             |
| ----------- | --------------------------------------------------------------------------------------- |
| Scope label | `$scope` — used in lode path and headings                                               |
| Scan root   | `$path` if given, else `repos/<scope>/` if it exists, else cwd (use absolute paths in tool calls; never write them into lode output) |
| Lode output | `lode/<scope>/dep-map.md` (relative to cwd)                                             |
| Lode map    | `lode/lode-map.md` (relative to cwd)                                                    |

Use absolute paths in all tool calls. Resolve cwd at runtime with `pwd`.

---

## Workflow

### Step 1 — Parse and validate arguments

Extract scope from `$scope`. Any additional text is `$path`.

**If no argument:** explain usage with examples and stop.

**Resolve the scan root:**
1. If `$path` given, verify it exists. If not, report an error and stop.
2. If no path, check if `<cwd>/repos/<scope>/` exists. Use it if found.
3. Otherwise use `<cwd>` as the scan root.

**Identify repos to scan:**

A **repo** is any immediate subdirectory of the scan root containing at least one package manifest
(`.csproj`, `pom.xml`, `package.json`, `go.mod`, `Cargo.toml`). If the scan root itself contains
manifests (single-repo run), treat it as a single repo.

Run these Globs from the scan root to discover repos (depth ≤ 4):
```
<scan-root>/**/*.csproj
<scan-root>/**/pom.xml
<scan-root>/**/package.json   (exclude node_modules)
<scan-root>/**/go.mod
<scan-root>/**/Cargo.toml
```

Group results by the immediate subdirectory of the scan root.

---

### Step 2 — Check for existing lode doc

Check whether `lode/<scope>/dep-map.md` exists:
- **Exists:** update run — note the `*Updated:` timestamp
- **Missing:** create run — create lode directory if needed

Also read any existing scope lode files for service names and architecture facts:
- `lode/<scope>/architecture.md`
- `lode/<scope>/tech-stack.md`
- `lode/<scope>/services.md`

---

### Step 3 — Scan each repo

For each repo discovered, run sub-steps in parallel within each repo.
See [`references/manifest-parsing.md`](references/manifest-parsing.md) for full per-language parsing instructions:
- **3a** Detect language and locate manifests
- **3b** Parse NuGet (.csproj)
- **3c** Parse Maven (pom.xml)
- **3d** Parse npm/yarn (package.json)
- **3e** Parse Go modules (go.mod)
- **3f** Parse Rust crates (Cargo.toml)
- **3g** Parse Python (requirements.txt / pyproject.toml)
- **3h** Detect infrastructure from config files
- **3i** Build Mermaid service-layer diagram
- **3j** Detect runtime versions and Docker images

For classification rules, see [`references/dependency-classification.md`](references/dependency-classification.md).
For unusual repo structures, see [`references/edge-cases.md`](references/edge-cases.md).

---

### Step 4 — Compose the lode document

Write to `lode/<scope>/dep-map.md`. See [`references/output-template.md`](references/output-template.md) for the full document structure and writing rules.

Key rules:
- UTC timestamp on last line: `*Updated: 2026-04-25T14:32:00Z*`
- If updating, rewrite the file entirely — lode captures current state, not a diff
- Never embed absolute home-directory paths

---

### Step 5 — Update lode-map.md

Check `lode/lode-map.md` for an existing entry for `dep-map.md` under `<scope>/`.

If not listed, add:
```
dep-map.md — NuGet/npm/Maven/Go/Rust deps, Docker runtime images, SDK pins + service layer diagrams for all <N> repos
```

---

### Step 6 — Report to user

```
dep-map: <scope> — done

Output: lode/<scope>/dep-map.md
Repos scanned: N of T (K skipped — no manifests found)

Top cross-repo service dependencies:
  1. <service> — <N repos>
  2. <service> — <N repos>

Infrastructure in use: <comma-separated list>

Runtime images:
  Production: <unique base images + tags>
  Build/SDK:  <unique build images + tags>
  ⚠ Unpinned tags: <repos with :latest or no tag, or "none">

Most common third-party dependencies:
  .NET:   <top 3>
  Java:   <top 3>
  JS:     <top 3>
  Go:     <top 3 if present>
  Python: <top 3 if present>

Skipped repos: <list names, or "none">
```

---

## User input

$ARGUMENTS
