# Tech Debt Audit: Duplicates

*Generated: 2026-11-26*

Focused run: `duplicates` mode over the full repo. Method: four parallel scans
with normalized sliding-window duplicate detection (8 and 15 line windows) over
`src/E128.Analyzers`, `src/E128.Analyzers.CodeFixers`, `src/E128.Analyzers.Union`,
and `tests/`, with every hit confirmed by reading the sites. Findings cite
`file:line`. Line numbers drift, so treat them as of 2026-11-26.

## Executive Summary

- No large hidden copy-pasted logic blocks remain in `src/`. The helper layer
  (TypeSymbols, MetadataTypes, LoopContext, FixSyntax, the test runners) already
  absorbs most duplication.
- What duplication remains is boilerplate repeated at scale: the analyzer
  `Initialize` prologue repeats in ~228 files (~1,400 lines) project-wide.
- Code fixers re-implement a diagnostic-node resolution preamble in ~86 files
  and a root plus semantic model guard in ~48 files. A typed base-class hook
  removes both.
- Several findings are sites that ignore an existing helper: a hand-rolled
  well-known-type registration (~25 sites vs `MetadataTypes.RegisterForType`),
  an invoked-name switch (~18 sites vs `InvocationNames`), and xUnit attribute
  detection that `XunitTestHelper` already owns.
- `tests/E128.Analyzers.Tests` has a real harness that a long tail of branch
  coverage files bypasses: ~73 files hand-roll what `AnalyzerTestRunner` and
  `CodeFixTestRunner` encapsulate, roughly 1,300 duplicated lines.
- The DI reference sets in tests pin two different versions of the same package
  (10.0.6 and 10.0.14). This is a correctness hazard, not just duplication.
- A whole-compilation duplicate-code detector analyzer stays a poor fit for the
  E128 rule model. The flagged shapes here are mostly conventional boilerplate,
  which would make the rule noisy. An offline scan script is the alternative.

## Findings Table

