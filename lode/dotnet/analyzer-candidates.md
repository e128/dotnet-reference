# Analyzer Candidates
*Updated: 2026-04-15T12:09:01Z*
*Source: analyzer-review-miner 2026-04-15 — evidence window: 2026-04-12..2026-04-15*

## Candidate: mutable-static-readonly-array - 5/5

**Pattern**: Flag `private static readonly T[]` fields — arrays are reference types; the `readonly` modifier prevents reassigning the field but callers can still mutate content via indexer. Should use `ImmutableArray<T>`.

**Before**: `private static readonly string[] s_knownPrefixes = ["s_", "m_", "_", "I", "T"];`

**After**: `private static readonly ImmutableArray<string> s_knownPrefixes = ["s_", "m_", "_", "I", "T"];`

**Why not covered**: E128060 covers `Dictionary<K,V>` exposure → `IReadOnlyDictionary`. No existing E128 rule covers `static readonly T[]` → `ImmutableArray<T>`.

**Implementation sketch**: Register `SyntaxKind.FieldDeclaration`; check `static` + `readonly` modifiers; check declared type is `ArrayTypeSyntax`; flag with suggestion to use `ImmutableArray<T>`. Code fix replaces array type with `ImmutableArray<T>` and wraps initializer in `ImmutableArray.Create(...)` or collection expression.

**Evidence**: 6 occurrences across 5 files in `src/E128.Analyzers/` (NamingStyleCodeFixProvider.cs, MutableCollectionExposureAnalyzer.cs, PathNamePatterns.cs x2, CollectionPathAnalyzer.cs) in last 3 days. HIGH-flagged in code review report 2026-04-15.

**Source**: `analyzer-review-miner 2026-04-15`

---

## Candidate: test-reference-assemblies-tfm-mismatch - 4/5

**Pattern**: Flag `ReferenceAssemblies = ReferenceAssemblies.Net.Net80` (or any net<N> constant older than the project's `<TargetFramework>`) inside Roslyn analyzer test harness initializers. TFM drift causes tests to miss API availability issues specific to the production TFM.

**Before**: `ReferenceAssemblies = ReferenceAssemblies.Net.Net80,  // project targets net10.0`

**After**: `ReferenceAssemblies = ReferenceAssemblies.Net.Net100,`

**Why not covered**: No existing E128 or third-party analyzer checks TFM alignment in test harness object initializers.

**Implementation sketch**: Register `SyntaxKind.ObjectInitializerExpression`; find assignments to property named `ReferenceAssemblies`; check RHS member access for known outdated constants (`Net60`, `Net70`, `Net80`, `Net90`); cross-reference project's target framework via `context.Options.AnalyzerConfigOptionsProvider` or additional files. Flag when constant predates the project TFM. Expressibility is limited (TFM from MSBuild, not syntax) — may need AdditionalFiles injection or hardcode as configurable threshold.

**Evidence**: 114 occurrences in `tests/E128.Analyzers.Tests/` targeting `net10.0` project. HIGH-flagged in code review report 2026-04-15.

**Source**: `analyzer-review-miner 2026-04-15`

---

## Candidate: readonly-struct-property-no-init - 3/5

**Pattern**: Flag properties in `readonly struct` declarations that have only a `get` accessor and no `init`. Without `init`, the immutability contract is implicit rather than explicit at the language level, and adding `set` later is not caught.

**Before**:
```csharp
private readonly struct RenameInfo {
    public string OldName { get; }
    public RenameInfo(string oldName) { OldName = oldName; }
}
```

**After**:
```csharp
private readonly struct RenameInfo {
    public string OldName { get; init; }
}
```

**Why not covered**: No existing E128 rule enforces `init` on `readonly struct` properties. Roslynator has no exact equivalent.

**Implementation sketch**: Register `SyntaxKind.StructDeclaration`; check `readonly` modifier; for each `PropertyDeclaration` child with only `get`, flag unless constructor-initialized pattern is intentional.

**Evidence**: 1 occurrence in `NamingStyleCodeFixProvider.cs` `RenameInfo` struct (introduced 2026-04-14). MEDIUM-flagged in code review 2026-04-15.

**Source**: `analyzer-review-miner 2026-04-15`

---

## Catalog

| ID  | Name                                  | Status | Score | Last seen  |
| --- | ------------------------------------- | ------ | ----- | ---------- |
| —   | mutable-static-readonly-array         | new    | 5/5   | 2026-04-15 |
| —   | test-reference-assemblies-tfm-mismatch | new   | 4/5   | 2026-04-15 |
| —   | readonly-struct-property-no-init      | new    | 3/5   | 2026-04-15 |
