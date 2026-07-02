# Repo-Map Wiki — File Conventions Reference

Per-file content requirements and Mermaid diagram type guidance.
Applies to both HTML (default) and markdown output formats.

## Every File Has

1. **Title** — identifies the page and product area
2. **One-sentence summary** — concise description of the page's content
3. **ISO 8601 timestamp** — when the page was last updated
4. **Mermaid diagrams** — visual-first, prose supports diagrams (not the other way round)
5. **Tables** for structured reference data
6. **Concise prose** — no filler, every paragraph earns its place

### HTML format (default)

```html
<header>
  <h1>System Architecture</h1>
  <p class="subtitle">Two core services backed by PostgreSQL and RabbitMQ, deployed to Azure via Bicep. -- Updated 2026-04-08</p>
</header>
```

Mermaid diagrams use `<pre class="mermaid">` (rendered client-side by Mermaid.js):
```html
<pre class="mermaid">
graph TB
  A[Service A] --> B[Service B]
</pre>
```

Tables use `<th data-col="N">` for sortable columns (effectiveness.js handles click sorting):
```html
<table>
  <thead><tr><th data-col="0">Name</th><th data-col="1">Purpose</th></tr></thead>
  <tbody><tr><td>MyApp.Core</td><td>Shared library</td></tr></tbody>
</table>
```

### Markdown format (`--format md`)

```markdown
# System Architecture — MyProduct

> **One sentence:** Two core services backed by PostgreSQL and RabbitMQ, deployed to Azure via Bicep.

*Updated: 2026-04-08*

---
```

## index.md (Pyramid Entry Point)

Must contain:
- **Semantic Index** — table mapping roles to their starting document:
  ```
  | I am a... | I want to... | Go to |
  ```
- **Pyramid Summary** — five levels of increasing detail:
  - Level 1 — One Sentence
  - Level 2 — One Paragraph
  - Level 3 — Key Capabilities (bullet list)
  - Level 4 — Architecture at a Glance (Mermaid diagram)
  - Level 5 — Go Deeper (links to full docs)
- **Document Map** — ASCII tree of the full docs/ structure with annotations
- **What Is This?** — one-paragraph product description

## overview/executive.md

Must contain:
- **The Problem** — business context and pain points
- **The Solution** — concise product pitch
- **Business Capabilities** — Mermaid `journey` for key workflows
- **Technical Architecture in Plain English** — non-technical summary
- **Key Risks & Mitigations** — table
- **Compliance Posture** — bullet list
- **Technology Investment Summary** — table with rationale column

## overview/strategy.md

Must contain:
- **Strategic Positioning** — where the product sits in the market
- **Key Trade-offs** — table with choice, rationale, and alternatives
- **Competitive Landscape** — if applicable
- **Growth Vectors** — expansion paths

## overview/product.md

Must contain:
- **Personas** — Mermaid `mindmap` of user types and their tasks
- **Feature Domains** — one section per domain with feature table
- **Key User Journeys** — Mermaid `flowchart TD` for 3-4 primary flows
- **Feature Matrix by Role** — table with role columns
- **Supported Systems** — if applicable, table of integrations

## architecture/system.md

Must contain:
- **Component Map** — comprehensive Mermaid `graph TB` showing all services, infra, and connections
- **Architectural Layers** — numbered layer descriptions with tables
- **Inter-Service Communication** — Mermaid diagram + rules
- **Multi-Tenancy Model** — if applicable
- **Authentication & Authorization** — Mermaid `sequenceDiagram`
- **Observability Stack** — Mermaid diagram
- **Technology Decisions Summary** — table with alternatives considered

## architecture/data-flow.md

- **Data Flow Diagram** — Mermaid of how data moves through the system
- **Ingress Patterns** — how data enters (API, imports, events)
- **Processing Pipelines** — transformation steps
- **Egress Patterns** — how data leaves (exports, notifications, reports)
- **Data Lifecycle** — retention, archival, deletion

## architecture/storage.md

