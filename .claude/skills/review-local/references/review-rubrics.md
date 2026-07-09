# Code Review Rubrics

Reference checklist tables for the code review workflow. Each section is injected into agent prompts
based on the diff content (controllers → security, classes → SOLID, test files → test quality, etc.).

## Security Checklist (Controllers & Business Logic)

Include when the diff touches controllers, command handlers, or business logic. Multi-tenant repo — tenant isolation is the highest-leverage check.

```
SECURITY RULES (include when diff touches controllers/handlers/business logic):

  [CRITICAL] Public action missing [Authorize] attribute
  [CRITICAL] Returning raw entity instead of DTO
  [CRITICAL] Hardcoded secrets or credentials
  [CRITICAL] SQL built with string concatenation — must use parameterized queries
  [HIGH] ID not validated against current user's tenant — multi-tenant isolation
  [HIGH] Missing tenant isolation in repo calls
  [HIGH] Mass assignment from input (binding directly to entity)
  [HIGH] Missing FluentValidation
  [HIGH] Multi-step DB ops without TransactionScope
  [HIGH] No null checks on inputs
  [HIGH] TODO/HACK/FIXME comments in new code
  [MEDIUM] Exceptions without context (generic messages)
  [MEDIUM] Missing audit logging on sensitive operations
  [MEDIUM] Large commented-out blocks
```

## SOLID Design Review

Model knows SOLID; this rubric lists **the severity calibration** + YAGNI guard, not the textbook restatement. Include when the diff introduces new classes, interfaces, or modifies class structure.

```
SOLID DESIGN RULES (include when diff adds/modifies classes, interfaces, or DI registrations):

  [HIGH] Override that throws NotSupportedException or NotImplementedException (LSP)
  [HIGH] Subtype that narrows preconditions or widens postconditions vs base contract (LSP)
  [HIGH] `new` instantiation of service types that should be injected (DIP — see DI & lifetime rules)

  [MEDIUM] New class with multiple unrelated public method groups (SRP)
  [MEDIUM] Class handling both orchestration and data access in same methods (SRP)
  [MEDIUM] Switch/if-chain on type discriminator that must be modified to add new variants (OCP)
  [MEDIUM] Interface with >7 methods — consider splitting by capability (ISP)
  [MEDIUM] Implementer with no-op methods to satisfy interface contract (ISP)
  [MEDIUM] Override that ignores base class behavior entirely (LSP)
  [MEDIUM] High-level class depending on concrete low-level class instead of abstraction (DIP)

  [LOW] God class indicators: >300 lines with mixed concerns (SRP)
  [LOW] Could be replaced with polymorphism + DI registration (OCP)
  [LOW] Client depending on interface but using <50% of its methods (ISP)
  [LOW] Static helper taking dependencies as parameters instead of constructor injection (DIP)
```

**YAGNI guard:** Do not flag single-implementation interfaces as ISP violations. Do not flag simple utility classes as SRP violations. SOLID is a guideline for reducing coupling, not a checklist to enforce mechanically.

## Pike's Rules Review (Simplicity & Data Design)

Apply Rob Pike's 5 Rules to every review. Catches over-engineering that SOLID alone misses.

```
PIKE'S RULES (include in every code review):

  [HIGH] Performance optimization without benchmark evidence — "it might be slow"
         is not justification. Require a benchmark baseline comparison. (Rules 1-2)
  [MEDIUM] Caching, pooling, or pre-computation added without measured bottleneck (Rules 1-2)
  [MEDIUM] Fancy algorithm when n is small — linear scan, hash map, or array would
           suffice. Ask "what's n?" before accepting trees, heaps, or custom structures. (Rules 3-4)
  [MEDIUM] Abstraction layer with no current second consumer — YAGNI + Rule 4
  [MEDIUM] Algorithm-first design — types feel wrong but logic is clever. Suggest
           restructuring data structures first; algorithms should follow naturally. (Rule 5)
  [MEDIUM] Parallel collections (List<A> + List<B> + List<C>) instead of List<(A,B,C)>
           or a record type — data not organized well. (Rule 5)
  [LOW] Complex generic constraints when a concrete type would work (Rule 4)
```

## Design Priority Order Review

