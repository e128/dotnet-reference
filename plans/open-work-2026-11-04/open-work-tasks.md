# Open Work Tasks, 2026-11-04

*Updated: 2026-11-04T20:40:40Z*

Phases match [the plan](open-work-plan.md). R
phase gate before you tick the last box of a phase.

## Part 1: Review-Practice Analyzers

## Phase 1: Hardcoded Configuration

### E128164 Hardcoded URL

- [x] `src/E128.Analyzers/Security/HardcodedUrlAnalyzer.cs` Report an absolute http or https literal outside a test project. Severity: Warning.
- [x] `tests/E128.Analyzers.Tests/HardcodedUrlAnalyzerTests.cs` Positive, interpolated, attribute argument, namespace host, example host, and test project cases.
- [x] README row and `AnalyzerReleases.Unshipped.md` entry.

### E128165 Hardcoded secret or connection string

- [x] `src/E128.Analyzers/Security/HardcodedSecretAnalyzer.cs` Report a credential-named literal and a connection string with a password. Severity: Warning.
- [x] `tests/E128.Analyzers.Tests/HardcodedSecretAnalyzerTests.cs` Field, property, local, argument, connection string, short value, suffix name, and test project cases.
- [x] README row and `AnalyzerReleases.Unshipped.md` entry.

## Phase 2: Sensitive Data In Logs

- [x] E128166 `Security/SensitiveLogArgumentAnalyzer.cs`, tests, README row, release entry. Gate run pending.

## Phase 3: Untracked Deferral

- [x] E128167 `Style/UntrackedTodoAnalyzer.cs` and `e128_tracked_ref_pattern`.
- [x] E128168 `Testing/UntrackedSkipAnalyzer.cs`.
- [x] E128169 `Style/ObsoleteWithoutMessageAnalyzer.cs`.

## Phase 4: Unchecked Assumption

- [x] E128170 `Reliability/UnguardedSplitIndexAnalyzer.cs`.

## Phase 5: Test Hygiene And Design

- [x] E128171 `Testing/CommittedRedStubAnalyzer.cs`.
- [x] E128172 `Testing/SwallowedAssertionAnalyzer.cs`.
- [x] E128173 `Style/UnjustifiedSuppressionAttributeAnalyzer.cs`.
- [x] E128174 `Design/ConstructorDependencyCeilingAnalyzer.cs`.

## Script

- [x] `scripts/diff.sh --max-lines [N]` size warning.

## Part 2: Review Backlog

Carried from the closed review plans. Each item is Low severity or deferred.
Dropped as already fixed: the per-transform `jq` calls and the `Utf8StringParseAnalyzer` context names.

### From review-2026-11-01-general

- [x] `scripts/internal/cr-target.sh:225,344` Newline in a PR file name fuses entries. Fix: NUL-delimited fields. Severity: Low. Source: security-reviewer, adversarial-security.
- [x] `tests/E128.Analyzers.Tests/ArrayPoolSqliteParameterSizeCodeFixTests.cs:131` E128086 `Size = 0` TODO emission. Fix: derive used length or decline. Severity: Low. Source: code-reviewer.
- [x] `scripts/test.sh:89` exit-3 gate covers an explicitly empty trait run and `--all` naming `trait CI` despite no filter. Fix: gate the branch on a supplied filter. Severity: Low. Source: ponytail, dotnet-lens.
- [x] `scripts/internal/cr-target.sh` PR resolution paths untested. Fix: mock `gh` behind a command variable. Source: functionality-attacker.
- [x] `scripts/internal/cr-target.sh:120` the `critical` `only` word drop does not implement the adjacent-pair rule the comment states. Fix: match the pair or rewrite the comment. Severity: Low. Source: functionality-attacker.
- [x] `XmlDocCommentAnalyzer.cs` note the name and line as shipped today. Severity: Low. Source: code-reviewer.
- [x] `NonPublicBclReflectionAnalyzer.cs` assembly-name prefix match over-selects user assemblies. Fix: BCL identity set. Severity: Low. Source: code-reviewer.
- [x] `ManualBitCountCodeFixProvider.cs:60` replacement built by string concat. Fix: SyntaxFactory nodes. Severity: Low. Source: code-reviewer.
- [x] `ManualBitCountAnalyzer` skips unsigned counters by design. Severity: Low. Source: dotnet-lens.
- [x] `SequentialRenameFixAllProvider` drops stale spans silently. Severity: Low. Source: dotnet-lens.
- [x] The 250 duplicate implementations across each fixer file have no lode-tracking entry. Severity: Low. Source: ponytail.

