# Analyzer Candidates: Open Register
*Updated: 2026-11-14T00:08:28Z*

One register for every open analyzer candidate, whichever repository noticed it.
A candidate enters at status `new`, carries a four-dimension score, and leaves
only when a rule ships or a session closes the decision as `skipped`. The
shipped rules live in [catalog.md](catalog.md), and every declined shape lives
in [skipped-decisions.md](skipped-decisions.md).

Writers: the `analyzer-review-miner` agent, `scripts/pug-heuristic.nu` in the
pugworks sibling checkout, and any session in this repository.

## Schema

Each row: `Pattern | Source | Score | Status | Notes`.

| Status    | Meaning                                                      |
| --------- | ------------------------------------------------------------ |
| new       | discovered and scored, not yet planned                       |
| planned   | owned by an active plan                                      |
| skipped   | evaluated and declined, with the reason in the skipped file  |
| candidate | Tier 2 shape that clears the bar and waits for a real defect |

The four dimensions are frequency (0-2), coverage gap (0-1), expressibility
(0-1), and value (0-1). A total of 3 or more is RECOMMEND. The rubric lives in
[scoring-rubric.md](../../infrastructure/scoring-rubric.md).

## Open Candidates

No open candidate remains.

The body-read shape shipped as E128227. See [catalog.md](catalog.md).
The JSON enumerator shape closed as skipped, because the enumerator releases no
resource. See [skipped-decisions.md](skipped-decisions.md).

## Tier 2 Candidates

These clear the same bar and wait for a real defect in review.
The `AsReadOnly()` getter shape left this table on 2026-11-14, because E128058
and E128060 require that shape. See [skipped-decisions.md](skipped-decisions.md).

| Rule                                                               | Check                                                          | Noise  |
| ------------------------------------------------------------------ | -------------------------------------------------------------- | ------ |
| Array compared by reference                                        | `==` or `object.Equals` on two arrays, except byte arrays      | Low    |
| Discarded `Stream.Read` count                                      | read used as a statement, buffer read later                    | Low    |
| Blocking call inside `lock`                                        | sync `Thread.Sleep`, `File`, `Stream`, or `DbCommand` call     | Low    |
| `CollectionsMarshal.AsSpan` invalidated by a list mutation         | span read after a mutating call on the list                    | Low    |
| `Array.Resize` or `Concat` in a loop                               | call reassigned to the same receiver inside the loop           | Low    |
| CORS wildcard with credentials                                     | one chain holds `AllowAnyOrigin` and `AllowCredentials`        | Low    |
| Antiforgery missing on a state-changing action                     | verb attribute with no antiforgery attribute in scope          | Medium |
| Token validation flag set to constant `false`                      | six `TokenValidationParameters` flags                          | Low    |
| `readonly` field of a mutable struct                               | defensive copy on every member call                            | Low    |
| Floating-point loop counter                                        | `for` loop steps a `float` or `double` local                   | Low    |
| Enum cast outside the member set                                   | constant integral to enum conversion                           | Low    |
| Guard after the division                                           | divisor compared with zero after the division                  | Low    |
| `[Flags]` member value that is not a power of two                  | constant collides with a lower bit                             | Low    |
| Dead store to a private field                                      | assignment killed on every path before a read                  | Medium |
| Loop with no exit path                                             | `while (true)` with no exit call                               | Medium |
| `Base64.*` over `Convert.ToBase64String` or `Convert.FromBase64String` | call to the `Convert` base64 member, waiting on a real defect | Low |

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
- [net11-conversion-sweep.md](net11-conversion-sweep.md): open .NET 11 conversion shapes
- [async-sweep.md](async-sweep.md): closed async fire-and-forget sweep
- [external-sweep.md](external-sweep.md): closed 2026-11-05 external rule-set sweep
- [performance-sweep.md](performance-sweep.md): closed twenty-practice performance sweep

## Captured

A capture records a correction before anyone scores it. The same shape counts
up, and the third capture opens a roadmap item. A C# capture lands here. A
Nushell or Markdown capture stays in the pugworks ledger.

