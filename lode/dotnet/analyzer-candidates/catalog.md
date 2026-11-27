# Analyzer Candidates: Settled Catalog
*Updated: 2026-11-27T16:16:52Z*

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

The BlazingFastDotnet performance pass on 2026-11-25 shipped the struct-key
equality shape as E128253. Detection shape:
[rule-details-performance-equality.md](../analyzers/rule-details-performance-equality.md).

The analyzer-security-batch-2 plan on 2026-11-27 shipped five security rules:
E128264 to E128268. It also widened E128013 and E128014.

The analyzer-candidates-batch plan on 2026-11-27 shipped eight rules:
E128256 to E128263. That batch closed every open row from the pugworks review
read, the unsafe best-practices read, the second BlazingFastDotnet pass, and
the naming read. All eight sit in `AnalyzerReleases.Unshipped.md` until the
next package release.

The vulnerability checklist sweep on 2026-11-27 checked two security
checklists against the installed rule sets. It found three shapes with no
owner. The path containment shape shipped as E128263, so two rows remain. See
[vulnerability-sweep-2026-11-27.md](vulnerability-sweep-2026-11-27.md).

The implicit state machine rule ships as E128218. See
[implicit-state-machine-rule.md](../implicit-state-machine-rule.md).
The union and closed-hierarchy family ships as E128219 to E128221, from the
net11-upgrade plan. Detection shape: [rule-details-union.md](../analyzers/rule-details-union.md).
The hand-rolled process capture shape ships as E128223, the first rule from the
conversion plan. Open .NET 11 conversion shapes: [net11-conversion-sweep.md](net11-conversion-sweep.md).

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
| E128253 | struct-key-without-equatable           | implemented | 3/5   | 2026-11-25 |
| E128254 | static-readonly-reflection-write       | implemented | 3/5   | 2026-11-25 |
| E128255 | elementwise-span-loop                  | implemented | n/a   | 2026-11-25 |
| E128256 | log-argument-carries-local-path        | implemented | 4/5   | 2026-11-27 |
| E128257 | bound-options-property-not-public      | implemented | 4/5   | 2026-11-27 |
| E128258 | service-lookup-throws-generic-exception | implemented | 3/5  | 2026-11-27 |
| E128259 | plural-twin-type-name                  | implemented | 4/5   | 2026-11-27 |
| E128260 | retained-memory-read-after-dispose     | implemented | 3/5   | 2026-11-27 |
| E128261 | padded-struct-marshal-copy             | implemented | 3/5   | 2026-11-27 |
| E128262 | http-context-accessor-in-service       | implemented | 3/5   | 2026-11-27 |
| E128263 | path-containment-prefix                | implemented | 4/5   | 2026-11-27 |
| E128264 | overlapping-alternation                | implemented | 3/5   | 2026-11-27 |
| E128265 | dynamic-regex-pattern                  | implemented | 3/5   | 2026-11-27 |
| E128266 | certificate-chain-bypass               | implemented | 3/5   | 2026-11-27 |
| E128267 | decompression-without-bound            | implemented | 3/5   | 2026-11-27 |
| E128268 | weak-symmetric-key-source              | implemented | 3/5   | 2026-11-27 |

## ID Maps And Owned Shapes

The guidance-document ID ranges, the FIPS set, the proof-test sweep, and the
rejected-shape table outgrew this file. They live in
[catalog-id-maps.md](catalog-id-maps.md).

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