### From review-2026-11-03-security

- [x] `AGENTS.md` Subagents section rides unrelated. Fix: split into its own commit or revert. Severity: Low. Source: code-reviewer, attacker-3. Moot: the section is already committed on main, with no diff on this branch.
- [x] `src/E128.Analyzers/Security/FipsSymbolHelpers.cs:ForEachAssignment` Compound assignment passes the compound op as the value. Fix: pass the right-hand side. Severity: Low. Source: code-reviewer.
- [x] `src/E128.Analyzers/Security/FipsTypeClassifier.cs:Classify` BCL fast path skips vendored cipher assemblies. Fix: skip only known BCL references, or scan every non-BCL assembly. Severity: Low. Source: attacker-1, security-reviewer.
- [x] `src/E128.Analyzers/Security/FipsSymbolHelpers.cs:NeverReassignedCache` Same-named locals can collide under SymbolEqualityComparer. Fix: drop the cache for locals or key on declaring syntax. Severity: Low. Source: attacker-1.
- [x] `src/E128.Analyzers/Security/FipsDirectInstantiationAnalyzer.cs` Name-matched cipher escapes the skip list and double-reports. Fix: consult FipsTypeClassifier.GetVerdict in the skip. Severity: Low. Source: code-reviewer.
- [x] `src/E128.Analyzers/Security/FipsCipherModeAnalyzer.cs` Message recommends CBC against authenticated policy. Fix: point at AES-GCM. Severity: Low. Source: code-reviewer.
- [x] `tests/E128.Analyzers.Tests/*` Untested documented behaviors on E128145, 146, 150, 151, 143, 149, 142. Fix: one test per README sentence. Severity: Low. Source: attacker-2.
- [x] `scripts/internal/analyzer-release-check.sh` Dedup stops at a hard-coded section header. Fix: filter the table lines and fail when both headers miss. Severity: Low. Source: security-reviewer.
- [x] `Directory.Packages.props` `Microsoft.CodeAnalysis.Workspaces.Common` pin unused directly. Fix: route to the `prune-deps` skill. Severity: Low. Source: dotnet lens. Done: pin removed, check.sh passes.
- [x] `src/E128.Analyzers/Security/FipsRsaKeySizeAnalyzer.cs` + `FipsPbkdfIterationAnalyzer.cs` Comments cite nonexistent FIPS0003/FIPS0005. Fix: name the real anchor. Severity: Low. Source: attacker-3.
- [x] `lode/dotnet/analyzer-candidates.md` Stale next-free-rule sentence. Fix: drop the contiguous-range clause. Severity: Low. Source: code-reviewer.
- [x] `src/E128.Analyzers.CodeFixers/Security/InsecureRandomInCryptoContextCodeFixProvider.cs` FindNode omits the tie-breaking idiom. Fix: pass `getInnermostNodeForTie: true`. Severity: Low. Source: attacker-3.
- [x] `src/E128.Analyzers/Security/FipsDeprecatedRngAnalyzer.cs` + `InsecureRandomInCryptoContextAnalyzer.cs` Three copies of the base-type walk. Fix: one `DerivesFromType` helper in FipsSymbolHelpers. Severity: Low. Source: attacker-3.
- [x] `src/E128.Analyzers/Security/FipsUnapprovedHashAnalyzer.cs` One closure lambda where siblings use the static-state overload. Fix: convert the call site. Severity: Low. Source: attacker-3.
