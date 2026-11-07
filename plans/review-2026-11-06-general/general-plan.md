# Mr. Sparkle Review Plan: days 5 (code-only scope)

*Updated: 2026-11-06T13:18:06Z*

Union review of dotnet-reference commits newer than 5 days (26 commits, base
8296a114ac7ea857887c1ae614f20cb8e5e1e9c7). Target resolved by
`scripts/internal/cr-target.sh --json`. Scope agreed with the owner: security
reviewer, code reviewer, and the .NET lens over `src/`, `tests/`, and
`scripts/`; `.claude/`, `lode/`, and doc markdown excluded. Review only; this
plan records findings. Apply fixes through `/review-apply`.

Verdict: Request changes (one or more confirmed High finding).

## Findings Table

| # | File:Line | Sev | Problem | Fix | Source |
|---|-----------|-----|---------|-----|--------|
| 1 | src/E128.Analyzers/Reliability/HttpClientInfiniteTimeoutAnalyzer.cs:140 | High | `GetConstantValue(...).Value` unguarded; non-constant argument throws and Roslyn surfaces AD0001 at every such call site. Defender confirmed at line 140. | Pattern match: `model.GetConstantValue(argument, ct) is { HasValue: true, Value: var v } && IsInfiniteMilliseconds(v)` | code-reviewer |
| 2 | src/E128.Analyzers/Security/FipsSymbolHelpers.cs | High | 1263 lines, over the 800-line gate. Defender confirmed separable clusters (buffer-length evaluation ~749-1022, assignment resolution ~492-590 and ~1079-1263). | Split `FipsBufferLengthHelpers` and the assignment-resolution helpers into internal classes in the same assembly. | code-reviewer |
| 3 | src/E128.Analyzers/Performance/ComplexityCeilingAnalyzer.cs:133 | Med | Coordinate-step exemption gate runs only when the enclosing loop is a foreach; for-index header variables fall through and infer quadratic on the documented cell-in-row shape. | Test `enclosing.Locals.Contains(symbol)` for both `IForEachLoopOperation` and `IForLoopOperation`. | code-reviewer |
| 4 | src/E128.Analyzers/Reliability/ConfigurationKeyMissingAnalyzer.cs:250 | Med | AdditionalFiles filter matches every `.json`, not the documented `appsettings*.json` contract; foreign tooling files hide real missing keys. | Filter the file name against `appsettings*.json` before parse. | code-reviewer |
| 5 | src/E128.Analyzers/Reliability/ImmutableArrayDefaultAnalyzer.cs:295 | Med | Assignment scan counts any `IFieldSymbol` descendant on the assignment LHS, so member writes suppress the never-assigned report. | Restrict to targets that resolve to the field itself plus deconstruction elements. | code-reviewer |
| 6 | src/E128.Analyzers/Reliability/RecordMutableCollectionEqualityAnalyzer.cs:136 | Med | `DeclaresGetHashCode` scans only `record.Members`; a base record's override still reports the derived member. | Walk `BaseType` and check for a parameterless `GetHashCode` before reporting. | code-reviewer |
| 7 | src/E128.Analyzers/Security/TimingSafeComparisonAnalyzer.cs:442 | Med | Cross-tree field initializer skipped, contradicting the comment that claims to handle it; silent false negative for E128157. | Use `model.Compilation.GetSemanticModel(declarator.SyntaxTree)`, or fix the comment and document the false negative. | code-reviewer |
| 8 | src/E128.Analyzers/Security/RecursiveDeleteRootAnalyzer.cs:536 | Med | Any readonly field or get-only property counts as a proven safe delete root; config-sourced values pass the guard. Security reviewer corroborated. | Narrow to const fields or require a constant initializer. | code-reviewer, security-reviewer |
| 9 | src/E128.Analyzers.CodeFixers/Security/TimingSafeComparisonCodeFixProvider.cs:477 | Med | `GetCompilationAsync` refetched per diagnostic; the semantic model already carries the compilation. | Take the compilation from the semantic model, as `PathCombineSegmentCodeFixProvider` does. | code-reviewer |
| 10 | src/E128.Analyzers/Design/ConstructorDependencyCeilingAnalyzer.cs:484 | Med | Analyzer options re-parsed per constructor declaration. | Read once at compilation start and cache per tree. | code-reviewer |
| 11 | src/E128.Analyzers/Design/OptionsValidationMissingAnalyzer.cs:130 | Med | `AnalyzeInvocation` runs about 60 lines against the 50-line gate. | Extract the fluent-chain walk into one local helper, or record the accepted length. | code-reviewer |
| 12 | tests/E128.Analyzers.Tests/CodeFixTestRunner.cs:68 | Med | `NoFixAsync` zeroes three fix-all iteration scopes, so ~20 no-fix tests never run fix-all; the replaced per-file tests verified at least one scope. Fixer silentness only guarded by the per-diagnostic comparison. Verified roslyn-sdk semantics: null = auto, 0 = skip. | Delete the zero-iteration lines so scopes run auto. | code-reviewer |
| 13 | tests/E128.Analyzers.Tests/AwaitInFinallyAnalyzerTests.cs:17 and ~17 siblings | Med | Near-identical analyzer `VerifyAsync` helper duplicated in ~18 new test files (two reviewers found it: counts of 14 and 18). | Add a shared analyzer runner beside `CodeFixTestRunner`. | code-reviewer |
| 14 | scripts/internal/analyzer-release-check.sh:41 | Med | The new guard rejects an Unshipped.md that carries only Removed Rules rows; `analyzer-promote.sh` can leave exactly that state, so the ship flow self-blocks. | Accept a `^### Removed Rules` header. | code-reviewer |

