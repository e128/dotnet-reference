# Analyzer Candidates: Open Register
*Updated: 2026-11-18T13:37:01Z*

One register for every open analyzer candidate, whichever repository noticed it.
A candidate enters at status `new`, carries a four-dimension score, and leaves
only when a rule ships or a session closes the decision as `skipped`. The
shipped rules live in [catalog.md](catalog.md), and every declined shape lives
in [skipped-decisions.md](skipped-decisions.md).

Writers: the `analyzer-review-miner` agent, a session in a consumer repository,
and any session in this repository.

## Schema

Each row: `Pattern | Source | Score | Status | Notes`.

| Status    | Meaning                                                      |
| --------- | ------------------------------------------------------------ |
| new       | discovered and scored, not yet planned                       |
| planned   | owned by an active plan                                      |
| skipped   | evaluated and declined, with the reason in the skipped file  |
| candidate | Tier 2 shape that clears the bar and waits for a real defect |

The four dimensions are frequency (0-2), coverage gap (0-1), expressibility
(0-1), and value (0-1). A total of 3 or more is RECOMMEND. The dimension
definitions live in the `analyzer-review-miner` agent definition.

## Open Candidates

The BlazingFastDotnet guide is the same source that seeded the shipped
performance rules. Every shape it lists that already has an owner stays out of
this table. The declined shapes sit in
[skipped-decisions.md](skipped-decisions.md).

No open candidate remains from the 2026-11-17 guidance read.
`ByteSizeType.Is` matches the type by simple name, so a namespace move does not
break the E128080 exemption.

The body-read shape shipped as E128227. See [catalog.md](catalog.md).
The token-validation flag shape shipped as E128228. See [catalog.md](catalog.md).
The JSON enumerator shape closed as skipped, because the enumerator releases no
resource. See [skipped-decisions.md](skipped-decisions.md).

The slopwatch read on 2026-11-18 added one recommended shape and two Tier 2
shapes, and all three ship as E128232, E128236, and E128237. The rule source,
the verdict for all seven rules, and the declined project-file shapes live in
[slopwatch-sweep.md](slopwatch-sweep.md).

No open candidate remains. The dynamic keyword ban ships as E128232. See
[catalog.md](catalog.md).

## Tier 2 Candidates

These clear the same bar and wait for a real defect in review.
The `AsReadOnly()` getter shape left this table on 2026-11-14, because E128058
and E128060 require that shape. See [skipped-decisions.md](skipped-decisions.md).
The token-validation flag shape left this table on 2026-11-17, because the
maintainer chose prevention over the real-defect bar. The five roadmap shapes
left the same way on 2026-11-18, and ship as E128233 to E128237.

| Rule                                                               | Check                                                          | Noise  |
| ------------------------------------------------------------------ | -------------------------------------------------------------- | ------ |
| Array compared by reference                                        | `==` or `object.Equals` on two arrays, except byte arrays      | Low    |
| Discarded `Stream.Read` count                                      | read used as a statement, buffer read later                    | Low    |
| Blocking call inside `lock`                                        | sync `Thread.Sleep`, `File`, `Stream`, or `DbCommand` call     | Low    |
| `CollectionsMarshal.AsSpan` invalidated by a list mutation         | span read after a mutating call on the list                    | Low    |
| `Array.Resize` or `Concat` in a loop                               | call reassigned to the same receiver inside the loop           | Low    |
| CORS wildcard with credentials                                     | one chain holds `AllowAnyOrigin` and `AllowCredentials`        | Low    |
| Antiforgery missing on a state-changing action                     | verb attribute with no antiforgery attribute in scope          | Medium |
| `readonly` field of a mutable struct                               | defensive copy on every member call                            | Low    |
| Floating-point loop counter                                        | `for` loop steps a `float` or `double` local                   | Low    |
| Enum cast outside the member set                                   | constant integral to enum conversion                           | Low    |
| Guard after the division                                           | divisor compared with zero after the division                  | Low    |
| `[Flags]` member value that is not a power of two                  | constant collides with a lower bit                             | Low    |
| Dead store to a private field                                      | assignment killed on every path before a read                  | Medium |
| Loop with no exit path                                             | `while (true)` with no exit call                               | Medium |
| `Base64.*` over `Convert.ToBase64String`/`FromBase64String`        | call to the `Convert` base64 member, waiting on a real defect  | Low    |