| ID | Category | File:Line | Severity | Effort | Description | Recommendation |
|---|---|---|---|---|---|---|
| F001 | Duplication | src/E128.Analyzers/Performance/ActivatorInLoopAnalyzer.cs:25 | HIGH | MEDIUM | `Initialize` guard plus configuration prologue repeats in ~228 analyzer files, ~1,400 lines | Add `AnalyzerSetup.Configure(AnalysisContext, GeneratedCodeAnalysisFlags)` at the src root and collapse each prologue |
| F002 | Duplication | src/E128.Analyzers.CodeFixers/Performance/HttpClientResponseHeadersReadCodeFixProvider.cs:26 | HIGH | MEDIUM | Diagnostic-node resolution preamble in `RegisterCodeFixesAsync` repeats in ~86 fixer files | Add a typed `RegisterNodeFixAsync<TNode>` hook on `E128CodeFixProviderBase` with an optional tie-break parameter |
| F003 | Duplication | src/E128.Analyzers/Reliability/TaskWhenAllCancellationPropagationAnalyzer.cs:69 | HIGH | MEDIUM | Well-known-type resolve, bail, register boilerplate hand-rolled in ~16 Performance and Reliability files plus 9 Unsafe files, while `MetadataTypes.RegisterForType` exists | Add a `TryRegisterForType` overload with multi-type and predicate support, then migrate the sites |
| F004 | Duplication | src/E128.Analyzers/Performance/PollingLoopNoTokenAnalyzer.cs:131 | HIGH | LOW | Invoked-method-name switch re-implemented at ~18 sites; `InvocationNames.GetSimpleName` has zero usages there | Add `InvocationNames.GetName(InvocationExpressionSyntax)` and replace all sites |
| F005 | Test debt | tests/E128.Analyzers.Tests/E128055PragmaBalanceCodeFixTests.cs:11 | HIGH | HIGH | ~43 files hand-roll `CSharpCodeFixTest` setups and ~30 files hand-roll `CSharpAnalyzerTest` setups that the runners encapsulate, roughly 1,300 lines | Extend `AnalyzerTestOptions` and `CodeFixTestOptions`, then migrate the files |
| F006 | Test debt | tests/E128.Analyzers.Tests/CaptiveDependencyAnalyzerTests.cs:11 | HIGH | LOW | DI reference sets re-declared in ~16 test files with version drift: 10.0.6 in six files, 10.0.14 in three | Add a shared `TestReferences` class with named sets and reconcile the version split deliberately |
| F007 | Duplication | src/E128.Analyzers/Unsafe/UnsafeCopyReferenceTypeAnalyzer.cs:47 | MEDIUM | LOW | "Resolve System.Runtime.CompilerServices.Unsafe then register" block repeats verbatim in 9 files | Add `MetadataTypes.RegisterForUnsafeInvocations` with an optional extra predicate |
| F008 | Duplication | src/E128.Analyzers.CodeFixers/Reliability/TaskFromResultSyncIoCodeFixProvider.cs:45 | MEDIUM | LOW | Root plus semantic model null-guard preamble repeats in ~48 files, 14 verbatim | Add `FixSyntax.GetRootAndSemanticModelAsync` returning a tuple |
| F009 | Duplication | src/E128.Analyzers/Reliability/DeadFieldStoreAnalyzer.cs:87 | MEDIUM | LOW | Anonymous-function or local-function ancestor walk appears in ~14 near-identical forms across Reliability and Performance | Add `SyntaxBoundary.IsInsideNestedFunction` and `StopsAtNestedFunction` with an optional extra-boundary predicate |
| F010 | Duplication | src/E128.Analyzers/Security/FipsSymbolHelpers.cs:53 | MEDIUM | LOW | Inline type name plus namespace equality checks in 8+ files re-implement `FipsSymbolHelpers.IsType`, which lives in the wrong namespace | Move it to `TypeSymbols.IsNamed` and make the top-level requirement optional |
| F011 | Duplication | src/E128.Analyzers/Security/UnvalidatedJwtIdentityAnalyzer.cs:80 | MEDIUM | LOW | Base-type walk loop with a per-rule predicate repeats at 6 sites | Add `TypeSymbols.WalkBaseTypes(ITypeSymbol?, Func<ITypeSymbol, bool>)` |
| F012 | Test debt | tests/E128.Analyzers.Tests/UnvalidatedJwtIdentityBranchCoverageTests.cs:9 | MEDIUM | LOW | The five UnvalidatedJwtIdentity package stubs copy-pasted at ~78 lines per copy across 3+ files, with intentional drift | Consolidate on the most complete shape inside the existing `TestStubs` class |
| F013 | Test debt | tests/E128.Analyzers.Tests/ComplexityCeilingBranchCoverageTests.cs:17 | MEDIUM | LOW | `VerifyWithCeilingAsync` and the `Warning(int,int)` helper are byte-identical across 6 ComplexityCeiling files | Add a shared `ComplexityCeilingTestHarness` static class |
| F014 | Test debt | tests/E128.Analyzers.Tests/EfQueryInLoopAnalyzerTests.cs:14 | MEDIUM | LOW | EF and DbConnection fake-source blocks duplicated per analyzer family, ~25 to 45 lines per copy | Move each family fake into `TestStubs` as two named stubs |
| F015 | Duplication | src/E128.Analyzers.CodeFixers/Design/SyncOverAsyncCodeFixProvider.cs:52 | MEDIUM | MEDIUM | "Insert await, promote containing method, add using" block repeats in 3 sites | Extend `AsyncSiblingCodeFixHelper` with `InsertAwaitAndPromoteAsync` taking the expression |
| F016 | Duplication | src/E128.Analyzers/Maintainability/TestMethodReflectionAnalyzer.cs:142 | MEDIUM | LOW | xUnit Fact and Theory detection hand-rolled in 2 files; `XunitTestHelper` owns the recognition and serves 10+ other sites | Call `XunitTestHelper.GetFactOrTheoryAttribute` and verify the qualified-name tests |
| F017 | Duplication | src/E128.Analyzers.CodeFixers/Performance/SearchValuesHoistingCodeFixProvider.cs:68 | MEDIUM | MEDIUM | Static-readonly-field hoisting re-implements `FixSyntax.InsertFieldAsFirstMember` with a different insertion order | Generalize the FixSyntax helper with a placement mode and share the field-name derivation |
| F018 | Duplication | src/E128.Analyzers/Performance/DbCommandInLoopAnalyzer.cs:49 | LOW | MEDIUM | 34-line `AnalyzeInvocation` body identical between the two expensive-call-in-loop analyzers | Extract `LoopContext.ReportCostInLoop`, migrate the two verbatim sites first |
| F019 | Duplication | src/E128.Analyzers/Performance/ManualSpanSumAnalyzer.cs:377 | LOW | LOW | "Identifier is written" switch in 6 sites and two variants | Add a `SyntaxWriteChecks.IsWriteTarget` helper with a strict-prefix bool parameter |
| F020 | Duplication | src/E128.Analyzers/Reliability/FileInfoToctouAnalyzer.cs:248 | LOW | LOW | `IsInsideTryCatch` pair byte-identical across 2 files | Add `ExceptionScope.IsInsideTryWithCatch` |
| F021 | Duplication | src/E128.Analyzers/Performance/ComparerSetConstructionAnalyzer.cs:115 | LOW | LOW | `GetTargetLocal` byte-identical across 2 files | Extract a `LocalTargets.FromCreation` helper |
| F022 | Duplication | src/E128.Analyzers.CodeFixers/Performance/ReadonlyFieldLoopHoistCodeFixProvider.cs:172 | LOW | LOW | `IndentationTrivia` helper byte-identical across 2 fixer files | Move to `FixSyntax.AnchorLeadingTrivia` |
| F023 | Duplication | src/E128.Analyzers/Performance/LoggerArgumentGuardAnalyzer.cs:224 | LOW | LOW | Parentheses unwrapping re-implemented in 2 files plus 4 parent-chain variants; `ExpressionUnwrapper.Parentheses` exists | Delete the re-implementations and adopt the existing helper |
| F024 | Duplication | src/E128.Analyzers.CodeFixers/Reliability/JsonDocumentLifetimeCodeFixProvider.cs:23 | LOW | LOW | Intentional no-op provider overrides repeat in 3 files against the base class defaults | Add an `E128NoOpCodeFixProvider` base or a `SupportsFixAll` flag |
| F025 | Duplication | src/E128.Analyzers/Reliability/AsyncFileIoAnalyzer.cs:64 | LOW | MEDIUM | "Sync call has an Async sibling" shape shared by the File and Db IO analyzers, ~28 lines each | Extract an `AsyncSiblingRules` helper for the clean File and Db pair only |
| F026 | Duplication | src/E128.Analyzers.CodeFixers/Style/NamingStyleCodeFixProvider.cs:270 | LOW | LOW | Word-joining `StringBuilder` loop skeleton repeats 5 times inside one file | Extract a private `JoinWords` with a per-word action |

