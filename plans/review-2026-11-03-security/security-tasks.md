# Review Tasks: FIPS Analyzer Gaps, 2026-11-03

*Updated: 2026-11-03T13:17:47Z*

## Phase 1

- [x] `src/E128.Analyzers/Security/FipsRsaKeySizeAnalyzer.cs:CheckCurveValue` E128143 stays silent for an unresolvable curve name. Fix: else-branch report "curve cannot be verified at compile time", plus README sentence and one test per unresolved form. Severity: High. Source: mr-sparkle merge (defender CONFIRMED).

## Phase 2

- [x] `.globalconfig:44` E128075 downgraded to none while the change raises it to Error upstream. Fix: the pre-existing third-party-redundancy policy stands; `fips-rules.md` and `redundant-rules.md` now record the exception. Severity: Medium. Source: code-reviewer, attacker-2.
- [x] `src/E128.Analyzers/Security/FipsUnapprovedHashAnalyzer.cs:IsExemptFromHashNameCheck` Name-prefix exemption covers every System/Microsoft type. Fix: restricted to AsymmetricAlgorithm-derived verification members resolved by symbol. Severity: Medium. Source: attacker-1, security-reviewer.
- [x] `src/E128.Analyzers/Security/FipsDirectInstantiationAnalyzer.cs:AnalyzeObjectCreation` Report gated on suggestion availability; string-parameter skip too broad. Fix: generic base-factory message on failover, `CngKey`/`CngProvider` only skip. Severity: Medium. Source: security-reviewer.
- [x] `src/E128.Analyzers/Security/FipsSymbolHelpers.cs:IsComparison` Instance `Equals` receiver reports as a selection. Fix: the invocation instance now counts as a read. Severity: Medium. Source: attacker-2.
- [x] `src/E128.Analyzers.CodeFixers/Security/InsecureRandomInCryptoContextCodeFixProvider.cs` Fixer bypasses `UsingDirectives.AddIfMissing`. Fix: unqualified name plus the shared helper, ImportAdder and Simplifier deleted (user chose the unqualified form). Severity: Medium. Source: attacker-3.
- [x] `src/E128.Analyzers/Security/FipsSymbolHelpers.cs` Non-generic ReportHashNameArguments and ReportHashNameStores have no caller. Fix: deleted both. Severity: Medium. Source: attacker-3.
- [ ] `AGENTS.md` Subagents section rides unrelated. Fix: split into its own commit or revert. Severity: Low. Source: code-reviewer, attacker-3.

## Phase 3

- [ ] `src/E128.Analyzers/Security/FipsSymbolHelpers.cs:ForEachAssignment` Compound assignment passes the compound op as the value. Fix: pass the right-hand side. Severity: Low. Source: code-reviewer.
- [ ] `src/E128.Analyzers/Security/FipsTypeClassifier.cs:Classify` BCL fast path skips vendored cipher assemblies. Fix: skip only known BCL references, or scan every non-BCL assembly. Severity: Low. Source: attacker-1, security-reviewer.
- [ ] `src/E128.Analyzers/Security/FipsSymbolHelpers.cs:NeverReassignedCache` Same-named locals can collide under SymbolEqualityComparer. Fix: drop the cache for locals or key on declaring syntax. Severity: Low. Source: attacker-1.
- [ ] `src/E128.Analyzers/Security/FipsDirectInstantiationAnalyzer.cs` Name-matched cipher escapes the skip list and double-reports. Fix: consult FipsTypeClassifier.GetVerdict in the skip. Severity: Low. Source: code-reviewer.
- [ ] `src/E128.Analyzers/Security/FipsCipherModeAnalyzer.cs` Message recommends CBC against authenticated policy. Fix: point at AES-GCM. Severity: Low. Source: code-reviewer.
- [ ] `tests/E128.Analyzers.Tests/*` Untested documented behaviors on E128145, 146, 150, 151, 143, 149, 142. Fix: one test per README sentence. Severity: Low. Source: attacker-2.
- [ ] `scripts/internal/analyzer-release-check.sh` Dedup stops at a hard-coded section header. Fix: filter the table lines and fail when both headers miss. Severity: Low. Source: security-reviewer.
- [ ] `Directory.Packages.props` `Microsoft.CodeAnalysis.Workspaces.Common` pin unused directly. Fix: route to the `prune-deps` skill. Severity: Low. Source: dotnet lens.
- [ ] `src/E128.Analyzers/Security/FipsRsaKeySizeAnalyzer.cs` + `FipsPbkdfIterationAnalyzer.cs` Comments cite nonexistent FIPS0003/FIPS0005. Fix: name the real anchor. Severity: Low. Source: attacker-3.
- [ ] `lode/dotnet/analyzer-candidates.md` Stale next-free-rule sentence. Fix: drop the contiguous-range clause. Severity: Low. Source: code-reviewer.
- [ ] `src/E128.Analyzers.CodeFixers/Security/InsecureRandomInCryptoContextCodeFixProvider.cs` FindNode omits the tie-breaking idiom. Fix: pass `getInnermostNodeForTie: true`. Severity: Low. Source: attacker-3.
- [ ] `src/E128.Analyzers/Security/FipsDeprecatedRngAnalyzer.cs` + `InsecureRandomInCryptoContextAnalyzer.cs` Three copies of the base-type walk. Fix: one `DerivesFromType` helper in FipsSymbolHelpers. Severity: Low. Source: attacker-3.
- [ ] `src/E128.Analyzers/Security/FipsUnapprovedHashAnalyzer.cs` One closure lambda where siblings use the static-state overload. Fix: convert the call site. Severity: Low. Source: attacker-3.
