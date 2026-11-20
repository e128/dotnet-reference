# Analyzer Candidates: Settled Catalog
*Updated: 2026-11-20T14:23:21Z*

This file holds the settled side of the register: which rule shipped, which idea
was checked and rejected, and which sweep closed. Every rule ships from
`src/E128.Analyzers`. The open side, including every candidate that waits for a
decision, lives in [index.md](index.md).

No open candidate remains from the 2026-04-15 mining pass. Each of the three
candidates from that pass ships as a real analyzer. See the catalog table for the
rule ID, and see `src/E128.Analyzers/README.md` for the current rule
description.

The 2026-11-05 external sweep is complete. See
[external-sweep.md](external-sweep.md).
The twenty-practice performance sweep shipped fifteen rules, and its E128037
extension also ships. See [performance-sweep.md](performance-sweep.md).
The async sweep closed with no rule. See [async-sweep.md](async-sweep.md).
The slopwatch read on 2026-11-18 ships E128232, E128236, and E128237. See
[slopwatch-sweep.md](slopwatch-sweep.md).
The five roadmap shapes ship as E128233 to E128237. See
[rule-details-roadmap.md](../analyzers/rule-details-roadmap.md).

The implicit state machine rule ships as E128218. See
[implicit-state-machine-rule.md](../implicit-state-machine-rule.md).
The union and closed-hierarchy family ships as E128219 to E128221, from the
net11-upgrade plan. Detection shape: [rule-details-union.md](../analyzers/rule-details-union.md).
The hand-rolled process capture shape ships as E128223, the first rule from the
conversion plan. Open .NET 11 conversion shapes: [net11-conversion-sweep.md](net11-conversion-sweep.md).

- `mutable-static-readonly-array` shipped as E128061.
- `test-reference-assemblies-tfm-mismatch` shipped as E128062.
- `readonly-struct-property-no-init` shipped as E128074.

## Catalog

