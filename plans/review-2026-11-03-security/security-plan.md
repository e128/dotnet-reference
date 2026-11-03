# Review Plan: FIPS Analyzer Gaps, 2026-11-03

*Updated: 2026-11-03T12:51:57Z*

Target: commit `74b9a01` (FIPS cryptography policy rules E128142-E128151), base
`f236ecf`. 55 files, +8279/−314. One confirmed High finding. Application order:
Phase 1 first, then Phase 2, then Phase 3.

The run built on the working tree as of 2026-11-03. Three fixes already land in
the tree from this run: the initializer-only `byte[]` length in
`FipsSymbolHelpers`, the default-curve report for `ECDsa.Create()` in
`FipsRsaKeySizeAnalyzer`, and the `fips-rules.md` invariant rewrite. Their tests
run green. Those fixes need one commit but no further work.

## Phase 1: High Security Gap

| # | File | Anchor | Problem | Sev |
|---|------|--------|---------|-----|
| 1 | src/E128.Analyzers/Security/FipsRsaKeySizeAnalyzer.cs | CheckCurveValue | E128143 stays silent when it cannot resolve the curve name. `ECCurve.CreateFromFriendlyName` or `CreateFromValue` with a non-constant argument, a curve from a parameter, and a populated `new ECCurve { ... }` struct all pass unread. The twin key-size path reports "cannot be verified at compile time" on purpose, so the asymmetry is in-rule. A below-384-bit curve passes an Error-severity gadget silently. | High |

Fix: add the else branch. Report "curve cannot be verified at compile time; use
nistP384 or nistP521" when the curve value fails to resolve. Mirror the message
shape of `CheckKeySize`. Add a README sentence and one test per unresolved form
(friendly name, Oid value, parameter, struct initializer).

## Phase 2: Medium Findings

| # | File | Anchor | Problem | Sev |
|---|------|--------|---------|-----|
| 2 | .globalconfig | line 44 | `dotnet_diagnostic.E128075.severity = none` suppresses a rule this same change raises to Error in `AnalyzerReleases.Unshipped.md`. Dogfooding contradicts the shipped policy, and the editorconfig downgrade has no recorded user approval. | Medium |
| 3 | src/E128.Analyzers/Security/FipsUnapprovedHashAnalyzer.cs | IsExemptFromHashNameCheck | The exemption accepts any method whose name starts with `Verify` on any `System` or `Microsoft` namespace tree. Naming decides the check, not the symbol. | Medium |
| 4 | src/E128.Analyzers/Security/FipsDirectInstantiationAnalyzer.cs | AnalyzeObjectCreation | The report runs only when `SuggestFactory` returns a suggestion, so a banned instantiation with an unmappable constructor stays silent. `UsesCngKeyStorage` also exempts every string parameter, wider than its comment. | Medium |
| 5 | src/E128.Analyzers/Security/FipsSymbolHelpers.cs | IsComparison | An instance `Equals` call reports its receiver as a selection. `CipherMode.ECB.Equals(aes.Mode)` on E128144/E128146/E128071 false-positives. | Medium |
| 6 | src/E128.Analyzers.CodeFixers/Security/InsecureRandomInCryptoContextCodeFixProvider.cs | fix registration | The fixer bypasses the documented `UsingDirectives.AddIfMissing` helper and drives `ImportAdder` plus `Simplifier` instead, a second mechanism no other fixer uses. | Medium |
| 7 | src/E128.Analyzers/Security/FipsSymbolHelpers.cs | ReportHashNameArguments, ReportHashNameStores | Both non-generic overloads have no caller. Delete them, or convert the one closure call site. | Medium |
| 8 | AGENTS.md | Subagents section | Unrelated process change rides in the feature commit. Split into its own commit or revert the hunk. | Low |

## Phase 3: Low Findings

| # | File | Anchor | Problem | Sev |
|---|------|--------|---------|-----|
| 9 | src/E128.Analyzers/Security/FipsSymbolHelpers.cs | ForEachAssignment compound branch | A compound assignment passes the whole operation as the value, so a constant increment reports "cannot be verified" instead of the real message. | Low |
| 10 | src/E128.Analyzers/Security/FipsTypeClassifier.cs | Classify fast path | Any type whose namespace root is `System` or `Microsoft` skips the forbidden-name scan, so a vendored cipher assembly escapes E128148. | Low |
| 11 | src/E128.Analyzers/Security/FipsSymbolHelpers.cs | NeverReassignedCache | Two distinct locals of the same name in a same-named method pair can compare equal under `SymbolEqualityComparer.Default` and share a wrong verdict. | Low |
| 12 | src/E128.Analyzers/Security/FipsDirectInstantiationAnalyzer.cs | AnalyzeObjectCreation | A name-matched cipher type with a `Managed` suffix reports both E128142 and E128148, breaking the one-diagnostic invariant. | Low |
| 13 | src/E128.Analyzers/Security/FipsCipherModeAnalyzer.cs | message text | The failure message points at "CBC with a random IV and a MAC", which contradicts the authenticated-encryption policy. | Low |
| 14 | tests/E128.Analyzers.Tests/* | FipsPbkdfIteration, FipsTlsVersion, FipsDirectInstantiation, FipsRsaKeySize, FipsAesGcmTagSize, FipsSha3, FipsHmacKeyLength | Seven rules leave documented behaviors without tests. See security-context.md for the per-rule list. | Low |
| 15 | scripts/internal/analyzer-release-check.sh | dedup loop | Dedup stops at one hard-coded section header, so a rule ID at or after that header escapes the check silently. | Low |
| 16 | Directory.Packages.props | Workspaces.Common pin | `Microsoft.CodeAnalysis.Workspaces.Common` has no direct `PackageReference`; projects use `CSharp.Workspaces` only. Removal candidate for the `prune-deps` skill. | Low |
| 17 | src/E128.Analyzers/Security/FipsRsaKeySizeAnalyzer.cs | generated-code comments | Comments cite rule anchors `FIPS0003` / `FIPS0005` that exist nowhere. | Low |
| 18 | lode/dotnet/analyzer-candidates.md | next-free-rule claim | The sentence about the contiguous guidance range conflicts with the E128151 fill. | Low |
| 19 | src/E128.Analyzers.CodeFixers/Security/InsecureRandomInCryptoContextCodeFixProvider.cs | node fetch | `FindNode` omits `getInnermostNodeForTie: true`, the repo idiom. | Low |
| 20 | src/E128.Analyzers.Security helper reuse | FindCryptographyBase, IsRandomType | Three copies of the same base-type walk; one helper replaces two. | Low |
| 21 | src/E128.Analyzers/Security/FipsUnapprovedHashAnalyzer.cs | E128071 call site | The one closure lambda where sibling rules use the static-state overload. | Low |

## Notes

- README and rule-table gates pass: `readme-table-diff.sh --analyzer`,
  `analyzer-release-check.sh`, `readme-table-diff.sh`.
- Build diagnostics clean; no new suppression outside test snippets (SYSLIB0021
  pragmas inside test snippets, matching the existing convention).
- Reviewer coverage and raw output live in security-context.md.
