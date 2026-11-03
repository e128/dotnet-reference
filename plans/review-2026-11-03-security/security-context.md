# Review Context: FIPS Analyzer Gaps, 2026-11-03

*Updated: 2026-11-03T00:00:00Z*

## Target

Commit `74b9a01` (feat(analyzers): add FIPS cryptography policy rules
E128142-E128151), base `f236ecf`. 55 files, +8279/−314, one commit. No plan
covered the branch before the run; the two 2026-11-01 plans cover earlier
cr-target work. The resolver listed one untracked file
(`lode/dotnet/analyzers/code-fix-trivia.md`) that did not exist on disk; stale
resolver hit, noted.

## Reviewers Run

| Reviewer | Kind | Result |
| --- | --- | --- |
| security-reviewer | vendored subagent | 2 Medium, 3 Low |
| security-reviewer (parallel second pass) | vendored subagent | 1 Medium (curve bypass), 3 Low |
| code-reviewer | vendored subagent | 2 Medium, 5 Low; `readme-table-diff.sh --analyzer` clean |
| adversarial-review attacker-1 (security) | subagent | 1 Medium, 3 Low |
| adversarial-review attacker-2 (functionality) | subagent | 3 defects, 7 untested behaviors |
| adversarial-review attacker-3 (quality) | subagent | 2 Medium, 5 Low |
| code-review | skill at high effort | 3 findings, all fixed in the tree with tests (user approved keeping the fixes) |
| ponytail:ponytail-review | main context | lean/suggestion notes only |
| mr-sparkle-dotnet | repo skill | lens clean; 1 Low orphan pin; 1 advisory |
| defender | subagent | E128143 unverifiable curve: CONFIRMED High |
| adversarial-review:adversarial-review (Codex plugin) | skill | SKIPPED: OpenAI Codex CLI not installed |

## Dedup Report

- E128143 default `ECDsa.Create()` silent: reported by the code-review fork and
  attacker-2. Fixed in the tree during the run; test added
  (`FactoryWithoutCurve_Reports_E128143`).
- E128143 unverifiable curve silent: attacker-1 SECURITY-1 and the vendored
  security pass. Defender evaluated and confirmed at High.
- E128071 `Verify` prefix exemption: attacker-1 SECURITY-2 and the vendored
  security pass agree.
- `.globalconfig` E128075 `severity = none`: code-reviewer Medium and
  attacker-2 FUNC-2.
- `FipsTypeClassifier` BCL fast path: attacker-1 SECURITY-4 and vendored
  security pass.
- Unrelated AGENTS.md change: code-reviewer Medium, attacker-3 Low; kept at Low.

## Working-Tree Changes Made During the Run

The code-review skill reviewer fixed three findings despite the review-only
instruction. The user chose to keep them:

- `FipsSymbolHelpers.cs` `TryGetConstantByteLength`: initializer-only
  `new byte[] { ... }` now resolves its length.
- `FipsRsaKeySizeAnalyzer.cs`: `ECDsa.Create()` and `ECDiffieHellman.Create()`
  without a curve now report the default-curve message.
- `lode/dotnet/analyzers/fips-rules.md`: the "unknown value is not reported"
  invariant rewritten to distinguish rules that report on purpose.
- Tests added: `ShortInitializerArrayKey_Reports_E128151`,
  `FactoryWithoutCurve_Reports_E128143`, `TripleDes3KeyPkcs12` coverage.

Targeted runs over five FIPS test classes pass; `format.sh --changed` clean.

## Untested Documented Behaviors (from attacker-2)

- E128145: bare two-argument `Rfc2898DeriveBytes` default 1000; the
  `IterationCount`-assignment suppression contract.
- E128146: mask test, mask removal incl. `&=`, `~SslProtocols.None`,
  `SecurityProtocolType.Tls11`, `SslProtocols.Default`.
- E128142: CNG persisted-key skip, argument-preserving suggestion string,
  string-parameter skip, E128071-overlap skip.
- E128143: import, certificate, method group, `KeySize` assignment, key-size
  assignment suppression, ECParameters.Curve.
- E128149: Decrypt path, `int? tagSizeInBytes` overload, readonly-field tag.
- E128150: mutable field and local hash-name stores.
- E128151: `Key` assignment path, `IncrementalHash.CreateHmac` and
  `CryptographicOperations.HmacData` branch, exact-32 boundary.
