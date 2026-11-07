# Build Performance

*Updated: 2026-11-07T18:08:42Z*

Analyzer execution dominates a local build. A warm full rebuild of
`E128.Reference.slnx` takes about 14 seconds with analyzers and about 3 seconds
with `-p:RunAnalyzers=false`, measured 2026-11-07. The ratio stays near 78 percent
because the compiler runs every analyzer package on every project.

## Where the Time Goes

`-p:ReportAnalyzer=true -v:d` prints a per-analyzer table for one compilation.
Three entries dominate `E128.Analyzers` and `E128.Analyzers.CodeFixers`:

| Analyzer | Cost on the analyzer projects |
| --- | --- |
| `SonarAnalyzer.CSharp` | about 18 seconds for each project |
| `Microsoft.CodeAnalysis.CSharp.CodeStyle` | about 10 seconds for each project |
| `CA1508` (`CSharpAvoidDeadConditionalCode`) | about 8 seconds and 5 seconds |

The report sums concurrent work, so the total exceeds the wall-clock build. Use
it to find an outlier, never as a budget.

## The Fast Path

`scripts/build.sh --fast` passes `-p:RunAnalyzers=false`. It skips every analyzer,
including the E128 rules. Use it for the inner edit loop. Run
`scripts/check.sh` before a commit, because that path keeps analyzers on.

The fast path is local only. It never changes a CI build, and it changes no
project property.

## Measure a Change

```bash
dotnet msbuild E128.Reference.slnx -t:Rebuild -v:q -nologo -clp:ErrorsOnly
```

Warm the compiler server first. The first build after a no-analyzer build pays
the assembly load and JIT cost for every analyzer, so it can take 60 seconds or
more. Repeat the timed build at least three times and compare the minimum. A
single run is not evidence.

For one project, add `-p:ReportAnalyzer=true -v:d` and read the
`Total analyzer execution time` block. A quiet build prints no table.

## Cost Levers

A package or a rule can be scoped to a project set. Each lever changes analysis
coverage, so the maintainer decides before it lands:

- Exclude `SonarAnalyzer.CSharp` from a project. The largest single cost.
- Turn off `EnforceCodeStyleInBuild` for a project. The IDE rules stop running.
- Scope a rule severity through `.globalconfig`. A suppression needs approval.

Do not put `RunAnalyzers=false` in `Directory.Build.props`. That removes the
gate from CI.

## Related

- [.NET Project Structure](project-structure.md): build properties and artifact layout
- [Analyzer Performance](analyzers/performance.md): per-analyzer cost rules
- [Analyzer Summary](analyzers/summary.md): rule and fix assembly layout
