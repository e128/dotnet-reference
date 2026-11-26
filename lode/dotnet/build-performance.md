# Build Performance
*Updated: 2026-11-26T12:36:14Z*

Analyzer execution dominates a local build. As of 2026-11-07, a warm full rebuild
of `E128.Reference.slnx` took about 14 seconds with analyzers and about 3 seconds
with `-p:RunAnalyzers=false`. The compiler runs every analyzer package on every
project. Measure again before you rely on these figures.

## Where the Time Goes

`-p:ReportAnalyzer=true -v:d` prints a per-analyzer table for one compilation.
Three entries dominated `E128.Analyzers` and `E128.Analyzers.CodeFixers` as of 2026-11-07:

| Analyzer | Cost on the analyzer projects |
| --- | --- |
| `SonarAnalyzer.CSharp` | about 18 seconds for each project |
| `Microsoft.CodeAnalysis.CSharp.CodeStyle` | about 10 seconds for each project |
| `CA1508` (`CSharpAvoidDeadConditionalCode`) | about 8 seconds and 5 seconds |

The report sums concurrent work, so the total exceeds the wall-clock build. Use
it to find an outlier, never as a budget.

## Machine Load

Measure on an idle machine. A loaded machine inflates every analyzer time, and the inflation is not uniform.
Check `uptime` before you time a build. A load average above the core count makes the result useless.
In one investigation, a rebuild took about 6 seconds at load 19 and about 26 seconds at load 44.
A concurrent IDE backend, a leftover build, or another agent session adds this cost.
Stop the competing work before you compare two builds.

The third-party analyzers dominate a consumer project. SonarAnalyzer.CSharp and the SDK .NET analyzers
take most of the analyzer time. The E128 rules take a small share, and no single E128 rule is an outlier.
Read the per-rule rows before you blame an E128 change.

A `Rebuild` of a leaf project rebuilds its whole dependency chain. In pugworks, a rebuild of the
GuidGenerator executable also rebuilds Pug.Core, Pug.Rendering, and Pug.Rendering.XenoAtom.
Measure an incremental build for the inner edit loop.

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
