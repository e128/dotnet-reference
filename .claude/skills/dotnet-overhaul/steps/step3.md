# Step 3: Modernize

Two areas: **Project Infrastructure** and **Language Usage**. Launch all 6 agents in a single parallel message — infrastructure and language analysis are independent reads and have no data dependencies between them.

---

## Project Infrastructure

**Agent 0 — TFM & packages:**
```
FIRST: check if project targets latest stable .NET:
- Run `dotnet --list-sdks` and `dotnet --list-runtimes`.
- Ask sme-researcher: "What is the latest stable (GA) .NET version and its LTS/STS status?"
- Scan all .csproj files for <TargetFramework>.
- If older TFM found (e.g., net8.0 when net10.0 is latest stable): flag as HIGH for upgrade.
- TFM upgrade scope: all .csproj TargetFramework values, global.json (if present),
  Dockerfiles base image tags, any TFM references in build scripts or CI pipelines.
- Apply TFM upgrade BEFORE other infrastructure findings — package updates and Dockerfile
  checks should target the new TFM.

THEN check remaining infrastructure:
- Outdated packages: `dotnet list <solution-file> package --outdated`
- Deprecated packages: `dotnet list <solution-file> package --deprecated`
  For each deprecated System.* or Microsoft.* package, determine correct action:
  - Project targets .NET 6+ only AND types are in shared framework → REMOVE the reference
  - Replacement package exists → MIGRATE (e.g., System.Data.SqlClient → Microsoft.Data.SqlClient)
  - Multi-target and .NET Framework needs it → conditional <ItemGroup>
  Reference the `dotnet-maintenance-packages` skill for known migration paths.

Common deprecated packages absorbed into .NET 6+:
  System.Memory, System.Buffers, System.ValueTuple, System.Runtime.CompilerServices.Unsafe,
  System.Threading.Tasks.Extensions, System.Numerics.Vectors, Microsoft.Bcl.HashCode,
  System.Reflection.DispatchProxy

Packages requiring migration (not just removal):
  System.Data.SqlClient → Microsoft.Data.SqlClient
  System.Json → System.Text.Json
  WindowsAzure.Storage → Azure.Storage.* (check sme-researcher for current replacement)
```

**Agent 0b — Dockerfile review:**
```
Glob for all Dockerfiles: **/Dockerfile*, **/docker-compose*.yml

For each Dockerfile:
- Floating tags: FROM using `:latest`, `-latest`, or no tag → replace with specific stable version
  (e.g., `10.0-alpine`). Ask sme-researcher for the latest stable tag if unsure.
- Base image tag should match the project's target framework (net8.0 → sdk:8.0-alpine). Flag mismatches.
- Digest pin freshness: if FROM uses @sha256, ask sme-researcher to check for a newer digest.
- Consistency across Dockerfiles: compare base image tags/digests, ENV vars, ARGs, user/group setup,
  and ENTRYPOINT patterns. Flag divergences not justified by different app requirements.
- Unused ARG declarations (declared but never referenced).
- Stale comments referencing old TFMs (e.g., net6.0 comment when project targets net8.0).
- Commented-out debug code (RUN echo, tree, COPY debug lines).
- Missing non-root user setup (USER instruction should exist; user/group should be created if not
  present in the base image).
- Pinned tool/agent versions in ENV or RUN (e.g., DataDog tracer) — flag for version check.
- Security: no secrets or credentials in ENV/ARG; COPY --chown used appropriately.
- Best practices: .dockerignore exists; HEALTHCHECK present if applicable; EXPOSE matches app port.
```

### Infrastructure Findings Table

**ID prefix: `MI`**

| ID | Finding | Severity | Category |
|----|---------|----------|----------|
| MI0 | Project targets net8.0 → upgrade to net10.0 | HIGH | TFM upgrade |
| MI1 | 12 packages have newer versions available | MEDIUM | Package updates |
| MI7 | Dockerfile FROM uses `:latest` → pin to `10.0-alpine` | HIGH | Dockerfile floating tag |
| MI8 | `System.Memory` referenced but project targets net10.0 only → remove | MEDIUM | Deprecated (remove) |
| MI9 | `System.Data.SqlClient` → migrate to `Microsoft.Data.SqlClient` | HIGH | Deprecated (migrate) |

**Severity:**
- HIGH: Outdated TFM, security-relevant package updates, deprecated packages requiring migration,
  Dockerfile missing non-root user, floating `:latest` tags
- MEDIUM: Deprecated packages to remove, outdated packages, Dockerfile TFM mismatch, pinned tool versions
- LOW: Minor bumps with no functional changes, stale Dockerfile comments, unused ARGs, debug code
- INFO: Intentionally pinned packages or justified Dockerfile divergences

**Package rules:**
- For major version bumps: accumulate all of them, then send a **single batched `sme-researcher` query** — "Check breaking changes for: PackageA v2→v3, PackageB v6→v7, ..." — rather than one call per package
- `dotnet build` after package updates; regenerate lock files if present (`--force-evaluate`)
- For deprecated packages, run `dotnet list <solution-file> package --include-transitive` after removal
  to verify they weren't re-introduced by transitive dependencies

