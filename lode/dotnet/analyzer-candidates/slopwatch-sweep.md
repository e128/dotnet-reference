# Slopwatch Detection Sweep
*Updated: 2026-11-18T12:18:45Z*

The `Aaronontheweb/dotnet-slopwatch` repository on branch `dev` was read on
2026-11-18. It is a command-line file scanner for "slop", which is code an
assistant produced to silence a failure instead of fixing it. It carries seven
detection rules and no Roslyn analyzer. Its shapes still seed candidates because
each one is decidable from a single file.

The detector source lives in `src/Slopwatch/Detection/Rules`. Rule SW021 is the
strongest shape. Two test shapes clear the bar and wait. Two project-file shapes
do not fit a Roslyn analyzer and sit in
[skipped-decisions.md](skipped-decisions.md). The settled owners sit in
[catalog.md](catalog.md).

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
| SW001 | Test disabled by `Skip`, `Ignore`, or `#if false` | Partly owned. E128168 owns an xUnit `Skip`. Two gaps remain. |
| SW002 | Warning suppression by pragma or `[SuppressMessage]` | Owned. Four pragma rules and E128173 cover it. |
| SW003 | Empty or comment-only `catch` block | Owned by S2486. E128172 covers a swallowed assertion. |
| SW004 | Arbitrary delay in test code | Partly owned. E128211 owns a delayed loop with no token. |
| SW005 | Project file slop | Not a Roslyn shape. Declined. |
| SW006 | CPM bypass by `VersionOverride` or inline `Version` | Not a Roslyn shape. Declined. |
| SW021 | Any use of `dynamic` | Open candidate, score 4. |

## Open Shape: Dynamic Keyword Ban

**Schema**: score 4 (frequency 1, coverage gap 1, expressibility 1, value 1).
Status `new`, and this shape clears the recommendation bar.

**Pattern**: an identifier named `dynamic` that occupies a type position.

```csharp
// Report
dynamic payload = Parse(body);
return payload.Value;
```

**Why not covered**: no installed rule set bans the keyword. E128138 covers a
dynamic method built from raw IL, which is a different shape. The repository has
zero uses of the keyword in `src/` (as of 2026-11-18), so the rule ships clean.

**Detection shape**: `SyntaxFacts.IsInNamespaceOrTypeContext` answers the type
position question. Exclude an attribute argument and a using directive with no
alias. Slopwatch tried a parent-kind allow-list first and found it incomplete,
because it missed lambda return types and LINQ clause types. The `SyntaxFacts`
call comes from the language grammar, and it needs no list.

```csharp
private static bool IsTypePosition(IdentifierNameSyntax id) =>
    id.Parent is not (QualifiedNameSyntax or AliasQualifiedNameSyntax)
    && id.Parent is not UsingDirectiveSyntax { Alias: null }
    && SyntaxFacts.IsInNamespaceOrTypeContext(id);
```

**Scope**: a semantic model lets E128 skip a user-defined type that is literally
named `dynamic`. Slopwatch cannot do that, because it holds no compilation. E128
also takes the compiler answer for a new syntax form.

**Fix**: none. A replacement type carries application intent, so no code fix
applies.

**Noise**: low. A ban reports a whole language feature, never a near miss.

## Tier 2 Shape: Unconditional Delay In A Test

**Schema**: score 3.5 (frequency 1, coverage gap 1, expressibility 1, value 0.5).

**Pattern**: a test method calls `Thread.Sleep`, `SpinWait`, or `Task.Delay`
without a cancellation token, outside a loop.

```csharp
// Report
await Task.Delay(500);
Assert.Equal(3, counter.Value);
```

**Why not covered**: E128211 reports a delayed loop with no token. It excludes
`Thread.Sleep`, and it does not read a single delay. E128168 covers a skip
reason, not a timing wait.

**Scope**: report a constant delay and no token. A delay that carries a token
stays silent, because the caller can cancel it. A test that asserts timeout
behavior stays silent, and the Tier 2 trigger decides when that rule lands.

**Noise**: medium. A timeout test needs a delay, so the rule needs a narrow
signal before it can ship.

## Tier 2 Shape: Test Disabled by `[Ignore]` or `#if false`

**Schema**: score 3 (frequency 0, coverage gap 1, expressibility 1, value 1).

**Pattern**: a method that carries a test attribute and an `[Ignore]` attribute,
or a test method inside an `#if false` block.

**Why not covered**: E128168 reads the `Skip` argument on an xUnit `[Fact]` or
`[Theory]` only. NUnit and MSTest disable a test with `[Ignore]`, and neither
attribute produces a report today.

**Scope**: reuse the E128168 `e128_tracked_ref_pattern` option on an
`[Ignore("reason")]` argument, so a tracked skip stays silent. A method inside
`#if false` carries no reason, so report it at every site. Slopwatch reads
`#else` and `#elif` before it reports, and the same care prevents a false report
in an active branch.

**Noise**: low in this repository, because xUnit is the only test framework in
use. The shape is common in LLM-generated code, and that is the value.

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