| ID      | Name                                   | Status      | Score | Last seen  |
| ------- | -------------------------------------- | ----------- | ----- | ---------- |
| E128061 | mutable-static-readonly-array          | implemented | 5/5   | 2026-04-15 |
| E128062 | test-reference-assemblies-tfm-mismatch | implemented | 4/5   | 2026-04-15 |
| E128074 | readonly-struct-property-no-init       | implemented | 3/5   | 2026-04-15 |
| E128070 | pool-rent-capacity-guard               | implemented | n/a   | 2026-05-09 |
| E128071 | fips-unapproved-hash                   | implemented | n/a   | 2026-05-10 |
| E128072 | sha256-create-obsolete                 | implemented | n/a   | 2026-05-10 |
| E128054 | temp-dir-no-cleanup                    | implemented | 5/5   | 2026-04-12 |
| E128056 | fileinfo-exists-toctou                 | implemented | 4/5   | 2026-04-12 |
| E128057 | finally-block-swallows-exception       | implemented | 3/5   | 2026-04-12 |
| E128058 | list-as-readonly-return                | implemented | 3/5   | 2026-04-13 |
| E128059 | unused-interface-parameter             | implemented | 4/5   | 2026-04-13 |
| E128063 | mid-name-underscore                    | implemented | 4/5   | 2026-04-15 |
| E128082 | composite-detection-specificity        | implemented | 3/5   | 2026-05-06 |
| E128089 | bare-parse-without-tryparse            | implemented | 3/5   | 2026-05-26 |
| E128092 | async-file-io                          | implemented | 5/5   | 2026-07-22 |
| E128096 | sync-local-function-call               | implemented | 3/5   | 2026-07-23 |
| E128098 | chained-string-replace                 | implemented | 4.5/5 | 2026-08-06 |
| E128099 | untimed-process-exit                   | implemented | 5/5   | 2026-08-06 |
| E128100 | oce-when-token-filter                  | implemented | 3.5/5 | 2026-08-06 |
| E128088 | switch-enum-not-exhaustive             | implemented | 3/5   | 2026-08-08 |
| E128094 | pragma-before-file-scoped-namespace    | implemented | 4/5   | 2026-08-08 |
| E128097 | comment-in-test-code                   | implemented | 3/5   | 2026-08-08 |
| E128101 | process-output-not-validated           | implemented | 3/5   | 2026-08-13 |
| E128103 | unregistered-service-resolve           | implemented | 4/5   | 2026-09-26 |
| E128104 | constant-operand-assertion             | implemented | 4/5   | 2026-09-26 |
| E128105 | out-of-repo-resolver-no-skip           | implemented | 3.5/5 | 2026-09-26 |
| E128106 | parallel-collection-index              | implemented | 3/5   | 2026-09-26 |
| E128107 | low-value-test-condition               | implemented | n/a   | 2026-10-03 |
| E128108 | low-value-test-conditions              | implemented | n/a   | 2026-10-03 |
| E128218 | implicit-state-machine                 | implemented | n/a   | 2026-11-07 |
| E128219 | memberless-abstract-base-is-closed     | implemented | 4/5   | 2026-11-12 |
| E128220 | union-switch-discard-arm               | implemented | 4/5   | 2026-11-12 |
| E128221 | union-value-read                       | implemented | 4/5   | 2026-11-12 |
| E128222 | uncontained-root-join                  | implemented | 5/5   | 2026-11-10 |
| E128223 | hand-rolled-process-capture            | implemented | 5/5   | 2026-11-13 |
| E128224 | read-only-memory-stream                | implemented | 4/5   | 2026-11-13 |
| E128225 | abstract-base-with-members-is-closed   | implemented | 4/5   | 2026-11-13 |
| E128226 | comparer-set-by-loop                   | implemented | 3.5/5 | 2026-11-13 |
| E128227 | http-body-read-without-size-bound      | implemented | 3.5/5 | 2026-11-13 |
| E128228 | token-validation-flag-false            | implemented | n/a   | 2026-11-17 |
| E128229 | middleware-func-task-next-delegate     | implemented | 4/5   | 2026-11-17 |
| E128230 | static-files-runtime-pipeline          | implemented | 4/5   | 2026-11-17 |
| E128231 | split-result-enumerated                | implemented | 3.5/5 | 2026-11-17 |
| E128232 | dynamic-keyword-in-a-type-position     | implemented | 4/5   | 2026-11-18 |
| E128233 | parallel-for-per-item-body             | implemented | n/a   | 2026-11-18 |
| E128234 | filestream-captured-by-parallel-body   | implemented | n/a   | 2026-11-18 |
| E128235 | readonly-field-loop-hoist              | implemented | n/a   | 2026-11-18 |
| E128236 | unconditional-test-delay               | implemented | 3.5/5 | 2026-11-18 |
| E128237 | foreign-test-disable                   | implemented | 3/5   | 2026-11-18 |
| E128238 | array-compared-by-reference            | implemented | n/a   | 2026-11-19 |
| E128239 | discarded-stream-read-count            | implemented | n/a   | 2026-11-19 |
| E128240 | blocking-call-in-lock                  | implemented | n/a   | 2026-11-19 |
| E128241 | cors-wildcard-with-credentials         | implemented | n/a   | 2026-11-19 |
| E128242 | antiforgery-missing-on-action          | implemented | n/a   | 2026-11-19 |
| E128243 | list-span-invalidated-by-mutation      | implemented | n/a   | 2026-11-19 |
| E128244 | division-guard-order                   | implemented | n/a   | 2026-11-19 |
| E128245 | enum-cast-outside-member-set           | implemented | n/a   | 2026-11-19 |
| E128246 | flags-member-not-power-of-two          | implemented | n/a   | 2026-11-19 |
| E128247 | readonly-mutable-struct-field          | implemented | n/a   | 2026-11-19 |
| E128248 | floating-point-loop-counter            | implemented | n/a   | 2026-11-19 |
| E128249 | array-growth-in-loop                   | implemented | n/a   | 2026-11-19 |
| E128250 | dead-private-field-store               | implemented | n/a   | 2026-11-19 |
| E128251 | loop-with-no-exit-path                 | implemented | n/a   | 2026-11-19 |

