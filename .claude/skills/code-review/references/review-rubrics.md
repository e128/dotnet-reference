# Code Review Rubrics

Reference checklist tables for the code review workflow. Each section is injected into agent prompts
based on the diff content (controllers → security, classes → SOLID, test files → test quality, etc.).

## Security Checklist (Controllers & Business Logic)

Include these rules when the diff touches controllers, command handlers, or business logic:

```
SECURITY RULES (include when diff touches controllers/handlers/business logic):

Controllers:
  [CRITICAL] Public action missing [Authorize] attribute
  [CRITICAL] Returning raw entity instead of DTO
  [HIGH] Request object without validation
  [HIGH] ID not validated against current user's tenant

Commands/Handlers:
  [CRITICAL] SQL built with string concatenation — must use parameterized queries
  [HIGH] Multi-step DB ops without TransactionScope
  [HIGH] Mass assignment from input (binding directly to entity)
  [HIGH] Missing FluentValidation

Business Logic:
  [HIGH] No null checks on inputs
  [MEDIUM] Exceptions without context (generic messages)
  [HIGH] Missing tenant isolation in repo calls
  [MEDIUM] Missing audit logging on sensitive operations

General:
  [CRITICAL] Hardcoded secrets or credentials
  [HIGH] TODO/HACK/FIXME comments in new code
  [MEDIUM] Large commented-out blocks
```

## SOLID Design Review

Include these rules when the diff introduces new classes, interfaces, or modifies class structure:

```
SOLID DESIGN RULES (include when diff adds/modifies classes, interfaces, or DI registrations):

Single Responsibility (SRP):
  [MEDIUM] New class with multiple unrelated public method groups (e.g., auth + persistence + notification)
  [MEDIUM] Class handling both orchestration and data access in same methods
  [LOW] God class indicators: >300 lines with mixed concerns

Open-Closed (OCP):
  [MEDIUM] Switch/if-chain on type discriminator that must be modified to add new variants
  [LOW] Could be replaced with polymorphism + DI registration

Liskov Substitution (LSP):
  [HIGH] Override that throws NotSupportedException or NotImplementedException
  [HIGH] Subtype that narrows preconditions or widens postconditions vs base contract
  [MEDIUM] Override that ignores base class behavior entirely

Interface Segregation (ISP):
  [MEDIUM] Interface with >7 methods — consider splitting by capability
  [MEDIUM] Implementer with no-op methods to satisfy interface contract
  [LOW] Client depending on interface but using <50% of its methods

Dependency Inversion (DIP):
  [HIGH] `new` instantiation of service types that should be injected (see DI & lifetime rules)
  [MEDIUM] High-level class depending on concrete low-level class instead of abstraction
  [LOW] Static helper taking dependencies as parameters instead of constructor injection
```

**Balance with YAGNI:** Do not flag single-implementation interfaces as ISP violations. Do not flag simple utility classes as SRP violations. SOLID is a guideline for reducing coupling, not a checklist to enforce mechanically.

## Pike's Rules Review (Simplicity & Data Design)

