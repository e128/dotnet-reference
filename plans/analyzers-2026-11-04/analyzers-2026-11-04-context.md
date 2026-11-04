# Analyzer Plan Context: Proof-Test Sweep

*Updated: 2026-11-04T13:32:31Z*

## Problem

The repository installs five third-party analyzer packages plus the full CA and
IDE rule sets. Gaps remain where a defect is decidable from one compilation and
no installed rule fires. Those gaps cost real review time, because a human finds
the defect each time.

## Where The Candidates Came From

The research page "Deterministic Static Code Analysis Rules: Proof-Test" grades
the claim that deterministic static rules are a sufficient basis for defect
detection. It returns Tier 3, meaning the claim does not survive.

The page carries no rule list. It names 20 analysis methods and cites seven
sources. The method names seeded the search for missing rules:

- Abstract interpretation, data-flow analysis, control-flow analysis
- Taint analysis, signal-flow analysis, data-race analysis
- Dead-code recognition, rule checking against MISRA, CERT, and CWE
- Path-sensitive semantic bug finding, static loop-bound proof
- Recursion and call-graph check, assertion density

The sources were read directly for rule shapes: the .NET code-analysis index,
CERT C, CERT Java, the CWE Top 25, the NASA Power of Ten, and the Clang Static
Analyzer check list.

The e128.info methodology profile pages sit behind Cloudflare Access. They
return a redirect and no content, so no claim here rests on them.

## Coverage Method

Each candidate was checked against every installed rule set before it was kept:
CA and IDE, Meziantou, Roslynator, Sonar, SharpSource, and AsyncFixer. The CA
security rules run because `AnalysisLevel=latest-all` sets `AnalysisMode=All`.

A candidate was rejected when an installed rule reports the same line. The
rejected list is 24 rows in
[analyzer-candidates.md](../../lode/dotnet/analyzer-candidates.md).

## Decisions

**Nine Tier 1 rules ship, and seventeen Tier 2 rules wait.** Tier 1 ships first.
Tier 2 waits for a real defect in review, because its value is lower or its noise
is higher. E128158 is dropped from Tier 1, because the compiler already fails that
shape.

**New IDs start at E128154.** The catalog ended at E128153. E128095, E128130, and
E128158 stay unused, and the next rule takes E128164. The range check runs before
each phase.

**E128156 targets `HttpClientHandler` only.** CA5359 covers the
`ServicePointManager` callback. The `HttpClientHandler` path stays open. The
`SocketsHttpHandler.SslOptions` callback is out of scope, because that handler
exposes no callback property.

**E128158 is gated on Sonar S9426.** If that rule ships in an installed Sonar
version, E128158 is dropped rather than written.

**E128157 scopes to producers and names.** It reports on a value whose name marks
it as a tag, MAC, or signature, and on a value from an `HMAC*` type. A plain
content hash stays silent, because a hash compare is not a forgery check. An
equality operator on a byte array stays silent too, because it compares
references.

**Four rules ship without a code fix.** E128154, E128156, E128160, and E128162.
A Tier 2 row follows the same rule when its remediation depends on intent. A
registered fix that returns the document unchanged shows a dead action.

## Risks

- **E128154 noise.** `IServiceProvider` injection is a legitimate escape hatch.
  The skip list in the plan covers it. A remaining false positive stays
  actionable, because the message names the two lifetimes.
- **E128155 precision.** A mutation through an alias defeats the analysis. The
  rule reports the direct receiver only.
- **E128162 scope.** The rule models an explicit `Dispose()` call in the same
  block. It does not model scope exit, so a `using` block stays silent by
  design.
- **E128161 false negative.** A rooted segment built at run time is invisible.
  The rule reports constants only.
- **Tier 2 noise.** "Loop with no exit path" and "dead store" both need a
  control-flow graph. Each carries medium noise.

## Runners-Up

Three shapes were considered and left out:

| Shape                              | Why it was left out                                |
| ---------------------------------- | -------------------------------------------------- |
| Assertion density per method       | Metric, not a defect. NASA Power of Ten rule 5     |
| Recursion depth bound              | Needs a call-graph proof, not a local check        |
| Loop-bound proof for termination   | Halting problem outside a narrow counted-loop case |

## See Also

- [plan](analyzers-2026-11-04-plan.md)
- [tasks](analyzers-2026-11-04-tasks.md)
- [candidate catalog](../../lode/dotnet/analyzer-candidates.md)