## Skipped Decisions

A skipped row keeps `/analyzer` from reporting the same idea twice. The reason
and the evidence for every skipped row live in
[skipped-decisions.md](skipped-decisions.md). That file holds the net11 shapes
declined on 2026-11-13 alongside the earlier decisions.

## Working The Register

1. Read this file, [catalog.md](catalog.md), and
   [skipped-decisions.md](skipped-decisions.md) before you scout, so a settled
   decision is not suggested again.
2. Check the installed rule sets and the C# compiler before you score a
   candidate. A shape that already reports as a build error is not a candidate.
3. Append the row, then add a detail section when the score is 3 or more. A
   sweep with more than three open shapes gets its own file, like
   [net11-conversion-sweep.md](net11-conversion-sweep.md).
4. Take the next free rule ID from `AnalyzerReleases.Shipped.md` and
   `AnalyzerReleases.Unshipped.md` in `src/E128.Analyzers`.
5. Move the row to [catalog.md](catalog.md) when the rule ships, and delete the
   detail section here.

## Related

- [catalog.md](catalog.md): settled rules, rejected ideas, and the ID map
- [skipped-decisions.md](skipped-decisions.md): every declined shape and its reason
- [guidance-reads.md](guidance-reads.md): each read of the two guidance documents
- [net11-conversion-sweep.md](net11-conversion-sweep.md): open .NET 11 conversion shapes
- [slopwatch-sweep.md](slopwatch-sweep.md): 2026-11-18 slopwatch read, with one open and two Tier 2 shapes
- [async-sweep.md](async-sweep.md): closed async fire-and-forget sweep
- [external-sweep.md](external-sweep.md): closed 2026-11-05 external rule-set sweep
- [performance-sweep.md](performance-sweep.md): closed twenty-practice performance sweep

## Captured

A capture records a correction before anyone scores it. The same shape counts
up, and the third capture opens a roadmap item. A C# capture lands here. A
Nushell or Markdown capture stays in the consumer repository ledger.

### Captured: Confine a joined path by checking the resolved result (1/3): score pending
> **Key**: `confine-a-joined-path-by-checking-the-resolved-result-never-b`
> **Class**: `correctness`, **Lang**: `cs`, **File**: `(none)`
> **Pattern**: Confine a joined path by checking the resolved result. Never flatten the leaf with Path.GetFileName, which silently retargets a multi-segment leaf.
> **Why**: A caller that passes a relative subpath such as images/a.png needs that subpath preserved. GetFileName turns it into a.png and the read or delete lands on a different file while the guard reports clean.
> **Source**: `correction 2026-11-11T14:26:32Z`, occurrence 1 of 3

### Captured: A link-aware containment check misses a symlinked occupant (1/3): score pending
> **Key**: `a-link-aware-containment-check-reports-a-symlinked-occupant-a`
> **Class**: `design`, **Lang**: `cs`, **File**: `(none)`
> **Pattern**: A link-aware containment check reports a symlinked occupant as absent. A name picker needs a separate is-occupied test that treats an escaping path as taken.
> **Why**: ExistsInside and ResolveInside both resolve links. A collision loop therefore sees a name taken by an escaping symlink as free. The later ResolveInside throws instead of advancing the suffix. FilePathContainment.IsOccupied answers the name-picker question.
> **Source**: `correction 2026-11-11T16:38:12Z`, occurrence 1 of 3

### Captured: Keep probe classes out of library code (1/3): score pending
> **Key**: `never-write-a-probe-or-scratch-class-into-real-library-code-a`
> **Class**: `design`, **Lang**: `cs`, **File**: `(none)`
> **Pattern**: Never write a probe or scratch class into real library code. Analyzer probes belong in dotnet-reference tests.
> **Why**: A consumer repository is only a client of the E128 analyzers. A scratch class in src/ ships in the library if forgotten, and it puts analyzer research in the wrong repo. Put the probe in dotnet-reference as a unit test instead.
> **Source**: `correction 2026-11-12T02:18:06Z`, occurrence 1 of 3

