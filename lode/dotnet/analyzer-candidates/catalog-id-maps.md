# Analyzer ID Maps And Owned Shapes
*Updated: 2026-11-27T15:20:00Z*

The ID ranges and the rejected-shape table that the catalog outgrew. The rule
table lives in [catalog.md](catalog.md), and the open shapes live in
[index.md](index.md).

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

## Checked And Rejected

These shapes already have an owner. A second rule would report the same line.
The pragma and empty-catch rows come from the slopwatch read. The security rows
come from the 2026-11-27 vulnerability checklist sweep.

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
| Insecure serializers                                        | CA2300, CA2301, CA2328, CA2350 to CA2356, SYSLIB0011 |
| Archive entry path joined into a write target               | CA5389, enabled by `AnalysisMode=All`               |
| `DtdProcessing.Parse` with an XML resolver                  | S2755, CA5369 to CA5372                             |
| Built `ProcessStartInfo.Arguments` string                   | E128091                                             |
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

## Related

- [catalog.md](catalog.md): the shipped rule table and the AI-assisted notes
- [index.md](index.md): the open register
- [skipped-decisions.md](skipped-decisions.md): every declined shape
