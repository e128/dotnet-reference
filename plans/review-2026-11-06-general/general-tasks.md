# Review Tasks: review-2026-11-06 general

*Updated: 2026-11-07T14:35:25Z*

## High

- [x] `src/E128.Analyzers/Reliability/HttpClientInfiniteTimeoutAnalyzer.cs:140` Unguarded `GetConstantValue(...).Value` throws on non-constant arguments, surfaced as AD0001. Fix: pattern match `is { HasValue: true, Value: var v }` before `IsInfiniteMilliseconds`. Severity: High. Source: code-reviewer, defender confirmed.
- [x] `src/E128.Analyzers/Security/FipsSymbolHelpers.cs` 1263 lines over the 800-line gate. Fix: split `FipsBufferLengthHelpers` and the assignment-resolution helpers into internal classes in the same assembly. Severity: High. Source: code-reviewer, defender confirmed.

## Medium

- [x] `src/E128.Analyzers/Performance/ComplexityCeilingAnalyzer.cs:133` Coordinate-step exemption skips for-index header variables. Fix: check `Locals` for `IForLoopOperation` too. Severity: Medium. Source: code-reviewer
- [x] `src/E128.Analyzers/Reliability/ConfigurationKeyMissingAnalyzer.cs:250` Filter matches any `.json`, not `appsettings*.json`. Fix: filter the file name against the documented contract. Severity: Medium. Source: code-reviewer
- [x] `src/E128.Analyzers/Reliability/ImmutableArrayDefaultAnalyzer.cs:295` Assignment scan counts member writes through the field. Fix: restrict to targets resolving to the field itself plus deconstruction elements. Severity: Medium. Source: code-reviewer
- [x] `src/E128.Analyzers/Reliability/RecordMutableCollectionEqualityAnalyzer.cs:136` Base-record `GetHashCode` not seen. Fix: walk `BaseType` before reporting. Severity: Medium. Source: code-reviewer
- [x] `src/E128.Analyzers/Security/TimingSafeComparisonAnalyzer.cs:442` Cross-tree declarator skipped, comment claims otherwise. Fix: `model.Compilation.GetSemanticModel(declarator.SyntaxTree)` or fix the comment. Severity: Medium. Source: code-reviewer
- [x] `src/E128.Analyzers/Security/RecursiveDeleteRootAnalyzer.cs:536` Readonly field or get-only property qualifies as a proven safe root. Fix: narrow to const fields or require a constant initializer. Severity: Medium. Source: code-reviewer, security-reviewer
- [x] `src/E128.Analyzers.CodeFixers/Security/TimingSafeComparisonCodeFixProvider.cs:477` `GetCompilationAsync` refetched per diagnostic. Fix: use the semantic model's compilation. Severity: Medium. Source: code-reviewer
- [x] `src/E128.Analyzers/Design/ConstructorDependencyCeilingAnalyzer.cs:484` Options re-parsed per constructor. Fix: read at compilation start, cache per tree. Severity: Medium. Source: code-reviewer
- [x] `src/E128.Analyzers/Design/OptionsValidationMissingAnalyzer.cs:130` Method over the 50-line gate. Fix: extract the chain walk or record the accepted length. Severity: Medium. Source: code-reviewer
- [x] `tests/E128.Analyzers.Tests/CodeFixTestRunner.cs:68` `NoFixAsync` zeroes the fix-all scopes, weakening ~20 no-fix tests versus the batch it replaced. Fix: delete the zero-iteration lines. Severity: Medium. Source: code-reviewer
- [x] `tests/E128.Analyzers.Tests/AwaitInFinallyAnalyzerTests.cs:17` plus ~17 sibling files Analyzer `VerifyAsync` boilerplate duplicated across new test files. Fix: add a shared analyzer runner beside `CodeFixTestRunner`. Severity: Medium. Source: code-reviewer
- [x] `scripts/internal/analyzer-release-check.sh:41` Guard rejects a Removed-Rules-only Unshipped.md that `analyzer-promote.sh` can produce. Fix: accept `^### Removed Rules`. Severity: Medium. Source: code-reviewer

## Low

