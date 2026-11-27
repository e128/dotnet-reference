# Analyzer Candidates: Open Register
*Updated: 2026-11-27T16:21:50Z*

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

The element-wise `Add` and `Multiply` shapes from the 2026-11-17 guidance read
ship as E128255. See [catalog.md](catalog.md).
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

The dynamic keyword ban ships as E128232. See [catalog.md](catalog.md).

The first candidate entered on 2026-11-20, from a rename recorded in a consumer
repository. The naming-conventions-sweep plan renamed `FetchCommands` to
`FetchOperations` because the plural collided with the real `FetchCommand`
type in the same namespace. The sweep recorded the shape as captured here and
never wrote the row.

The BlazingFastDotnet performance pass on 2026-11-25 shipped its one candidate
as E128253. See [catalog.md](catalog.md) and the detection shape in
[rule-details-performance-equality.md](../analyzers/rule-details-performance-equality.md).

The unsafe best-practices read on 2026-11-25 scored six shapes. Every one
shipped: three inside E128112, E128125, and E128129 on the same day, then
E128254 for the static readonly write, and E128260 and E128261 for the retained
memory read and the padded struct copy. The defect list and the shipped fixes
live in [unsafe-sweep.md](unsafe-sweep.md).

The second BlazingFastDotnet performance pass on 2026-11-25 added three shapes.
Two duplicated an installed rule and left the register. A per-call
`new JsonSerializerOptions()` is CA1869, and an `object` lock target is MA0158.
The `IHttpContextAccessor` rule shipped as E128262.

The pugworks review read on 2026-11-27 scored three shapes. All three shipped:
E128256, E128257, and E128258. The same read found a detection gap in E128033,
which the batch closed. See
[pugworks-review-sweep.md](pugworks-review-sweep.md).

The analyzer-candidates-batch plan on 2026-11-27 shipped every remaining row
from the four reads above, plus the plural twin name shape as E128259. The open
table below holds only the shapes that still wait.

The vulnerability checklist sweep on 2026-11-27 mapped two security checklists
to the installed rule sets. It added three rows. The path containment prefix
shape shipped as E128263, so two rows remain. See
[vulnerability-sweep-2026-11-27.md](vulnerability-sweep-2026-11-27.md).

The ReDoS sweep on 2026-11-27 mapped a regex denial-of-service checklist to the
installed rules. A constant pattern reaches E128013, E128014, and E128011
through the E128123 funnel, so only a non-constant pattern escapes. The sweep
added four pattern rows, one detection gap in E128014, and the three security
shapes the earlier checklist left open. The security batch shipped them as
E128264 to E128268 and widened E128013 and E128014. See
[redos-sweep-2026-11-27.md](redos-sweep-2026-11-27.md).

| Pattern | Source | Score | Status | Notes |
|---------|--------|-------|--------|-------|
| Sonar S6585 rejects a custom `DateTime` format literal that renders a culture-invariant ISO-8601 stamp | `session friction` | 2/5 | new | Frequency 1, gap 0, expressibility 1, value 0. S6585 already owns the shape, and the `s` standard specifier plus a literal `Z` produces the same text, so the workaround beats a suppression. Decide whether to keep the workaround or tune S6585. Seen in build-time-hotspots on 2026-11-26. |
| A whole-compilation duplicate code detector analyzer with an extract shared code fixer | `session idea` | 2/5 | new | Frequency 0, gap 1, expressibility 0, value 1. Roslyn shows an analyzer one declaration at a time. Duplicate detection needs a whole-compilation fragment registry. The fix needs a cross type extract method refactor. The 2026-11-26 duplication audit in plans/TECH_DEBT_AUDIT.md found the repeated shapes here are mostly conventional boilerplate, so the rule would be noisy. The alternative shape is an offline dup scan script in scripts/. |
| A `bool` guard predicate returns `true` from its `catch` block | `vuln checklist` | 3/5 | new | Frequency 0, gap 1, expressibility 1, value 1. The method name carries the guard contract, so the rule reads `Is`, `Has`, `Can`, `Allow`, `Validate`, and their siblings. No code fix, because the safe answer depends on the caller. |
| Type activated from a run-time name or bytes | `vuln checklist` | 3/5 | new | Frequency 0, gap 1, expressibility 1, value 1. Waits for a defect site. |
| An alternation inside a quantified group whose branches share a first character | `redos checklist` | 3/5 | new | Frequency 0, gap 1, expressibility 1, value 1. `^(a\|ab)+$` splits 2^n ways. Extends E128014 with the shipped `HasTopLevelAlternation` and `AtomsCanMatchSameText` helpers. |
| A repeated group whose last atom is optional | `redos checklist` | 3/5 | new | Frequency 0, gap 1, expressibility 1, value 1. `(\w+[-.]?)+` moves the iteration boundary. E128014 misses it because its first and last atoms do not overlap, so the fix belongs in that rule. |
| Three or more unbounded `.*` segments in one pattern | `redos checklist` | 3/5 | new | Frequency 0, gap 1, expressibility 1, value 1. Widens E128013 from one wildcard adjacency to a wildcard count. |
| A non-constant regex pattern with no timeout and no `NonBacktracking` | `redos checklist` | 3/5 | new | Frequency 0, gap 1, expressibility 1, value 1. MA0009 reads the constructor only and MA0110 needs a constant. `ContentFilter` passes a timeout, so the rule stays silent there. |
| A certificate chain whose `Build` result is ignored or whose policy drops a check | `redos checklist` | 3/5 | new | Frequency 0, gap 1, expressibility 1, value 1. E128156 owns the callback path only. |
| A decompression loop with no cumulative output bound | `redos checklist` | 3/5 | new | Frequency 0, gap 1, expressibility 1, value 1. E128227 bounds an HTTP body only. No site in the tree today. |
| A symmetric key set from a text encoding instead of a key derivation function | `redos checklist` | 3/5 | new | Frequency 0, gap 1, expressibility 1, value 1. The FIPS family covers key length and algorithm, never key derivation. |
| E128045 flags a single-file tool whose whole output is a JSON document | `session friction` | 2/5 | new | Frequency 1, gap 0, expressibility 1, value 0. A tool has no container and no terminal writer, and `ITerminalWriter` would drag `src/Pug.Rendering` into a process that prints one JSON body. The ask is a narrow exemption for a machine-readable tool, not a new rule. Seen in the file-based-apps probe on 2026-11-27. |