Apply [Rob Pike's 5 Rules](../../lode/rob-pikes-rules.md) to every review. These catch over-engineering that SOLID alone misses.

```
PIKE'S RULES (include in every code review):

Premature optimization:
  [HIGH] Performance optimization without benchmark evidence — "it might be slow"
         is not justification. Require benchmark baseline comparison. (Rules 1-2)
  [MEDIUM] Caching, pooling, or pre-computation added without measured bottleneck (Rules 1-2)

Over-engineering:
  [MEDIUM] Fancy algorithm when n is small — linear scan, hash map, or array would
           suffice. Ask "what's n?" before accepting trees, heaps, or custom structures. (Rules 3-4)
  [MEDIUM] Abstraction layer with no current second consumer — YAGNI + Rule 4
  [LOW] Complex generic constraints when a concrete type would work (Rule 4)

Data design:
  [MEDIUM] Algorithm-first design — types feel wrong but logic is clever. Suggest
           restructuring data structures first; algorithms should follow naturally. (Rule 5)
  [MEDIUM] Parallel collections (List<A> + List<B> + List<C>) instead of List<(A,B,C)>
           or a record type — data not organized well. (Rule 5)
```

## Design Priority Order Review

Apply CLAUDE.md's **Design Priority Order** to every diff touching .NET classes, business logic, or data processing. Violations of higher-priority principles are more severe than violations of lower ones — a parallelism issue caused by mutable shared state is not a parallelism problem, it is an immutability problem.

```
DESIGN PRIORITY ORDER (include when diff adds/modifies classes, business logic, or data processing):

Immutability — Priority 1 (most important):
  [HIGH] Mutable `set` property where `init` would suffice — mutability requires justification
  [HIGH] New mutable shared state in a class that participates in async or parallel code
  [MEDIUM] Public `List<T>` or `Dictionary<K,V>` property instead of `IReadOnlyList<T>` /
           `IReadOnlyDictionary<K,V>` — expose the narrowest mutable surface
  [MEDIUM] `record` with mutable `set` properties — use `init` unless mutation is explicitly
           required and documented
  [LOW] Mutable local variable assigned exactly once — consider `readonly` field or `const`

Memory Efficiency — Priority 2:
  [HIGH] LINQ chain with `.ToList()` mid-chain when it feeds another LINQ query —
         unnecessary intermediate allocation
  [HIGH] String concatenation in a loop — use `StringBuilder` or interpolated `$""` spans
  [MEDIUM] Large object allocation (>85KB) in a hot path — goes to LOH and pressures GC;
           pool or chunk instead
  [MEDIUM] `new List<T>()` or `new Dictionary<K,V>()` without capacity hint when size is
           known or bounded — avoids repeated doubling reallocations
  [MEDIUM] `Span<T>` / `Memory<T>` opportunity missed on array-backed or stack-friendly data

CPU Efficiency — Priority 3 (only after allocations are minimized):
  [HIGH] O(n²) or worse algorithm where O(n) or O(n log n) exists — nested loops over the
         same collection, or repeated linear scans of a list that should be a HashSet
  [MEDIUM] Multiple enumerations of the same non-materialized LINQ query —
           `.Count()` then `.Any()` or two `foreach` passes; materialize once
  [MEDIUM] `OrderBy(...).First()` when `MinBy` / `MaxBy` avoids the full sort
  [LOW] Micro-optimization without benchmark evidence (measurement required per CLAUDE.md
        Rules 1–2; "it might be slow" is not justification)

Parallelism — Priority 4 (last resort; emerges from clean functional code):
  [CRITICAL] `Parallel.ForEach`, `Task.WhenAll`, or channels introduced on code with mutable
             shared state — fix the mutability first; concurrency is not the root problem
  [HIGH] Parallelism added before establishing whether the bottleneck is algorithmic or
         allocation-driven — verify O(n) baseline and heap profile first
  [MEDIUM] Lock scope wider than the mutation it protects — suggests the shared state is
           poorly modeled, not that the lock is wrong
```

**Priority triage rule:** If a finding sits at the intersection of two priority levels (e.g., a parallelism bug caused by mutable state), classify it at the **higher-priority violation** (immutability), not the surface symptom (parallelism). This directs the fix to the root cause.

## Test Quality Rubric

Include these rules when the diff touches test files:

```
TEST QUALITY RULES (include when diff touches test files):

  [HIGH] New public methods without unit tests
  [MEDIUM] Assert.NotNull() as sole assertion — must have meaningful assertions
  [MEDIUM] Missing edge cases (null, empty, boundary values)
  [MEDIUM] Only happy-path tested — error paths must be covered
  [MEDIUM] Test method names not following Method_Scenario_Expected pattern
  [HIGH] Test logic (if/for/while in tests) — tests should be linear
  [MEDIUM] Parameterized scenarios not using Theory/InlineData
```

## Code Reduction Review

**Always include this section.** Removing lines of code without changing unit test assertions is a high-value, low-risk win. Shorter code has fewer places to break and is easier to review.

```
CODE REDUCTION RULES (include in every code review):

Dead code / unreachable branches:
  [HIGH] Private method or field with no usages outside its own file
  [MEDIUM] if/else branch that is unreachable given the surrounding type constraints
  [MEDIUM] Catch block that only rethrows — remove the try/catch entirely
  [LOW] Local variable assigned but read in exactly one place — inline it (RCS1124)

LINQ simplification:
  [MEDIUM] .Where(pred).First() → .First(pred)  (same for FirstOrDefault, Single, Any, Count)
  [MEDIUM] .Where(pred).Select(proj) on the only consumer — combine to a single Select with guard
  [MEDIUM] .ToList() mid-chain feeding a second LINQ query with no intermediate branching
  [MEDIUM] .OrderBy(...).First() → .MinBy(...)  /  .OrderByDescending(...).First() → .MaxBy(...)
  [LOW] .Select(x => x) identity projection — remove it

Redundant abstraction:
  [MEDIUM] Single-line private method called in exactly one place — inline the body at the call site
  [MEDIUM] Local variable that aliases a parameter and is never mutated — use parameter directly
  [MEDIUM] Interface with exactly one implementation and no test-doubles — consider removing the interface
  [LOW] Wrapper type that adds no invariants over its inner type

Verbose language idioms with shorter equivalents:
  [MEDIUM] if (x == null) return null; else return x.Foo — use x?.Foo
  [MEDIUM] string.Format("...", x) or "..." + x in non-hot paths → interpolated string
  [MEDIUM] new List<T> { a, b, c } where an array [a, b, c] suffices (no Add needed)
  [MEDIUM] Manual null-check before ??= when ??= is cleaner
  [LOW] Explicit type annotation where var reduces noise without ambiguity

Test-file reduction (assertions only — never change assertion semantics):
  [MEDIUM] Multiple Assert.Equal calls that collapse to a single Assert.Equivalent or record comparison
  [MEDIUM] Arrange section building objects that a factory or TestData helper already produces
  [LOW] [InlineData] rows that duplicate a pattern — collapse with [MemberData] or a helper method

Golden rule: if removing the code leaves all existing unit test assertions unchanged and passing,
the removal is safe. Flag it as a MEDIUM unless it is dead code (HIGH) or a catch-rethrow (HIGH).
```

## State & Mutation Discipline (Clanker Discipline)

Include these rules when the diff introduces or modifies state types, data models, DTOs, options classes, boolean flags, or functions that manage application state. Full before/after examples: see [clanker-patterns.md](clanker-patterns.md).

```
STATE & MUTATION RULES (include when diff touches state types, models, DTOs, boolean flags, or mutable patterns):

Derive, don't store:
  [HIGH] New boolean field that can be derived from existing state or events —
         doubles theoretical state space with each flag added
  [MEDIUM] Mutable state visible beyond its minimal scope — trap in a closure or
           private inner class; class-level fields are the worst case
  [MEDIUM] Cached computed value without a clear invalidation path —
           if the source data changes, the cached value silently drifts

Make wrong states impossible:
  [HIGH] Optional-bag model (5+ optional fields) where a discriminated union or
         phased composition would make invalid states unrepresentable
  [MEDIUM] Sentinel value ('none', 'unknown', -1, '') where null is semantically correct
  [MEDIUM] Identical type aliases for different domain concepts (e.g., UserId = string,
           TeamId = string) — brand them or use distinct types
  [LOW] Dead type variant that is never constructed — delete it

Enforce function contracts:
  [HIGH] Function that both mutates its input and returns the same reference —
         callers cannot tell whether to use the return value or the original
  [MEDIUM] Pure function that quietly gained a side effect (DB write, logging,
           state mutation) — extract side effects into an orchestrator
  [MEDIUM] Semantic function (small, pure) growing into a pragmatic function
           (orchestrator with domain glue) — split before it spreads

Data over procedure:
  [MEDIUM] Long if/switch chain where every branch returns a similar shape —
           convert to a lookup table; data is easier to scan, extend, and test
```

**Activation heuristic:** Include this rubric when the diff contains any of: `bool `, `boolean`, `? `, `Optional<`, `| null`, `record `, `class ` with 3+ properties, `enum `, `status`, `state`, `flags`, `options`.

## Analyzer Suppression Audit

**Always include this section.** Every `#pragma warning disable`, `[SuppressMessage]`, or `.editorconfig`/`.globalconfig` severity downgrade in the diff must be challenged. Suppressions are technical debt with a justification label — the label may be wrong.

**How to audit:** For each suppression in the diff, extract the diagnostic ID and justification comment, then apply the challenge questions below. A justification passes only if it explains **why the analyzer's suggested fix is impossible or inappropriate** — not just what the warning means.

```
SUPPRESSION AUDIT RULES (include when diff contains #pragma warning disable, [SuppressMessage],
or editorconfig/globalconfig severity changes):

Missing or weak justification:
  [CRITICAL] Suppression with no justification comment — PUG0025 build error; must have //
             reason after the diagnostic ID
  [HIGH] Justification restates the warning ("suppress nullable warning") instead of
         explaining why the fix is inappropriate — ask: "what prevents fixing this properly?"
  [HIGH] Justification says "temporary" / "TODO" / "will fix later" — these never get fixed;
         either fix now or justify permanently

Suppression scope:
  [HIGH] File-level or assembly-level suppression ([assembly: SuppressMessage]) when the
         issue is in one method — scope must be as narrow as possible
  [HIGH] #pragma without matching #pragma warning restore — suppression bleeds to end of file
  [MEDIUM] Suppression covers multiple diagnostic IDs on one line (e.g., CA2007, MA0004) —
           each ID needs its own justification; bundling hides weak reasoning

Challenge questions (apply to every suppression):
  1. "Can the code be restructured to eliminate the warning entirely?"
     → If yes: [HIGH] — restructure instead of suppress
  2. "Is this a framework/tooling limitation (Dapper requires long, Blazor DTO shape,
     pre-DI startup) or a design choice?"
     → Framework limitation: acceptable if stated explicitly
     → Design choice: [MEDIUM] — challenge whether the design is correct
  3. "Does the suppressed analyzer catch a real bug category in this codebase?"
     → If PUG analyzer (PUG0004, PUG0015, etc.): [HIGH] — these exist because the
        pattern caused real bugs here; suppressing needs strong evidence
     → If third-party (CA*, SA*, MA*, RCS*): [MEDIUM] — still challenge, but lower bar
  4. "Is this the Nth suppression of the same diagnostic in this file?"
     → If 3+ suppressions of the same ID in one file: [HIGH] — the file likely needs
        a structural fix, not per-line suppressions

Severity downgrades in config:
  [CRITICAL] Diagnostic severity changed from error to warning or none in .editorconfig
             or .globalconfig — this silently affects the entire project/solution
  [HIGH] New "dotnet_diagnostic.XXXX.severity = none" entry — equivalent to suppressing
         everywhere; must justify why the rule is wrong for the whole project
```

**Adversarial stance:** The default assumption is that every suppression is wrong. The reviewer's job is to find the fix that eliminates the suppression. Only mark a suppression as acceptable if all four challenge questions have been answered and the justification names a specific, verifiable constraint (framework type requirement, API shape contract, pre-DI bootstrap sequence).