## Rules From Guidance Documents

Two published guidance documents seeded the rules below. Most rules carry a code
fix provider. The rules E128125 to E128129 and E128136 to E128138 carry a
diagnostic only.

- Source A: the .NET performance skill guide in `EgorBo/BlazingFastDotnet`.
- Source B: the runtime unsafe-code guidance page in `dotnet/docs`.

This table records the rule IDs so a later session can check for ID reuse. The
rule description and the fix shape live in `src/E128.Analyzers/README.md`.

| ID range        | Domain      | Code fix        | Category directory |
| --------------- | ----------- | --------------- | ------------------ |
| E128112-E128115 | Unsafe code | every rule      | `Unsafe/`          |
| E128116-E128117 | Performance | every rule      | `Performance/`     |
| E128118-E128120 | Unsafe code | every rule      | `Unsafe/`          |
| E128121-E128124 | Performance | every rule      | `Performance/`     |
| E128125-E128129 | Unsafe code | no rule         | `Unsafe/`          |
| E128131-E128135 | Performance | E128135 partial | `Performance/`     |
| E128136-E128138 | Unsafe code | no rule         | `Unsafe/`          |
| E128139         | Unsafe code | `bool` read     | `Unsafe/`          |
| E128140-E128141 | Performance | every rule      | `Performance/`     |

The unused and retired IDs follow `AnalyzerReleases.Shipped.md` and
`AnalyzerReleases.Unshipped.md` in `src/E128.Analyzers`. Take the next free ID
from those manifests, never from a row in this file.

## FIPS Cryptography Rules

Rules E128071, E128075, and E128142 to E128151 implement the FIPS policy set.
See `src/E128.Analyzers/README.md` for each rule description. Shared symbol and
constant helpers live in `FipsSymbolHelpers.cs`.

| ID range        | Domain   | Code fix | Category directory |
| --------------- | -------- | -------- | ------------------ |
| E128142-E128151 | Security | no rule  | `Security/`        |

These rules have no code fix. The remediation depends on intent that the syntax
tree does not carry.

- E128125: the pin scope is a caller decision.
- E128126: the replacement depends on how the new string is built.
- E128127: safe code has no null byref equivalent.
- E128128: the caller places the anchor field.
- E128129: the replacement depends on whether the caller needs a deep or a shallow copy.

Each read of the two guidance documents, the rows that stay a document, and the
unshipped idea that waits on a real defect live in
[guidance-reads.md](guidance-reads.md).

## Proof-Test Sweep Candidates

The research page "Deterministic Static Code Analysis Rules: Proof-Test"
grades the sufficiency claim for static analysis. It carries no rule list. Its
20 methodology names and its source list seeded this sweep. The methodology
profile pages sit behind Cloudflare Access, so their content was not read.

The other sources are the .NET code-analysis rule index, the CERT C and CERT
Java standards, the CWE Top 25, the NASA Power of Ten, and the Clang Static
Analyzer check list.

Every candidate is decidable from one compilation or one method body. Each was
checked against the installed rule sets: CA and IDE, Meziantou 3.0.318,
Roslynator 5.0.1, Sonar 10.35, SharpSource, and AsyncFixer. The CA security
rules run because `AnalysisLevel=latest-all` sets `All` mode.

Nine rules from this sweep ship: E128154 to E128157 and E128159 to E128163.
The rule table in `src/E128.Analyzers/README.md` carries their behavior and
code-fix status.

E128158 stays unused. The C# compiler reports CS8425 when an async iterator
takes a token and carries no `[EnumeratorCancellation]` attribute, so a second
rule would repeat a build failure.

CA5359 reads the `ServicePointManager` callback only. The `HttpClientHandler`
path stays open, so E128156 covers it.

