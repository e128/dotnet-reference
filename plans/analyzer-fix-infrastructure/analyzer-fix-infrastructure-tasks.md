# Analyzer Fix Infrastructure Tasks

*Updated: 2026-10-31T22:20:00Z*

## Phase 1: Repair the unsafe fixes

- [x] `src/E128.Analyzers/Design/SealedByDefaultAnalyzer.cs` A class named by a generic constraint is reported, and the fix emits CS0701. Fix: collect constraint types from type parameters, methods, and local functions, and skip them. Severity: High
- [x] `src/E128.Analyzers/Design/AsyncDisposableWithoutDisposableCodeFixProvider.cs` The fix inserts a second `Dispose()`. Fix: add the interface always, and the stub only when the type declares no parameterless `Dispose`. Severity: High
- [x] `src/E128.Analyzers/Testing/TempDirCleanupCodeFixProvider.cs` The fix inserts `InitializeAsync` and `DisposeAsync` unconditionally. Fix: skip each member that exists. Severity: High

## Phase 2: Apply the formatter instead of hand-built trivia

- [x] `AsyncDisposableWithoutDisposableCodeFixProvider.cs` and `TempDirCleanupCodeFixProvider.cs` Fix: attach `Formatter.Annotation` and drop the manual line feeds. Severity: Medium

## Phase 3: Migrate the remaining fix providers to DocumentEditor

- [ ] `src/E128.Analyzers/**/*CodeFixProvider.cs` Fix: convert a provider that touches more than one node, starting with E128017. Confirm the schema and the FixAll tests stay green. Severity: Medium
- [ ] `src/E128.Analyzers/UsingDirectives.cs` Fix: add a namespace-scoped using path for the first fix that needs it. Severity: Low
- [ ] `src/E128.Analyzers/**/*CodeFixProvider.cs` Fix: replace hand-built type names with `SyntaxGenerator.TypeExpression(symbol, addImport: true)`, starting with the providers that add a using by hand. Severity: Medium
- [ ] `src/E128.Analyzers/InModifierHelper.cs` Fix: add a modifier-order helper and replace the three providers that compute an insert index. Severity: Low
- [ ] `src/E128.Analyzers/**/*CodeFixProvider.cs` Fix: decline a fix through a speculative bind where the fix adds an argument or swaps an overload. Severity: Medium

## Phase 4: Split the package assembly

- [ ] `src/E128.Analyzers/E128.Analyzers.csproj` Fix: move the fix providers into `src/E128.Analyzers.CodeFixers`, link the shared tables, and remove the RS1038 suppression. Severity: High
- [ ] `src/E128.Analyzers/E128.Analyzers.csproj` Fix: evaluate a `net10.0;netstandard2.0` multi-target and a package path per target. Severity: Medium
- [ ] `Directory.Build.targets` Fix: add `Microsoft.CodeAnalysis.BannedApiAnalyzers` and a banned-symbol file for any direct diagnostic report. Run the dependency review first. Severity: Medium

## Phase 5: Harden the build metadata

- [ ] `global.json` Fix: change `rollForward` from `latestMajor` to `latestPatch`, so an SDK major bump cannot change analyzer output silently. Severity: Low
- [ ] `tests/` Fix: add a test run configuration that caps parallel compilation, then measure peak memory. Severity: Low

## Phase 6: Verify

- [ ] Run `scripts/check.sh`.
- [ ] Re-read the three lode files this plan links and correct any claim that a phase changes.
