# Review Context: review-2026-11-06 general

*Updated: 2026-11-06T13:18:02Z*

## Target and Vectors

- Target: `mr-sparkle "days 5"` on dotnet-reference, base 8296a114ac7ea857887c1ae614f20cb8e5e1e9c7, 26 commits.
- Owner-scaled scope: security reviewer + code reviewer + .NET lens over src, tests, scripts. No adversarial, no ponytail, no code-review skill, no markdown config files.
- Diff: 2.26 MB code diff delivered as 63 slices (cr-diff-deliver split strategy, 8 batches per reviewer).
- Vectors run: diagnostics.sh --group (clean, zero build diagnostics), violation-scan.sh (3 hits, all detection-message literals in analyzer sources, false positives), suppression-scan.sh (224, one src, justified: S5443 in HardcodedTmpPathE128Analyzer on detection literals), analyzer-stats.sh (197 ids, 190 classes, 472 public API entries, total 55k LOC), deps-graph orphans (all classified: Workspaces.Common confirmed transitive, rest transitive pins or build assets), nuget-heat-map (informational).
- Roslyn navigator MCP skipped: the server answers but holds the pugworks workspace, not this solution.
- Mechanical-diff step: 0 excluded, working tree clean so the script saw an empty working diff.

## Reviewer Coverage

16 reviewer instances: security-reviewer over slices 1-8, 9-16, 17-24, 25-32, 33-40, 41-48, 49-56, 57-63; code-reviewer over the same eight batches. Each read the vendored reference file and the slice diffs, full source only where a finding depended on it. Every batch returned a coverage line; none reported a failed tool call. No slice was left unread.

## Dedup Report

Folded duplicates (same file:line or same defect):

- RecursiveDeleteRootAnalyzer readonly-field safe root: security 33-40 (LOW) + code 33-40 (MEDIUM) -> kept MEDIUM (finding 8).
- TrackerReference silent fallback: security 33-40 + code 33-40 -> one Low.
- SequentialRename skip branch untested: security 9-16 + code 9-16 -> one Low.
- Analyzer VerifyAsync duplication: code 41-48 (~14 files) + code 57-63 (~18 files) -> one MEDIUM (finding 13).
- Location correction by the defender: the unguarded GetConstantValue call sits at line 140, not 146.

## Defender Pass

- `HttpClientInfiniteTimeoutAnalyzer.cs:140`: CONFIRMED, no HasValue check, no outer catch. Severity kept High.
- `FipsSymbolHelpers.cs`: CONFIRMED 1263 lines by wc, separable clusters named. Severity kept High.

## Raw Findings by Reviewer (condensed)

### security-reviewer 1-8
Low: cr-target.sh:497 PR number into DIFF_COMMAND unvalidated; dotnet-reference-go.sh:407 token export (parity, no fix required). Scripts praised: %q escaping, newline-safe filtering.

### security-reviewer 9-16
Low: TimingSafeComparisonCodeFixProvider text-concat ParseExpression, no binding gate; TaskCompletionSourceOptionsCodeFixProvider WithFlag same pattern; SequentialRenameFixAllProvider skip branch untested.

### security-reviewer 17-24
No findings. All analyzer source and PublicAPI lines; JSON reader bounds nesting at 64 and never evaluates values.

### security-reviewer 25-32
Low: FipsSymbolHelpers decode and scans over source-derived constants with no cap; hostile literal inflates analyzer memory and time only in untrusted-compilation pipelines.

### security-reviewer 33-40
Low: TrackerReference unbounded cache, fail-open on timeout; XunitTestHelper.IsTestCompilation blanket exemption; RecursiveDeleteRoot readonly-field safe root; RecursiveDeleteRoot StartsWith without separator bound.

### security-reviewer 41-48
Low: test reference-assemblies package pins unaudited; test-scoped.

### security-reviewer 49-56, 57-63
No findings. Test fixtures only.

### code-reviewer 1-8
MEDIUM: analyzer-release-check.sh removed-rules-only blind spot. Lows: branch.sh behind JSON-only; "E128152" literal in the suppression fixer; ~20 surviving stock GetFixAllProvider overrides; lode-guard 200 literal; go.sh model swap silent; cr-target self_test 72 lines; RCS9004 suppression on record. Spec fit confirmed for the fixer split and bash migration.

### code-reviewer 9-16
MEDIUM: GetCompilationAsync per diagnostic; options re-parse per constructor; 60-line method. Lows: stale package Description; E128158 gap; StringComparison qualif; unpinned skip path. Spec fit: base class, InternalsVisibleTo, pack target confirmed via Directory.Build.props.

### code-reviewer 17-24
MEDIUM: ComplexityCeiling foreach-only gate; ConfigurationKeyMissing `.json` filter. Lows: scope accessors by name; ParallelOptions alias; DirectRecursion other-instance; AppSettingsKeyReader literal root; compound assignment read. Strengths: E128152 classifier correct on README cases, fail-closed gates match docs, AlternateLookup fix lands, JSON reader solid.

### code-reviewer 25-32
HIGH (both confirmed): unguarded GetConstantValue; 1263-line helper file. MEDIUM: ImmutableArrayDefault LHS scan; RecordMutableCollectionEquality base chain. Lows: double.Epsilon comment; `days` local; TaskCreationOptions bare name.

### code-reviewer 33-40
MEDIUM: cross-tree declarator skip; readonly safe root (dedup). Lows: `_._` empty file; SplitWords dup; TrackerReference fallback (dedup). Verified PfxImport enum-constant tests and FipsSymbolHelpers IsComparison coverage.

### code-reviewer 41-48
MEDIUM: ~14 duplicated VerifyAsync helpers. Lows: DI pins 10.0.6 vs 10.0.14; editorconfig vs globalconfig; `-= null` test. Spec fit: csproj project reference, runner parity, E128022 removal corroboration, ConfigureAwaitFalse source gone.

### code-reviewer 49-56
Lows: EmitXunitStub dup; two files missed migration; E128187 explicit -1 vs default; FIPS generated-code matrix row missing. Fix replacement accuracy verified against System.Random semantics.

### code-reviewer 57-63
MEDIUM: CodeFixTestRunner NoFixAsync zero-iterations disables fix-all scopes (confirmed against roslyn-sdk semantics, null auto vs 0 skip). Lows: 18-file VerifyAsync dup (dedup); foreach-named while test; redundant parens expectation.

## Verdict and Exit

- Verdict: Request changes (2 confirmed High).
- Exit code: 1.
- Full plan and checklist: general-plan.md, general-tasks.md in this directory.
