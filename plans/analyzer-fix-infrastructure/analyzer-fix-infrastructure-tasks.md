# Analyzer Fix Infrastructure Tasks

*Updated: 2026-11-01T13:49:33Z*

## Phase 1: Repair the unsafe fixes

- [x] `src/E128.Analyzers/Design/SealedByDefaultAnalyzer.cs` A class named by a generic constraint is reported, and the fix emits CS0701. Fix: collect constraint types from type parameters, methods, and local functions, and skip them. Severity: High
- [x] `src/E128.Analyzers/Design/AsyncDisposableWithoutDisposableCodeFixProvider.cs` The fix inserts a second `Dispose()`. Fix: add the interface always, and the stub only when the type declares no parameterless `Dispose`. Severity: High
- [x] `src/E128.Analyzers/Testing/TempDirCleanupCodeFixProvider.cs` The fix inserts `InitializeAsync` and `DisposeAsync` unconditionally. Fix: skip each member that exists. Severity: High

## Phase 2: Apply the formatter instead of hand-built trivia

- [x] `AsyncDisposableWithoutDisposableCodeFixProvider.cs` and `TempDirCleanupCodeFixProvider.cs` Fix: attach `Formatter.Annotation` and drop the manual line feeds. Severity: Medium

## Phase 3: Migrate the remaining fix providers to DocumentEditor

- [x] `src/E128.Analyzers/Design/PrimaryConstructorBackingFieldCodeFixProvider.cs` (E128017) Fix: convert to `DocumentEditor`, so the field removal and the reference rewrites share one tracked tree. Severity: Medium
- [x] `UsingDirectives.cs` Fix: no fix writes a namespace-scoped using, so the top-level path serves every provider. Severity: Low
- [x] `HttpClientResponseHeadersReadCodeFixProvider.cs` Fix: `SyntaxGenerator.TypeExpression(symbol, addImport: true)` names the option type, and the hand-written `AddUsings` block is gone. Severity: Medium
- [x] `InModifierHelper.cs` Fix: `AddModifier` places a token in language-reference order. `SealedByDefault` and `ByteSizeForDataSize` use it. No third provider computes a modifier index. Severity: Low
- [x] `HttpClientResponseHeadersReadCodeFixProvider.cs` Fix: `BindsAfterImport` proves the added argument binds in a copy of the compilation that holds the import, and the fix declines otherwise. Severity: Medium

## Phase 4: Split the package assembly

- [x] `src/E128.Analyzers.CodeFixers/E128.Analyzers.CodeFixers.csproj` Fix: the fix providers, their helpers, and `UsingDirectives` live in the new project. The Workspaces reference and the RS1038 suppression are gone from `E128.Analyzers`. Severity: High
- [x] Multi-target `net10.0;netstandard2.0` Fix: deferred. One assembly name cannot occupy two folders under `analyzers/dotnet/cs`, and the versioned layout needs three projects per Roslyn version. Severity: Medium
- [x] `Directory.Build.targets` Fix: deferred. No shared diagnostic helper exists, so a banned-symbol file has no entry to guard. RS1038 covers the Workspaces boundary. Severity: Medium

## Phase 5: Harden the build metadata

- [x] `global.json` Fix: `rollForward` is `latestPatch`, so a feature-band SDK bump needs a deliberate edit. Severity: Low
- [x] `tests/E128.Analyzers.Tests/xunit.runner.json` Fix: `maxParallelThreads` is 4. Peak RSS fell from 402 MiB to 375 MiB on a full CI run. Severity: Low

## Phase 6: Verify

- [x] Run `scripts/check.sh`.
- [x] Re-read the three lode files this plan links and correct any claim that a phase changes.
