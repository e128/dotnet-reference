# Async Fire-And-Forget Sweep
*Updated: 2026-11-11T16:29:38Z*

Closed 2026-11-11. No E128 rule ships from this sweep. A probe of every
installed rule set found an owner for each async shape, and the two shapes with
no owner fail the value test.

The shapes came from the rules that the `AsyncAwaitBestPractices` library
encodes by convention. That library ships no analyzers, so each candidate was an
inference from its documented guidance.

## Result

The probe measured the shapes in `.claude/tmp/async-probe/` and recorded each
firing rule id in
[redundant-rules.md](analyzers/redundant-rules.md#async-drop-family). Read that
table before you propose a dropped-task or `async`-lambda rule again.

| Candidate                              | Result                                     |
| -------------------------------------- | ------------------------------------------ |
| Discarded awaitable                    | MA0134 and VSTHRD110 own it                |
| `async` lambda to a void delegate      | AsyncFixer03, MA0147, VSTHRD101 own it      |
| `SuppressThrowing` await, no fault check | No owner. Rejected on value              |
| Dependency event subscription          | No owner. Rejected on false positives      |

## Why The Two Open Shapes Stay Rejected

`ConfigureAwaitOptions.SuppressThrowing` appears nowhere in `src/`. The flag is
the author writing down intent, so the review target is a deliberate choice
rather than a slip. A fault check is also correct to omit for a cleanup that
tolerates a lost exception. Revisit only after a real defect.

`EventSubscriptionLeakAnalyzer` already bails out unless the subscriber is
`IDisposable` and already collects dependency events. The only open case is a
non-disposable subscriber, which is the long-lived type that never needs to
unsubscribe. The rule would report normal code.

## Probe Method

One scratch project, one snippet per shape, one build. The project sits under
`.claude/tmp/async-probe/` and pins its own `global.json`, `Directory.Build.props`
and `.editorconfig` so the repository settings do not leak in. Run it with
`scripts/build.sh --project .claude/tmp/async-probe/probe.csproj --warnings`.

The local `.editorconfig` sets `root = true` to stop the repository style rules.
The repository `.globalconfig` still applies, and it raises every analyzer
diagnostic to `error`, so a fire shows as a build error.

## Related

- [analyzer-candidates.md](analyzer-candidates.md): the open candidate catalog
- [redundant-rules.md](analyzers/redundant-rules.md): the probe evidence
- [rule-details-reliability-concurrency.md](analyzers/rule-details-reliability-concurrency.md):
  the shipped async rules