## Low Findings (one checkbox per finding in tasks.md)

Security side: cr-target.sh PR-number validation before `DIFF_COMMAND`
interpolation; `ANTHROPIC_AUTH_TOKEN` export parity note; FipsSymbolHelpers
unbounded decode of source-derived constants (no length cap); test fixture
package pins (`PackageIdentity`) unaudited; TrackerReference unbounded regex
cache plus fail-open on timeout; RecursiveDeleteRoot `StartsWith` guard
without a separator bound; TimingSafeComparison and TaskCompletionSourceOptions
code fixers build replacements by text concat + `ParseExpression` with no
speculative binding check; SequentialRenameFixAllProvider skip branch has no
test pin (two reviewers); `XunitTestHelper.IsTestCompilation` blanket
exemption disables the secret/log rules for any compilation referencing a test
framework; gitleaks allowlist widened to `[A-Za-z]*OptionKey`.

Code side: branch.sh behind count JSON-only; "E128152" literal in
ComplexityCeilingSuppressionCodeFixProvider instead of the analyzer constant;
~20 stock `GetFixAllProvider` overrides survive the base-class split;
lode-guard ok/warn boundary not derived from `LODE_FILE_LIMIT`;
dotnet-reference-go.sh default model changed silently in the wrapper
migration; cr-target.sh `self_test` over the 50-line rule; RCS9004 suppression
in the CodeFixers project (approval noted in file); package Description still
lists the deleted ConfigureAwait rule; E128158 absent from release notes with
no removal row; one fully-qualified `System.StringComparison.Ordinal` in
TempDirCleanupCodeFixProvider; ScopeAccessorNames loose name matching;
ParallelOptions matched by spelled name so an alias skips the cap check;
DirectRecursion flags other-instance self-type calls; AppSettingsKeyReader
accepts a literal-only document; compound assignment on a config key skips the
read check; `double.Epsilon` comment describes an exact match as a tolerance;
`days` local name in SubstringLengthUnderflowAnalyzer; TaskCreationOptions
matched by bare type name; stray empty `src/E128.Analyzers/_._`; SplitWords
duplicated across two analyzer files; DI test pins split 10.0.6 vs 10.0.14;
`.editorconfig` vs `.globalconfig` for the same option mechanism in tests;
`-= null` test reads as a real removal; duplicated `EmitXunitStub` helper; two
test files missed the CodeFixTestRunner migration; E128187 explicit `-1`
versus default verdicts unexplained; FipsGeneratedCodeTests missing the
both-generated matrix row; `RemoveAtZero_InForeach_Fires_E128153` name vs
while-loop body; fix expectation carries redundant `TaskCreationOptions` cast
parens.

## Phases

1. Phase A: fix the two High findings (analyzer crash, helper split).
2. Phase B: the eleven Medium findings across analyzers, fixers, tests, scripts.
3. Phase C: Low sweep, batched by file area.
4. Phase D: re-run `scripts/diagnostics.sh --group`, `scripts/violation-scan.sh`,
   `bash scripts/test.sh`, and the release check before ship.

## Notes

- All scopes counted: 63 delivered diff slices (src, tests, scripts), 9
  reviewer batches per reviewer averaged 68 files each; baseline fight
  reviewed by hand in the main thread (publish.yml, .gitignore, gitleaks,
  packages, slnx, global.json, Release notes, README).
- Build ground truth: `scripts/diagnostics.sh --group` reports zero
  diagnostics; `analyzer-stats.sh` reports 197 ids, 190 analyzer classes.
- Excluded per owner's scope choice: `.claude/`, `lode/`, markdown docs.