Apply the repo's **Design Priority Order** to every diff touching .NET classes, business logic, or data processing. Violations of higher-priority principles are more severe than violations of lower ones — a parallelism issue caused by mutable shared state is not a parallelism problem, it is an immutability problem.

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
  [LOW] Micro-optimization without benchmark evidence (measurement required per Pike's
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

Include when the diff touches test files. "Tests are solid, no findings" is a valid output.

```
TEST QUALITY RULES (include when diff touches test files):

  CRITICAL — Tests that give false confidence:
  [CRITICAL] No assertions — test executes code but never asserts anything; proves nothing
  [CRITICAL] Swallowed exceptions — try/catch with empty catch block hides failures
  [CRITICAL] Always-true assertions — Assert.True(true), Assert.Equal(x, x), or conditions
             that can never fail regardless of implementation correctness
  [CRITICAL] Coverage touching — test class systematically calls every public method without
             asserting meaningful outcomes; inflates coverage metrics without testing behavior

  HIGH — Tests likely to cause pain:
  [HIGH] New public methods without unit tests
  [HIGH] Test logic (if/for/while in tests) — tests should be linear
  [HIGH] Flakiness indicators — Thread.Sleep/Task.Delay for synchronization, DateTime.Now
         without abstraction, Random without seed, environment-dependent paths
  [HIGH] Test ordering dependency — static mutable fields modified across tests,
         [ClassInitialize] that does not fully reset state, tests that fail individually
         but pass in suite (or vice versa)

  MEDIUM — Should fix for maintainability:
  [MEDIUM] Assert.NotNull() as sole assertion — must have meaningful assertions
  [MEDIUM] Missing edge cases (null, empty, boundary values)
  [MEDIUM] Only happy-path tested — error paths must be covered
  [MEDIUM] Over-mocking — more mock setup lines than actual test logic; verifying exact
           call sequences on mocks rather than outcomes; mocking types the test owns
  [MEDIUM] Implementation coupling — test asserts internal state, private field values,
           or exact method call counts rather than observable behavior
  [MEDIUM] Parameterized scenarios not using Theory/InlineData
  [MEDIUM] Test method names not following Method_Scenario_Expected pattern

  LOW — Nice to have:
  [LOW] Inconsistent naming conventions across test file
  [LOW] IDisposable test resources not disposed (implement IAsyncLifetime)
  [LOW] Unused test infrastructure (helper methods never called)
```

**Calibration notes:** Separate boundary tests are NOT duplicates — testing `0`, `1`, and `MaxValue` separately is correct. Test method naming is MEDIUM, not HIGH — names matter but working assertions matter more.

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

Include when the diff introduces or modifies state types, data models, DTOs, options classes, boolean flags, or functions that manage application state. Full before/after examples: [clanker-patterns.md](clanker-patterns.md).

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

## Yoda Principle — Command Naming & Integration Intent

Apply when the diff touches command handlers, service method signatures, integration points, message contracts, or public APIs. Name things by what they **do** (business intent), not what they **check**. A command is a request to perform a business action; a query is a request to return data. Commands named like queries (`VerifyX`, `CheckX`, `ValidateX`) hide intent, invite race conditions, and obscure the domain model.

```
YODA PRINCIPLE — COMMAND NAMING (include when diff touches command handlers, service
method signatures, integration points, message contracts, or public APIs):

Command-as-query naming:
  [HIGH] Command handler, service method, or message contract named with Verify/Validate/
         Check/Ensure prefix that actually performs a state change, orchestration step, or
         business action — rename to reflect the action (ReserveProducts, not VerifyProductExists)
  [HIGH] Command named like a predicate (returns bool true/false) when success/failure should
         be communicated through events, exceptions, or a result type — boolean commands hide
         partial-failure modes and make rejection invisible to callers
  [MEDIUM] Method named VerifyX or CheckX that is the only caller of a downstream command —
           the check is redundant; the command already validates internally; rename or inline

Check-then-act (TOCTOU race condition):
  [HIGH] Two-step integration pattern: VerifyX() returns true, then DoX() is called on the same
         resource — the state verified in step 1 can change before step 2 executes; merge into
         DoX() or make DoX() return a result communicating whether pre-conditions were met
  [HIGH] Service or orchestrator querying a module to pre-check state before issuing a command
         to that same module — the command module is the source of truth; let it reject
  [MEDIUM] Boolean guard variable populated from a query result, then checked before a command —
           the guard is stale by definition in async or concurrent code

Hidden business concept:
  [MEDIUM] Verify/Check method that is actually a domain operation with its own lifecycle
           (async steps, compensation, timeout) named as if it were a synchronous read —
           the name should reflect the operation (ReserveInventory, AcquireLock, ClaimSlot)
  [MEDIUM] Orchestrator that requires callers to invoke CheckX before DoX, documented or implied —
           internal pre-conditions belong inside DoX; externalizing them creates coupling and
           invites callers to skip the check
  [LOW] Query result used as a confirmed precondition ("we already checked, so it's guaranteed") —
        document that the result is best-effort/stale, or replace with a command that atomically
        checks and acts
```

**Activation heuristic:** Include this rubric when the diff contains any of: `Verify`, `Validate`, `Check`, `Ensure` as method/class/interface name prefixes; two-step patterns (`if (IsX) { DoX() }` or `if (await VerifyX()) { await DoX() }`); command handlers or message contracts returning `bool`; service methods that call a query and then a command on the same resource.

## Analyzer Suppression Audit

**Always include this section.** Every `#pragma warning disable`, `[SuppressMessage]`, or `.editorconfig`/`.globalconfig` severity downgrade in the diff must be challenged. Suppressions are technical debt with a justification label — the label may be wrong.

**How to audit:** For each suppression in the diff, extract the diagnostic ID and justification comment, then apply the challenge questions below. A justification passes only if it explains **why the analyzer's suggested fix is impossible or inappropriate** — not just what the warning means.

```
SUPPRESSION AUDIT RULES (include if diff contains #pragma warning disable, [SuppressMessage],
or editorconfig/globalconfig severity changes):

Missing or weak justification:
  [CRITICAL] Suppression with no justification comment — many analyzers make this a build
             error; must have // reason after the diagnostic ID
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
     → If a repo-owned analyzer (E128*, or another first-party rule): [HIGH] — these exist
        because the pattern caused real bugs here; suppressing needs strong evidence
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

## Cross-File Consistency Review

See [cross-file-consistency.md](cross-file-consistency.md) — separate file to keep this rubric under the 250-line target. Include when diff spans 3+ agent/skill/script/config files, or after renames, multi-session plans, or merge resolutions.