The Tier 2 shapes that wait for a real defect in review move with the open
register. See [index.md](index.md).

## Checked And Rejected

These shapes already have an owner. A second rule would report the same line.
The last two rows come from the slopwatch read, which checked the pragma and
empty-catch shapes against the installed rule sets.

| Rule                                                        | Already covered by                                  |
| ----------------------------------------------------------- | --------------------------------------------------- |
| `[SuppressMessage]` without a justification                 | E128173                                             |
| Empty or comment-only `catch` block                         | S2486, and E128172 for a swallowed assertion        |
| `#pragma warning disable` with no justification or restore  | E128047, E128055, E128065, E128094                  |
| Shift count out of range                                    | S2183                                               |
| Method length                                               | MA0051                                              |
| Ignored `Try*` return value                                 | MA0060, with `enable_tryparse_pattern`              |
| Throw from `finally`                                        | MA0072                                              |
| NaN comparison                                              | CA2242                                              |
| `goto`                                                      | S907                                                |
| Hardcoded credential literal                                | S2068, gitleaks                                     |
| Cookie `Secure` and `HttpOnly` flags                        | S2092, S3330                                        |
| Insecure serializers                                        | CA2300, CA2301, CA2328                              |
| SQL, path, command, and open redirect injection             | CA3001, CA3003, CA3006, CA3007                      |
| `ServicePointManager` certificate bypass                    | CA5359                                              |
| Culture-sensitive string compare                            | CA1307, CA1310                                      |
| Multiple enumeration                                        | CA1851                                              |
| `ContainsKey` then indexer                                  | CA1853, CA1854                                      |
| `GetOrAdd` with an eager value                              | MA0242                                              |
| `UseShellExecute` with a redirect                           | MA0163                                              |
| Reference type as a dictionary key                          | MA0066                                              |
| `Path.GetTempFileName`                                      | S5445                                               |
| Regex call without a timeout                                | S6444                                               |
| `Select(async ...)` result never awaited                    | MA0134, VSTHRD110                                  |
| Consecutive `Path.Combine` calls                            | CA1877                                              |
| `JsonDocument.RootElement` escape                           | CA2026                                              |
| Object disposed twice                                       | CA2202 retired, and Dispose is idempotent by contract |
| Assertion density, dead code, comment density               | Metric or judgment checks. IDE0051, IDE0052, and CS0162 cover the code shapes |

## AI-Assisted Technical Debt Candidates

Source: [Syncfusion technical debt article](https://www.syncfusion.com/blogs/post/vibe-coding-technical-debt).

`decoded-jwt-authenticated-identity` shipped as E128202.
Token readers and string token constructors decode tokens without validation.
The rule tracks decoded claims into `ClaimsIdentity` with a nonempty or non-constant authentication type.
It reports a flow into `HttpContext.User` or an `AuthenticationTicket`, including nested lambda and local function bodies.
A preceding synchronous validation or a checked async validation on the same unchanged token clears the report.
No automatic fix applies because validation needs application policy.
See [rule-details.md](analyzers/rule-details.md) for the detection shape and the gaps.

No other shipped E128 rule references these APIs.
The installed Sonar S5659 source checks JWT.Net, not the Microsoft IdentityModel handlers.
A static scan of the installed analyzer assemblies found no owner for this shape.

Evidence: [Microsoft token reader](https://learn.microsoft.com/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytokenhandler.readjwttoken),
[authenticated identity contract](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsidentity.isauthenticated),
and [Sonar S5659 source](https://github.com/SonarSource/sonar-dotnet/blob/10.35.0.4138/analyzers/src/SonarAnalyzer.Core/Rules/JwtSignedBase.cs).

Duplicate conditional branches and cognitive complexity have Sonar owners: S1871, S3923, and S3776.
Test expectation changes, repeated file edits, and semantic duplication require review rather than a local diagnostic.
Existing E128 testing rules catch concrete assertion and skip defects.
A lone `Assert.NotNull` assertion does not prove a weak test.
