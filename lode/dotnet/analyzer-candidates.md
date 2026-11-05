# Analyzer Candidates
*Updated: 2026-11-05T13:32:58Z*
*Source: analyzer-review-miner 2026-04-15, evidence window 2026-04-12..2026-04-15*

No open candidate remains from the 2026-04-15 mining pass. Each of the three
candidates from that pass ships as a real analyzer. See the catalog table for the
rule ID, and see `src/E128.Analyzers/README.md` for the current rule
description. The Tier 2 table lists the candidates that wait for a real defect.

The 2026-11-05 external sweep holds open candidates.
See [analyzer-candidates-external-sweep.md](analyzer-candidates-external-sweep.md).

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

## Rules From Guidance Documents

Two published guidance documents seeded the rules below. Most rules carry a code
fix provider. The rules E128125 to E128129 and E128136 to E128138 carry a
diagnostic only.

- Source A: the .NET performance skill guide in `EgorBo/BlazingFastDotnet`.
- Source B: the runtime unsafe-code best-practices page in `dotnet/docs`.

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

The unused IDs are E128095, E128130, and E128158. E128022 is retired. The next
new rule takes E128176, after the last assigned ID.

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

The guidance rows that stay a document and not a rule:

| Row                                    | Why it stays a document                   |
| -------------------------------------- | ----------------------------------------- |
| Ordinal string comparison              | CA1307, CA1309, and CA1310 cover it       |
| `ToLower()` equality idiom             | Meziantou covers it                       |
| Runtime knob tuning                    | Environment variables, not source         |
| False sharing and cache-line padding   | Not visible to the compiler               |
| Thread-local and striped counters      | Detection needs runtime behavior          |
| `WebApplication.CreateSlimBuilder`     | Project-level choice, not a code defect   |
| Source-generated JSON registration     | Whole-project configuration, no local fix |
| Object header and MethodTable access   | No supported replacement                  |
| Fuzzing and raw IL validation          | Process guidance, not a code shape        |
| `AggressiveInlining` overuse           | The size heuristic is too noisy           |

## Second Read Of The Guidance Documents

A second read on 2026-10-31 found ten more rows with a code shape that an
analyzer can see. Nine of them ship. The tenth folded into a shipped rule.

| ID            | Rule                                                   | Source  | Code fix | Noise  |
| ------------- | ------------------------------------------------------ | ------- | -------- | ------ |
| none          | Static byte array literal becomes an RVA span property | C5      | Yes      | Low    |
| E128131       | Parse and format UTF-8 bytes without a string          | C7, C28 | Yes      | Low    |
| E128132       | Use an ordinal comparer in a string dictionary or set  | C27     | Yes      | Low    |
| E128133       | Look up a span key with `GetAlternateLookup`           | C10     | Yes      | Medium |
| E128134       | Use `BitOperations` instead of a manual bit loop       | C17     | Yes      | Medium |
| E128135       | Use `TensorPrimitives` over a numeric span             | C12     | Partial  | Medium |
| E128136       | Use `Span<T>` instead of a pointer and a length        | 16.1    | No       | Low    |
| E128137       | Do not access nonpublic BCL members                    | 3.4     | No       | Low    |
| E128138       | Do not emit raw IL with `ILGenerator`                  | 18.1    | No       | Low    |
| none          | Guard pointer indexing with `Debug.Assert`             | 9.4     | No       | High   |

The byte literal row folded into E128061. That rule owns every
`private static readonly T[]` field, so a second rule would report the same
field twice. The E128061 fix reads a constant byte sequence as a
`ReadOnlySpan<byte>` property and every other element type as
`ImmutableArray<T>`.

The unsafe guidance row 20.2 says to abandon a pooled buffer instead of
returning it in `finally`. The repository requires a zero and a return in
`finally`. Keep the repository rule. It satisfies the row, because the buffer
is provably unused at that point.

The `Debug.Assert` guard idea stays unshipped. Its noise is high, so it needs a
real defect in review before a rule is worth the false positives. The E128139
ID belongs to the unaligned-access rule below.

## Third Read Of The Guidance Documents

A third read on 2026-10-31 found three code shapes that the first two reads
missed. Two of them ship a code fix. E128139 covers the `bool` case only.

| ID      | Rule                                                         | Source                            | Code fix         | Noise  |
| ------- | ------------------------------------------------------------ | --------------------------------- | ---------------- | ------ |
| E128139 | Do not move a non-blittable type through an unaligned access | Unsafe guide, struct bit patterns | `bool` read only | Low    |
| E128140 | Use a span bulk method instead of an element loop            | Performance guide, bulk span APIs | Yes              | Low    |
| E128141 | Use a u8 literal instead of `Encoding.UTF8.GetBytes`         | Performance guide, UTF-8 parsing  | Yes              | Low    |

E128139 extends E128118 to the `Unsafe` read and write calls. E128118 covers
`MemoryMarshal` only. E128140 covers the `Fill`, `Clear`, and `CopyTo` shapes.
E128141 needs C# 11 or later, and the rule checks the language version.

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

### Tier 2 Candidates

These clear the same bar and wait for a real defect in review.

| Rule                                                               | Check                                                          | Noise  |
| ------------------------------------------------------------------ | -------------------------------------------------------------- | ------ |
| Array compared by reference                                        | `==` or `object.Equals` on two arrays, except byte arrays      | Low    |
| Discarded `Stream.Read` count                                      | read used as a statement, buffer read later                    | Low    |
| Blocking call inside `lock`                                        | sync `Thread.Sleep`, `File`, `Stream`, or `DbCommand` call     | Low    |
| `CollectionsMarshal.AsSpan` invalidated by a list mutation         | span read after a mutating call on the list                    | Low    |
| `AsReadOnly()` in a property getter                                | wrapper allocated on every access                              | Low    |
| `Array.Resize` or `Concat` in a loop                               | call reassigned to the same receiver inside the loop           | Low    |
| CORS wildcard with credentials                                     | one chain holds `AllowAnyOrigin` and `AllowCredentials`        | Low    |
| Antiforgery missing on a state-changing action                     | verb attribute with no antiforgery attribute in scope          | Medium |
| Token validation flag set to constant `false`                      | six `TokenValidationParameters` flags                          | Low    |
| `[SuppressMessage]` without a justification                        | attribute with no named justification argument                 | Low    |
| `readonly` field of a mutable struct                               | defensive copy on every member call                            | Low    |
| Floating-point loop counter                                        | `for` loop steps a `float` or `double` local                   | Low    |
| Enum cast outside the member set                                   | constant integral to enum conversion                           | Low    |
| Guard after the division                                           | divisor compared with zero after the division                  | Low    |
| `[Flags]` member value that is not a power of two                  | constant collides with a lower bit                             | Low    |
| Dead store to a private field                                      | assignment killed on every path before a read                  | Medium |
| Loop with no exit path                                             | `while (true)` with no exit call                               | Medium |

## Checked And Rejected

These shapes already have an owner. A second rule would report the same line.

| Rule                                                        | Already covered by                                  |
| ----------------------------------------------------------- | --------------------------------------------------- |
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