### Captured: A comment must state only behavior the body shows (1/3): score pending
> **Key**: `a-member-summary-or-code-comment-must-state-only-behavior-the`
> **Class**: `style`, **Lang**: `cs`, **File**: `(none)`
> **Pattern**: A member summary or code comment must state only behavior the body shows. Never assert a thread-safety or pass-ordering guarantee that no code or citation backs.
> **Why**: An uncited comment claim hid a divergence for a whole plan. A parser summary promised a per-call parser while the field was static. An extractor comment said every later pass read the flattened tree while the next statement read the raw HTML. A reader trusts the claim and misses the real behavior.
> **Source**: `correction 2026-11-12T13:50:47Z`, occurrence 1 of 3

### Captured: Return an explicit Result for an expected failure instead of throwing (1/3): score pending
> **Key**: `return-an-explicit-result-for-an-expected-failure-instead-of-`
> **Class**: `design`, **Lang**: `cs`, **File**: `(none)`
> **Pattern**: Return an explicit Result for an expected failure instead of throwing
> **Why**: Railway-oriented programming keeps the happy and unhappy paths linear and composable, and a thrown exception hides the failure from the signature
> **Source**: `correction 2026-11-13T03:36:51Z`, occurrence 1 of 3

### Captured: Replace a repo-wide analyzer relaxation with a scoped suppression on the one declaration that needs it (1/3): score pending
> **Key**: `replace-a-repo-wide-analyzer-relaxation-with-a-scoped-suppres`
> **Class**: `design`, **Lang**: `cs`, **File**: `(none)`
> **Pattern**: Replace a repo-wide analyzer relaxation with a scoped suppression on the one declaration that needs it
> **Why**: Turning ca1815, ca2231, and ss017 to none in .globalconfig lets one union declaration compile. It also drops the struct equality guards for every value type in the repo. A pragma around the union declaration restores the guard everywhere at the cost of three lines.
> **Source**: `correction 2026-11-13T11:54:14Z`, occurrence 1 of 3

### Captured: Audit rules that a new runtime band makes redundant (1/3): score pending
> **Key**: `when-a-language-or-runtime-band-lands-audit-the-installed-e12`
> **Class**: `design`, **Lang**: `cs`, **File**: `(none)`
> **Pattern**: When a language or runtime band lands, audit the installed E128 rule set. Record each rule that the new mechanism makes redundant for removal.
> **Why**: A rule that guards a shape the new API removes becomes permanent noise. The net11 band replaces the hand-rolled process capture, so E128160 guards a shape that cannot be written once Process.RunAndCaptureText is adopted. The audit keeps the rule set shrinking as the platform grows.
> **Source**: `correction 2026-11-13T12:09:27Z`, occurrence 1 of 3

### Captured: A disposal analyzer must not flag a process-global instance that the owning library disposes at exit (1/3): score pending
> **Key**: `a-disposal-analyzer-must-not-flag-a-process-global-instance`
> **Class**: `design`, **Lang**: `cs`, **File**: `(consumer repository)`
> **Pattern**: IDISP004 must not fire on a deliberately discarded long-lived singleton handed back by a library factory
> **Why**: `XenoAtom.Terminal.Terminal.Initialize` returns the process-global `TerminalInstance`, and the library disposes it at process exit. The caller has no lifetime to own, and disposing it would tear down the terminal it just configured. The pragma is the correct answer today, so the rule adds a false positive on every global-instance factory.
> **Source**: `session friction 2026-11-14T17:51:00Z`, occurrence 1 of 3

### Captured: S109 and S2339 leave a small public constant table unbuildable (1/3): score pending
> **Key**: `s109-and-s2339-leave-a-small-public-constant-table-unbuildable`
> **Class**: `design`, **Lang**: `cs`, **File**: `(consumer repository)`
> **Pattern**: A value with an inherent number, such as a process exit code, must be expressible once in a public table
> **Why**: S109 wants each literal behind a named constant, S2339 forbids a public `const` member, and neither accepts the answer of the other. The workaround is a private const per value behind a public static property, which doubles the type for no behavior. Either S109 should skip a member whose declaration is the name, or S2339 should allow a const on a table type.
> **Source**: `session friction 2026-11-14T18:20:00Z`, occurrence 1 of 3