- [x] `scripts/internal/cr-target.sh:497` PR number interpolated into `DIFF_COMMAND` without an integer check. Fix: `[[ "$PR_NUMBER" =~ ^[0-9]+$ ]] || exit 1`. Severity: Low. Source: security-reviewer
- [x] `scripts/dotnet-reference-go.sh:407` Keychain token exported to the child environment; parity with the deleted `.nu` design. Fix: none required, harden later via stdin or a helper. Severity: Low. Source: security-reviewer
- [x] `src/E128.Analyzers/Security/FipsSymbolHelpers.cs:983` No length cap before `Convert.FromBase64String`, `GetByteCount`, or the hex scan over source-derived constants. Fix: cap the constant before resolution; oversized literals fall through to the fail-closed report. Severity: Low. Source: security-reviewer
- [x] `tests/E128.Analyzers.Tests/CaptiveDependencyAnalyzerTests.cs:412` Test fixture package pins unaudited. Fix: run `dotnet list package --vulnerable` on the tests project once. Severity: Low. Source: security-reviewer
- [x] `src/E128.Analyzers/Style/TrackerReference.cs:45` Unbounded regex cache and fail-open on timeout. Fix: bound the cache and fall back to the default pattern or emit a diagnostic on timeout. Severity: Low. Source: security-reviewer, code-reviewer
- [x] `src/E128.Analyzers/Testing/XunitTestHelper.cs:709` Any compilation referencing a test framework loses secret/log detection. Fix: scope the exemption to a real test context. Severity: Low. Source: security-reviewer
- [x] `src/E128.Analyzers/Security/RecursiveDeleteRootAnalyzer.cs:412` `StartsWith` guard accepted without a separator bound, so `/tmp` matches `/tmp2`. Fix: require a directory prefix or provenance. Severity: Low. Source: security-reviewer
- [x] `src/E128.Analyzers.CodeFixers/Security/TimingSafeComparisonCodeFixProvider.cs:96` Replacement built by text concat + `ParseExpression`, no speculative binding check. Fix: node rewrite plus the sibling fixer's binding gate. Severity: Low. Source: security-reviewer
- [x] `src/E128.Analyzers.CodeFixers/Reliability/TaskCompletionSourceOptionsCodeFixProvider.cs:240` Same text-concat rewrite. Fix: compose the AST nodes or bind before registering. Severity: Low. Source: security-reviewer
- [x] `src/E128.Analyzers.CodeFixers/Style/SequentialRenameFixAllProvider.cs:109` Skip branch shipped with no test pin; remarks admit it. Fix: one fixture test that shifts a span. Severity: Low. Source: security-reviewer, code-reviewer
- [x] `.gitleaks.toml:8` Allowlist widened from `ThresholdOptionKey` to `[A-Za-z]*OptionKey`. Fix: keep or tighten; value pattern still locks lowercase. Severity: Low. Source: main-thread review
- [x] `scripts/branch.sh:52` Human output omits the behind count that the JSON carries. Fix: print it once in the human blocks. Severity: Low. Source: code-reviewer
- [x] `src/E128.Analyzers.CodeFixers/Performance/ComplexityCeilingSuppressionCodeFixProvider.cs:15` Pragmas embed the literal `"E128152"`. Fix: build from `ComplexityCeilingAnalyzer.DiagnosticId`. Severity: Low. Source: code-reviewer
- [x] `src/E128.Analyzers.CodeFixers/Design/*` about 20 stock `GetFixAllProvider` overrides survive `E128CodeFixProviderBase`. Fix: delete the stock overrides. Severity: Low. Source: code-reviewer
- [x] `scripts/lode-guard.sh:53` ok/warn boundary stays a bare `200` literal while the hard limit is `LODE_FILE_LIMIT`. Fix: derive the boundary from a named constant. Severity: Low. Source: code-reviewer
- [x] `scripts/dotnet-reference-go.sh:7` Default model changed in the wrapper migration without callout. Fix: note the swap or restore parity. Severity: Low. Source: code-reviewer
- [x] `scripts/internal/cr-target.sh:263` `self_test` over the 50-line rule. Fix: split into per-form test functions if it grows. Severity: Low. Source: code-reviewer
- [x] `src/E128.Analyzers.CodeFixers/.editorconfig` RCS9004 project suppression, approval noted in file. Severity: Low. Source: code-reviewer, record only
- [x] `src/E128.Analyzers/E128.Analyzers.csproj:32` Description still lists the deleted ConfigureAwait rule. Fix: drop it. Severity: Low. Source: code-reviewer
- [ ] `src/E128.Analyzers/AnalyzerReleases.Shipped.md:28` E128158 absent in the claimed E128154-174 range with no removal row. Fix: add a Unshipped Removed Rules row. Severity: Low. Source: code-reviewer. Not applied: E128158 was never assigned, so a Removed Rules row would be false. The gap is documented in `lode/dotnet/analyzer-candidates.md`.
- [x] `src/E128.Analyzers.CodeFixers/Testing/TempDirCleanupCodeFixProvider.cs:162` One fully-qualified `System.StringComparison.Ordinal`. Fix: unqualify with `using System;`. Severity: Low. Source: code-reviewer
- [x] `src/E128.Analyzers/Reliability/CaptiveDependencyAnalyzer.cs:543` Scope accessors matched by bare type name. Fix: resolve the three types and compare symbols, fallback to names. Severity: Low. Source: code-reviewer
- [x] `src/E128.Analyzers/Performance/ParallelForEachAsyncCapAnalyzer.cs:168` Options matched by spelled name, so a using alias skips the cap. Fix: bind `new` and compare types by symbol. Severity: Low. Source: code-reviewer
- [x] `src/E128.Analyzers/Maintainability/DirectRecursionAnalyzer.cs:292` Other-instance self-type calls report. Fix: receiver identity check or README note. Severity: Low. Source: code-reviewer
- [x] `src/E128.Analyzers/Reliability/AppSettingsKeyReader.cs:57` Literal-only document parses as well-formed with zero keys, arming the rule everywhere. Fix: require an object or array root. Severity: Low. Source: code-reviewer
- [x] `src/E128.Analyzers/Reliability/ConfigurationKeyMissingAnalyzer.cs:115` Compound assignment treated as a define-write and skips the read check. Fix: match `SimpleAssignmentExpression` kind. Severity: Low. Source: code-reviewer
- [x] `src/E128.Analyzers/Reliability/HttpClientInfiniteTimeoutAnalyzer.cs:166` Comment calls an exact match a tolerance. Fix: correct the comment or use `==`. Severity: Low. Source: code-reviewer
- [x] `src/E128.Analyzers/Reliability/SubstringLengthUnderflowAnalyzer.cs:659` Local named `days` in a substring-count rule. Fix: rename to `subtracted`. Severity: Low. Source: code-reviewer
- [x] `src/E128.Analyzers/Reliability/TaskCompletionSourceOptionsAnalyzer.cs:240` Options type matched by bare name. Fix: namespace check through `FipsSymbolHelpers.IsType`. Severity: Low. Source: code-reviewer
- [x] `src/E128.Analyzers/_._` Stray zero-byte file. Fix: delete. Severity: Low. Source: code-reviewer. Reverted: the file is the required `lib/netstandard2.0` NuGet placeholder and is referenced by the csproj. Deleting it broke `dotnet pack`.
- [x] `src/E128.Analyzers/Security/SensitiveLogArgumentAnalyzer.cs:335` and `TimingSafeComparisonAnalyzer.cs:410` SplitWords duplicated. Fix: one shared implementation with separators as input. Severity: Low. Source: code-reviewer
- [x] `tests/E128.Analyzers.Tests/CaptiveDependencyAnalyzerTests.cs:11` Same DI package pinned 10.0.6 and 10.0.14 in one file. Fix: one version across files. Severity: Low. Source: code-reviewer
- [x] `tests/E128.Analyzers.Tests/ComplexityCeilingE128AnalyzerTests.cs:297` vs `ConditionalDirectiveCeilingAnalyzerTests.cs:703` Same option mechanism attached via `.editorconfig` and `.globalconfig`. Fix: one spelling. Severity: Low. Source: code-reviewer
- [x] `tests/E128.Analyzers.Tests/EventSubscriptionLeakAnalyzerTests.cs:495` `-= null` test reads as a real removal. Fix: rename or comment the leniency. Severity: Low. Source: code-reviewer
- [x] `tests/E128.Analyzers.Tests/HardcodedSecretAnalyzerTests.cs:387` `EmitXunitStub` duplicates `EmitStub`. Fix: delete and parameterize. Severity: Low. Source: code-reviewer
- [x] `tests/E128.Analyzers.Tests/ImmutableArrayDefaultCodeFixTests.cs:481` and `ManualSpanSumCodeFixTests.cs` Two files missed the CodeFixTestRunner migration; one block needs `CodeFixTestBehaviors` support. Severity: Low. Source: code-reviewer
- [x] `tests/E128.Analyzers.Tests/ParallelForEachAsyncCapAnalyzerTests.cs:62` Explicit `-1` silent while default `new ParallelOptions()` fires, names mislead. Fix: agree the verdicts or comment the intent. Severity: Low. Source: code-reviewer
- [x] `tests/E128.Analyzers.Tests/FipsGeneratedCodeTests.cs:334` Both-generated combination missing from the matrix. Fix: add the row. Severity: Low. Source: code-reviewer
- [x] `tests/E128.Analyzers.Tests/SortInLoopE128AnalyzerTests.cs` Test name says foreach, body is while. Fix: rename to `RemoveAtZero_InWhile_Fires_E128153`. Severity: Low. Source: code-reviewer
- [x] `tests/E128.Analyzers.Tests/TaskCompletionSourceOptionsCodeFixTests.cs` Fix expectation carries redundant cast parens. Fix: emit without parens or comment the wart. Severity: Low. Source: code-reviewer
