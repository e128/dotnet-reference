# False-Positive Taxonomy

A dependency with zero `using` directives is **not** automatically unused. These
categories look dead to a naive scan but are intentional. Check every candidate against
this list before flagging.

## Central packages (Directory.Packages.props)

| Pattern                                   | Why it has no direct PackageReference                                  | Verdict |
|-------------------------------------------|------------------------------------------------------------------------|---------|
| Transitive pin (comment-marked)           | Pins the version of a package pulled in transitively, to control the restore graph. `CentralPackageTransitivePinningEnabled=true` makes version-only entries legitimate. | Keep    |
| Appears in `dotnet list package --include-transitive` | Same — it's a real transitive dependency being version-pinned.         | Keep    |
| Analyzer / Roslyn version pin             | The `PackageReference` lives in `Directory.Build.props`, not a csproj. | Keep    |
| Truly absent (no ref, not transitive, no comment) | Genuinely dead version entry.                                          | **Flag** |

## PackageReference — skip list (never flag as unused PackageReference)

These never surface as `using` directives, so "no usage" tells you nothing:

- **Analyzers / source generators** — `AsyncFixer`, `Meziantou.Analyzer`,
  `Roslynator.*`, `SharpSource`, `SonarAnalyzer.CSharp`,
  `Microsoft.CodeAnalysis.*.CodeStyle`, `Microsoft.VisualStudio.Threading.Analyzers`,
  `Microsoft.CodeAnalysis.PublicApiAnalyzers`, `PolySharp`. Identified by
  `PrivateAssets="all"` and/or `IncludeAssets` restricted to analyzers/build.
- **Test SDK / runner / extensions** — `xunit.*`, `Microsoft.Testing.Extensions.*`,
  `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.Extensions.Diagnostics.Testing`. Wired
  by the test host, not by `using`.
- **Analyzer-testing harnesses** — `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing`,
  `Microsoft.CodeAnalysis.CSharp.CodeFix.Testing` (used via base classes, often aliased).
- **Runtime-only / DI glue** — `Microsoft.Extensions.DependencyInjection`,
  `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Logging` and abstractions. Often
  consumed only through generic-host wiring or attributes, with minimal `using`.
- **Build/MSBuild-only** — anything with `IncludeAssets="build"` or
  `PrivateAssets="all"` whose purpose is build-time only.

For a non-skip package, confirm usage by the package's **actual root namespace**, not its
package id — they often differ (e.g. package `System.CommandLine` → namespace
`System.CommandLine`; but many packages diverge). When the namespace is unknown, treat a
"no usage" result as `[LOW]` (verify), never `[HIGH]`.

## ProjectReference — intentional references with no symbol usage

- `OutputItemType="Analyzer"` — the referenced project is an analyzer shipped into the
  consumer; no type usage expected.
- `ReferenceOutputAssembly="false"` — referenced purely for build ordering or to bundle
  content; no assembly reference at all.
- A reference kept to force build order of a tool/generator project.

Check the `ProjectReference` element's attributes before flagging. Only a plain
`<ProjectReference Include="..." />` with no usage anywhere is a real candidate.

## Decision rule

When a candidate's status is ambiguous after checking the above, **downgrade and ask** —
the cost of removing a needed dependency (broken restore/build, or worse, a silently
dropped analyzer) far exceeds keeping one dead line. The verification build in Phase 5 is
the final backstop, not the first line of defense.
