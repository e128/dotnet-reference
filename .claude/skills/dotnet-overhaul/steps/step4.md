# Step 4: Cross-Cutting Design Review

Launch 5 `Explore` agents in parallel:

**Agent 1 — Error handling:**
```
Empty catch blocks, catch-and-swallow, catch(Exception) without rethrow.
Missing ArgumentNullException.ThrowIfNull guards on public methods.
Null returns where TryGet/Result would be clearer. Bare `throw ex` (stack trace loss).
```

**Agent 2 — Logging:**
```
String interpolation in logger calls (should use LoggerMessage source generator).
Swallowed exceptions without logging. Debug.WriteLine/Console.WriteLine where ILogger belongs.
Missing log level guards around expensive argument computation.
```

**Agent 3 — DI & lifetime:**
```
`new` instantiation of types that should be injected.
Service locator pattern (IServiceProvider.GetService outside composition root).
Singleton with mutable state without thread safety.
Static helpers that take dependencies as parameters.
```

**Agent 4 — Organization:**
```
Duplicate logic across files. God classes (files > 300 lines with mixed responsibilities).
Methods with > 5 parameters. Magic strings/numbers that should be constants or config.
```

**Agent 5 — SOLID design:**
```
SRP: Classes with multiple unrelated public method groups (auth + persistence, orchestration + data access).
OCP: Switch/if-chains on type discriminators that require modification to add new variants.
LSP: Overrides that throw NotSupportedException/NotImplementedException or narrow base contracts.
ISP: Interfaces with >7 methods or implementers with no-op methods.
DIP: `new` instantiation of service types, high-level classes depending on concrete low-level classes.
Balance with YAGNI — don't flag single-implementation interfaces or simple utility classes.
See `${CLAUDE_SKILL_DIR}/conventions.md` or `lode/coding-standards/` (if either exists) for project SOLID reference.
```

**Findings ID prefix: `CC`**
Severity: HIGH = swallowed exceptions / DI violations, MEDIUM = missing guards / logging,
LOW = magic values / duplication, INFO = acknowledged intentional patterns.

Present table. Wait for approval. Then Fix Cycle.