### Captured: Confine a joined path by checking the resolved result, never by flattening the leaf with Path.GetFileName, which silently retargets a multi-segment leaf (1/3): score pending
> **Key**: `confine-a-joined-path-by-checking-the-resolved-result-never-b`
> **Class**: `correctness`, **Lang**: `cs`, **File**: `(none)`
> **Pattern**: Confine a joined path by checking the resolved result, never by flattening the leaf with Path.GetFileName, which silently retargets a multi-segment leaf
> **Why**: A caller that passes a relative subpath such as images/a.png needs that subpath preserved. GetFileName turns it into a.png and the read or delete lands on a different file while the guard reports clean.
> **Source**: `correction 2026-11-11T14:26:32Z`, occurrence 1 of 3

### Captured: A link-aware containment check reports a symlinked occupant as absent, so a name picker needs a separate is-occupied test that treats an escaping path as taken (1/3): score pending
> **Key**: `a-link-aware-containment-check-reports-a-symlinked-occupant-a`
> **Class**: `design`, **Lang**: `cs`, **File**: `(none)`
> **Pattern**: A link-aware containment check reports a symlinked occupant as absent, so a name picker needs a separate is-occupied test that treats an escaping path as taken
> **Why**: ExistsInside and ResolveInside both resolve links, so a collision loop sees a name taken by an escaping symlink as free and the later ResolveInside throws instead of advancing the suffix. FilePathContainment.IsOccupied answers the name-picker question.
> **Source**: `correction 2026-11-11T16:38:12Z`, occurrence 1 of 3

### Captured: Never write a probe or scratch class into real library code; analyzer probes belong in dotnet-reference tests (1/3): score pending
> **Key**: `never-write-a-probe-or-scratch-class-into-real-library-code-a`
> **Class**: `design`, **Lang**: `cs`, **File**: `(none)`
> **Pattern**: Never write a probe or scratch class into real library code; analyzer probes belong in dotnet-reference tests
> **Why**: pugworks is only a client of the E128 analyzers. A scratch class in src/ ships in the library if forgotten, and it puts analyzer research in the wrong repo. Put the probe in dotnet-reference as a unit test instead.
> **Source**: `correction 2026-11-12T02:18:06Z`, occurrence 1 of 3

### Captured: A member summary or code comment must state only behavior the body shows; never assert a thread-safety or pass-ordering guarantee that no code or citation backs (1/3): score pending
> **Key**: `a-member-summary-or-code-comment-must-state-only-behavior-the`
> **Class**: `style`, **Lang**: `cs`, **File**: `(none)`
> **Pattern**: A member summary or code comment must state only behavior the body shows; never assert a thread-safety or pass-ordering guarantee that no code or citation backs
> **Why**: An uncited comment claim hid a divergence for a whole plan: HotelResultsParser's summary promised a per-call parser while the field was static, and an HtmlExtractor comment said every later pass read the flattened tree while the next statement read the raw HTML. A reader trusts the claim and misses the real behavior.
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
> **Why**: Turning ca1815, ca2231, and ss017 to none in .globalconfig lets one union declaration compile but drops the struct equality guards for every value type in the repo. A pragma around the union declaration restores the guard everywhere at the cost of three lines.
> **Source**: `correction 2026-11-13T11:54:14Z`, occurrence 1 of 3

### Captured: When a language or runtime band lands, audit the installed E128 rule set for rules the new mechanism makes redundant and record each for removal (1/3): score pending
> **Key**: `when-a-language-or-runtime-band-lands-audit-the-installed-e12`
> **Class**: `design`, **Lang**: `cs`, **File**: `(none)`
> **Pattern**: When a language or runtime band lands, audit the installed E128 rule set for rules the new mechanism makes redundant and record each for removal
> **Why**: A rule that guards a shape the new API removes becomes permanent noise. The net11 band replaces the hand-rolled process capture, so E128160 guards a shape that cannot be written once Process.RunAndCaptureText is adopted. The audit keeps the rule set shrinking as the platform grows.
> **Source**: `correction 2026-11-13T12:09:27Z`, occurrence 1 of 3
