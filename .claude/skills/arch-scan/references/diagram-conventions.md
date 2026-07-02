# Arch-Scan -- Diagram Conventions

## Node Styling

Use `classDef` to categorize elements visually:

```mermaid
classDef external fill:#585b70,color:#cdd6f4
classDef concern fill:#f38ba8,color:#1e1e2e
classDef entry fill:#89b4fa,color:#1e1e2e
classDef store fill:#a6e3a1,color:#1e1e2e
classDef core fill:#cba6f7,color:#1e1e2e
```

| Class      | Use for                              | Color    |
|------------|--------------------------------------|----------|
| `external` | Third-party services, APIs           | Gray     |
| `concern`  | Risks, bottlenecks, tech debt        | Red      |
| `entry`    | Entry points, CLI commands           | Blue     |
| `store`    | Persistent storage (DB, files)       | Green    |
| `core`     | Core domain logic                    | Purple   |

Apply classes with `:::` syntax: `node-id["Label"]:::external`

## Node Labels

Two-line labels with display name and source path:
```
element-id["Display Name\nsrc/Project/Namespace/"]
```

## Element IDs

Kebab-case matching source directory names: `extraction-service`, `embedding-repo`, `delta-client`

## Node Shapes

| Shape | Syntax | Use for |
|-------|--------|---------|
| Rectangle | `id["Label"]` | Services, components, projects |
| Cylinder | `id[("Label")]` | Databases, persistent storage |
| Stadium | `id(["Label"])` | Message queues, event buses |
| Hexagon | `id{{"Label"}}` | Decision points, gateways |

## Edges

Every edge requires a meaningful label:
```
A -->|"processes"| B
```

**Edge label specificity** (from dep-map pattern):
- Dependency maps: label with the integration mechanism, not just "refs"
  - NuGet: the package name (e.g. `"System.CommandLine"`)
  - ProjectReference: `"refs"` (acceptable when all edges are the same type)
  - HTTP client: `"HTTP: {TypedClientName}"`
  - Message queue: `"MQ: {queue/topic name}"`
- When all edges in a diagram are the same type (e.g. all ProjectReferences), a uniform label like `"refs"` is acceptable

## Node ID Rules

- **Alphanumeric, hyphens, and underscores only** -- no dots, no spaces
- Sanitize dotted names to kebab-case: `MyApp.Core` → `myapp-core`, `Foo.Bar.Baz` → `foo-bar-baz`
- IDs are kebab-case matching source directory names where possible

## Size Limits

- **25 nodes max per diagram.** For scopes with more, group minor items into a single "Other" node
- Subgraphs count toward the limit -- each subgraph is free, but its children count as nodes
- If a heat map or aggregate view would exceed 25 nodes, split into domain-scoped sub-diagrams

## Diagram Direction

- `graph TB` -- component architectures (top-down hierarchy)
- `graph LR` -- dependency maps, integration flows (left-to-right)
- `flowchart TD` -- pipelines, data flows (top-down sequence)
- `sequenceDiagram` -- request/response flows between components
- `classDiagram` -- storage layers, type hierarchies
- `stateDiagram-v2` -- state machines, workflow transitions