- **Storage Landscape** — Mermaid of all data stores
- **Database model** — per-tenant, shared, or hybrid
- **Data Access Pattern** — ORM, query tools, conventions
- **Key Database Objects** — important tables/collections
- **Schema Management** — migration strategy, script conventions
- **Entity Relationships** — Mermaid `erDiagram`

## architecture/deployment.md

- **Resource Architecture** — Mermaid of cloud resources
- **IaC Modules** — Terraform/Bicep module inventory
- **CI/CD Pipeline** — branch strategy, auto-deployment triggers, build commands
- **Provisioning** — how new tenants/environments are created
- **Secrets Management** — Key Vault / secret store inventory
- **Monitoring & Alerting** — tools and what they track

## architecture/integration-patterns.md

- **Messaging Architecture** — Mermaid of publishers, queues, consumers
- **Message Pipeline** — middleware/filters applied to messages
- **Key Message Categories** — table of message types by domain
- **Connector Pattern** — if applicable, import/sync flow
- **External Service Integrations** — table of external APIs
- **CQRS/Event Pipeline** — if applicable, decorator chain

## architecture/security.md

- **Authentication** — Mermaid `sequenceDiagram` for login flow
- **Authorization** — mechanisms table
- **API Security** — rate limiting, CORS, input validation
- **Data Security** — encryption-at-rest, in-transit, file safety
- **Multi-Tenancy Security** — isolation controls
- **Middleware Pipeline** — Mermaid `flowchart` of request processing

## engineering/getting-started.md

- **Prerequisites** — tools table with versions
- **Repository Setup** — clone commands
- **Building & Running** — per-project build instructions
- **Architecture Quick Reference** — condensed directory tree
- **Key Patterns** — code examples of dominant patterns
- **Development Workflow** — branch strategy, CI/CD, feature environments

## engineering/codebase-map.md

- **Project Inventory** — every project with language, framework, purpose
- **Dependency Graph** — Mermaid of project references
- **Directory Structure** — annotated tree
- **Shared Libraries** — what each shared project provides

## engineering/testing-strategy.md

- **Test Projects** — inventory table
- **Testing Patterns** — code examples (AAA, etc.)
- **CI/CD Quality Gates** — table of gates
- **Test Coverage** — honest assessment per area

## guides/configuration.md

- **Configuration Sources** — files, env vars, Key Vault
- **Options Classes** — table of options and their keys
- **Environment-Specific Overrides** — per-environment settings
- **Secrets** — what is secret vs config

## reference/glossary.md

- **Terminology Table** — term, definition, see-also
- **Acronyms** — expanded and explained

## reference/cli-commands.md

- **Command Table** — command, flags, description
- **Common Workflows** — 5-8 task-oriented command sequences

## Mermaid Diagram Types by Purpose

| Purpose                    | Diagram Type      | Example                               |
| -------------------------- | ----------------- | ------------------------------------- |
| System topology            | `graph TB`        | All services and infrastructure       |
| Capability overview        | `mindmap`         | Business domains and features         |
| User workflow              | `journey`         | Satisfaction-scored user stories      |
| Request flow               | `sequenceDiagram` | Auth flow, import pipeline            |
| Decision flow              | `flowchart TD`    | User journeys with branches           |
| Data relationships         | `erDiagram`       | Entity models                         |
| Technology overview        | `graph LR`        | Protocol/stack connections            |
| Language distribution      | `pie`             | Codebase composition                  |
| Git workflow               | `gitgraph`        | Branch strategy                       |

## Quality Checklist

Before reporting completion:

- [ ] index.md semantic index links all resolve to real files
- [ ] index.md contains pyramid summary (Level 1-5)
- [ ] Every file has H1 title, one-sentence tag, ISO timestamp, and `---` separator
- [ ] Every file has at least one Mermaid diagram
- [ ] No `README.md` used as an output file (only `index.md`)
- [ ] No `services/` directory exists (per-service docs are in `architecture/` or `engineering/`)
- [ ] Service count, controller count, and connector count are accurate
- [ ] No placeholder text ("TODO", "TBD", "fill in later")
- [ ] File names are kebab-case
- [ ] No file exceeds 400 lines
- [ ] Architecture overview diagram includes all services from the component map
- [ ] Semantic index table covers at least 8 roles
