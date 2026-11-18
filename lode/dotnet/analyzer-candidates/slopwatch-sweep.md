# Slopwatch Detection Sweep
*Updated: 2026-11-18T13:37:01Z*

The `Aaronontheweb/dotnet-slopwatch` repository on branch `dev` was read on
2026-11-18. It is a command-line file scanner for "slop", which is code an
assistant produced to silence a failure instead of fixing it. It carries seven
detection rules and no Roslyn analyzer. Its shapes still seed candidates because
each one is decidable from a single file.

The detector source lives in `src/Slopwatch/Detection/Rules`. Rule SW021 and
the two test shapes ship as E128232, E128236, and E128237. Two project-file
shapes do not fit a Roslyn analyzer and sit in
[skipped-decisions.md](skipped-decisions.md). The settled owners sit in
[catalog.md](catalog.md), and the detection shapes that shipped live in
[rule-details-roadmap.md](../analyzers/rule-details-roadmap.md).

## How Slopwatch Works

Each rule parses one file in isolation. No compilation, no project references,
no semantic model. The scanner reports only added or modified lines when a diff
range is supplied.

Remediation has three mechanisms, and none of them is a code fix.

1. `[SlopwatchSuppress("SW001", "reason")]` needs a justification of 20
   characters or more.
2. A JSON baseline stores a hash per finding, so a later run reports only new
   slop. The hash mixes the rule ID, the file path, and the enclosing snippet.
3. Each finding carries a guidance string that names the intended repair.

```csharp
[Fact(Skip = "flaky on CI")]
[SlopwatchSuppress("SW001", "This test needs a Windows-only driver.")]
public void WritesTheRegistryValue() { }
```

E128 has no baseline mechanism. It uses `.globalconfig` severities and an
approval gate for each suppression. That difference is recorded below, and it is
not a candidate.

## Rule Verdicts

| Slopwatch rule | Shape | Verdict |
| -------------- | ----- | ------- |
| SW001 | Test disabled by `Skip`, `Ignore`, or `#if false` | Owned. E128168 owns an xUnit `Skip`, and E128237 owns the foreign forms. |
| SW002 | Warning suppression by pragma or `[SuppressMessage]` | Owned. Four pragma rules and E128173 cover it. |
| SW003 | Empty or comment-only `catch` block | Owned by S2486. E128172 covers a swallowed assertion. |
| SW004 | Arbitrary delay in test code | Owned. E128211 owns the loop, and E128236 owns the single delay. |
| SW005 | Project file slop | Not a Roslyn shape. Declined. |
| SW006 | CPM bypass by `VersionOverride` or inline `Version` | Not a Roslyn shape. Declined. |
| SW021 | Any use of `dynamic` | Shipped as E128232, score 4. |

## Shipped Shape: Dynamic Keyword Ban (E128232)

**Schema**: score 4 (frequency 1, coverage gap 1, expressibility 1, value 1).
The maintainer chose prevention over the real-defect bar.

An identifier named `dynamic` in a type position reports. The rule reads the
type position through the language grammar, then a semantic gate keeps a user
type named `dynamic` silent. E128138 covers a dynamic method built from raw IL,
which is a different shape. The detection shape lives in
[rule-details-roadmap.md](../analyzers/rule-details-roadmap.md).

## Shipped Shape: Unconditional Delay In A Test (E128236)

**Schema**: score 3.5 (frequency 1, coverage gap 1, expressibility 1, value 0.5).

A single fixed delay in a test method reports. E128211 owns the delayed loop, so
a delay inside any loop stays silent. The detection shape lives in
[rule-details-roadmap.md](../analyzers/rule-details-roadmap.md).

## Shipped Shape: Test Disabled by `[Ignore]` or `#if false` (E128237)

**Schema**: score 3 (frequency 0, coverage gap 1, expressibility 1, value 1).

An `[Ignore]` on a test method reports unless its reason matches
`e128_tracked_ref_pattern`. A test method inside a literal `#if false` region
reports at every site. E128168 stays the owner of the xUnit `Skip` argument. The
detection shape lives in
[rule-details-roadmap.md](../analyzers/rule-details-roadmap.md).

## Declined For The Analyzer

SW005 and SW006 read a `.csproj`, a `.props`, or a `.targets` file. A Roslyn
`DiagnosticAnalyzer` receives a `Compilation` and cannot open the project file
that produced it. E128062 reads an xUnit constant named `ReferenceAssemblies`,
not a project file, so it is no precedent.

A project-file guard belongs in a script or an MSBuild target. The repository
already routes dependency questions through `scripts/deps-graph.sh` and
`scripts/nuget-heat-map.sh`. Both shapes and the reason sit in
[skipped-decisions.md](skipped-decisions.md).

## Baseline And Diff Scope

Slopwatch ships a baseline file and diff-scoped reporting. Both are tool state,
not a code shape, so neither becomes an analyzer. A diff-scoped Roslyn report
would duplicate the IDE, and a baseline would let a defect stay in the tree
while the build passes.

## Related

- [index.md](index.md): the open register rows from this sweep
- [catalog.md](catalog.md): installed owners for SW002 and SW003
- [skipped-decisions.md](skipped-decisions.md): the project-file shapes and the baseline decision
- [guidance-reads.md](guidance-reads.md): the two published guidance documents read earlier