## Top 5 "If You Fix Nothing Else, Fix These"

1. **F001, retracted on 2026-11-26.** The `AnalyzerSetup.Configure` helper was
   built and the build failed: RS1025 and RS1026, enabled as errors, require the
   configuration calls inline in each `Initialize` and do not see through a
   helper. The prologue is platform-enforced style. The lesson lives in
   [Analyzer Authoring](../lode/dotnet/analyzers/authoring.md).
2. **F002 plus F008, the code fixer preambles.** One typed hook on
   `E128CodeFixProviderBase` handles `Diagnostics[0]` plus `FindNode` plus the
   type test. One `FixSyntax` tuple helper handles root plus model. Together
   they shrink ~100 files by 8 to 20 lines each.
3. **F003 plus F004 plus F007, adopt the helpers that already exist.** These
   sites predate or ignore `MetadataTypes`, `InvocationNames`, and
   `ExpressionUnwrapper`. Extend `MetadataTypes` with a multi-type overload,
   then migrate mechanically. Lowest risk per line saved.
4. **F005 plus F006, migrate the test tail onto the runners.** Extend the two
   option records first, then migrate file by file. Reconcile the 10.0.6 vs
   10.0.14 DI version split as part of `TestReferences` and confirm no test
   depends on the older pin.
5. **F012 plus F014, promote the fakes into `TestStubs`.** The class exists for
   exactly this purpose. Keep the two distinct EfStub lineages as two named
   stubs and consolidate the JWT stubs on the most complete shape.

## Quick Wins

- F004 `InvocationNames.GetName` migration, pure rename-level change.
- F023 adopt `ExpressionUnwrapper.Parentheses`, deletes verbatim copies.
- F022 `FixSyntax.AnchorLeadingTrivia`, pure verbatim duplicate.
- F021 `GetTargetLocal` extraction, no caveat found.
- F020 `ExceptionScope.IsInsideTryWithCatch`, no caveat found.
- F006 `TestReferences` centralization, small diff, removes the version drift.
- F024 no-op fixer base class, structural only.

## Things That Look Bad but Are Actually Fine

- The `Initialize` prologue itself. It is conventional Roslyn analyzer style,
  and the flags argument varies. The win is line count, not correctness.
- The sensitive-word lists in `InsecureRandomInCryptoContextAnalyzer.cs:39` and
  `SensitiveLogArgumentAnalyzer.cs:26`. They overlap in vocabulary but encode
  different heuristics: substring match versus whole-word versus suffix
  exemptions. Unifying them would couple two rules.
- The four DI registration collectors in Reliability. Structurally similar but
  they differ materially in what they collect.
- The 173 one-line per-class `VerifyAsync` wrappers in the test project. They
  are a deliberate seam over `AnalyzerTestRunner`, not duplication.
- `FipsCipherModeAnalyzer.cs:82` versus `FipsTlsVersionAnalyzer.cs:166`. Same
  numeric-cast skeleton, but every predicate differs.
- The Pragma* Style family. It already shares `PragmaWarningDirectives.cs`
  correctly.

## Open Questions for the Maintainer

- F006: should the DI package pin move to 10.0.14, or does a test depend on the
  10.0.6 behavior on purpose?
- F001: drop the defensive `ArgumentNullException` in the prologue, or keep it
  inside the shared helper?
- F012: is the member drift between the three UnvalidatedJwtIdentity stub
  copies intentional per test, or an accident of copy-paste?
- F005: should the migration of ~73 test files be one plan or a rolling cleanup
  rule for new files only?
- The duplicate-code detector analyzer idea scored 2/5 in the candidate
  register. Confirm the offline `dup-scan` script shape instead, or close the
  candidate as skipped.