## Tier 2 Candidates

A Tier 2 shape clears the score bar and waits for a real defect in review. The
register table marks such a row with the `candidate` status. No row holds that
status today.

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

- [catalog.md](catalog.md): settled rules and the shipped release table
- [redos-sweep-2026-11-27.md](redos-sweep-2026-11-27.md): 2026-11-27 ReDoS read, whose five rules ship as E128264 to E128268
- [skipped-decisions.md](skipped-decisions.md): every declined shape and its reason
- [guidance-reads.md](guidance-reads.md): each read of the two guidance documents
- [net11-conversion-sweep.md](net11-conversion-sweep.md): open .NET 11 conversion shapes
- [slopwatch-sweep.md](slopwatch-sweep.md): 2026-11-18 slopwatch read, with one open and two Tier 2 shapes
- [async-sweep.md](async-sweep.md): closed async fire-and-forget sweep
- [unsafe-sweep.md](unsafe-sweep.md): 2026-11-25 unsafe best-practices read, with two open shapes and a defect list
- [pugworks-review-sweep.md](pugworks-review-sweep.md): 2026-11-27 pugworks read, with three new shapes and an E128033 defect
- [vulnerability-sweep-2026-11-27.md](vulnerability-sweep-2026-11-27.md): 2026-11-27 security checklist read, with three new rows and the owned-shape map
- [redos-sweep-2026-11-27.md](redos-sweep-2026-11-27.md): 2026-11-27 ReDoS read, with four pattern rows, one E128014 detection gap, and the open security shapes
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

### Captured: When a control is replaced with a different type, delete or retarget every style selector that named the old type (1/3): score pending
> **Key**: `when-a-control-is-replaced-with-a-different-type-delete-or-re`
> **Class**: `design`, **Lang**: `cs`, **File**: `(none)`
> **Pattern**: When a control is replaced with a different type, delete or retarget every style selector that named the old type
> **Why**: A selector on the removed type matches nothing after the swap, so its layout fix stops applying silently and no build error reports it
> **Source**: `correction 2026-11-23T16:25:33Z`, occurrence 1 of 3

### Captured: Scope a resource cap to the pipeline that needs it; a user-invoked path that shares the reader must not inherit the indexing cap (1/3): score pending
> **Key**: `scope-a-resource-cap-to-the-pipeline-that-needs-it-a-user-inv`
> **Class**: `design`, **Lang**: `cs`, **File**: `(none)`
> **Pattern**: Scope a resource cap to the pipeline that needs it; a user-invoked path that shares the reader must not inherit the indexing cap
> **Why**: A 600-page PDF converted to an empty file reported as Converted because ReadDocumentAsync reused the indexing read. Give the shared reader an explicit scope (extraction vs conversion) instead of one behavior for every caller.
> **Source**: `correction 2026-11-27T03:59:56Z`, occurrence 1 of 3

### Captured: A regex that scans untrusted input must bound backtracking: no nested quantifier, exclusive alternation branches, and a timeout or NonBacktracking (1/3): score pending
> **Key**: `a-regex-that-scans-untrusted-input-must-bound-backtracking-no`
> **Class**: `correctness`, **Lang**: `cs`, **File**: `(none)`
> **Pattern**: A regex that scans untrusted input must bound backtracking: no nested quantifier, exclusive alternation branches, and a timeout or NonBacktracking
> **Why**: A nested or ambiguous quantifier explores exponential splits on crafted input, so one request burns a worker thread. Rewrite the pattern, add an atomic group, or pass RegexOptions.NonBacktracking.
> **Source**: `correction 2026-11-27T15:49:31Z`, occurrence 1 of 3