---

## Language Usage

Launch 5 `Explore` agents in parallel:

**Agent 1 — Immutability conversions:**
```
Search for mutable types and collections that should be immutable:

Classes:
- Classes with only get/set auto-properties and no behavior → convert to `record`
- Classes with only get/init properties → convert to `record` or `readonly struct`
- Structs without `readonly` modifier → add `readonly` if all fields are readonly
- Properties with `set` that could be `init` → convert to `init`-only

Collections (return types, fields, parameters):
- `List<T>` fields/properties exposed publicly → `IReadOnlyList<T>` or `ImmutableArray<T>`
- `Dictionary<TK,TV>` fields used as lookup-only → `FrozenDictionary<TK,TV>` or `IReadOnlyDictionary<TK,TV>`
- `HashSet<T>` fields used as lookup-only → `FrozenSet<T>` or `IReadOnlySet<T>`
- `T[]` returns from public methods → `IReadOnlyList<T>`, `ReadOnlySpan<T>`, or `ImmutableArray<T>`
- Method parameters accepting `List<T>` when `IReadOnlyList<T>` or `IEnumerable<T>` suffices
- `new List<T>()` immediately assigned to a readonly/immutable target → collection expression `[]`
  or `.ToImmutableArray()` / `.ToFrozenSet()`

Packages to suggest adding (if conversions warrant):
- `System.Collections.Immutable` — `ImmutableArray<T>`, `ImmutableList<T>`, `ImmutableDictionary<TK,TV>`
- `System.Collections.Frozen` (in-box .NET 8+) — `FrozenSet<T>`, `FrozenDictionary<TK,TV>`

Do NOT flag:
- Private mutable state behind an immutable public API (encapsulation is fine)
- Builder patterns or object pools that require mutability
- Entity Framework DbContext models (EF requires mutable properties)
- ViewModels/DTOs that frameworks mutate via reflection
```

**Agent 2 — Primary constructors & expression bodies:**
```
Search for classes with simple constructors that assign parameters to fields.
Search for single-line method bodies that could use expression-bodied syntax.
Search for `record` candidates not caught by Agent 1 (data types with value equality).
```

**Agent 3 — Collection expressions & pattern matching:**
```
Search for `new List<T> { ... }`, `new[] { ... }`, `Array.Empty<T>()` → collection expressions [].
Search for `x == null` / `x != null` → `is null` / `is not null`.
Search for nested if/else chains → switch expressions or pattern matching.
Search for `as` + null check → `is` pattern.
```

**Agent 4 — Modern API replacements:**
```
Search for `String.IsNullOrEmpty` / `IsNullOrWhiteSpace` where `is [not] null or []` fits.
Search for `.Count() == 0` / `.Count() > 0` / `.Any()` on types with `.Count` or `.Length`.
Search for `Enum.Parse` without the generic overload.
Search for manual `StringBuilder` loops where `string.Join` or `string.Create` works.
Search for `DateTime.Now` where `DateTime.UtcNow` or `TimeProvider` is more correct.
Search for `""` empty string literals → `string.Empty`.
```

**Agent 5 — using/disposal, nullability & xUnit v3 migration:**
```
Search for `IDisposable`/`IAsyncDisposable` objects created without `using` statements.
Search for `#nullable disable` or missing nullable annotations on public APIs.
Search for the null-forgiving operator (!) usage that masks real nullable issues.
Search for `IAsyncLifetime` implementations returning `Task` instead of `ValueTask`
  (xUnit v3 changed InitializeAsync/DisposeAsync to return ValueTask).
```

### Language Findings Table

**ID prefix: `M`**

| ID | Finding | Severity | Category |
|----|---------|----------|----------|
| M1 | `OrderService` class → `record` (immutable data, value equality) | MEDIUM | Immutability |
| M2 | `List<string>` property on `Config` → `IReadOnlyList<string>` | MEDIUM | Immutability |
| M3 | `Dictionary<string,T>` lookup field → `FrozenDictionary<string,T>` | MEDIUM | Immutability |
| M4 | `set` property on immutable type → `init` | LOW | Immutability |
| M5 | `new List<string> { ... }` at ValidationHelper.cs:42 → collection expression | LOW | Collection expression |
| M6 | `IDisposable` not disposed in FileProcessor.cs:88 | HIGH | Resource leak |

**Severity:**
- HIGH: Resource leaks, correctness issues (wrong DateTime kind), nullable holes
- MEDIUM: Immutability conversions (prevent mutation bugs, improve thread safety), null check modernization
- LOW: Pure style modernization (primary constructors, expression bodies, collection expressions)
- INFO: Acknowledged intentional patterns

**Package additions:** If immutability conversions require `System.Collections.Immutable` and it's not
already referenced, add it to `Directory.Packages.props` and the relevant `.csproj`. `FrozenSet<T>` and
`FrozenDictionary<TK,TV>` are in-box for .NET 8+ (no additional package needed).
