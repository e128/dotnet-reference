# .NET 11 Upgrade: Context
*Created: 2026-11-07T04:25:42Z*

*Updated: 2026-11-10T20:42:20Z*

*Status: draft. Phase 0 has not run. The .NET 11 SDK is not installed on this machine.*

## Intent

Move the reference application from `net10.0` to `net11.0` as the primary target.
Keep the analyzer assemblies on `netstandard2.0`, and keep every `net10.0`
consumer of the `E128.Analyzers` package working.

The two goals pull in opposite directions on one axis. The application moves
forward. The published package must not lose reach.

The plan carries a second objective. The application adopts a union result for
expected failure, and a generic helper composes it. An `E128.Analyzers` rule
family then names the shapes the rewrite replaced. The rule family is the
part that reaches a developer or an agent before the rewrite, because a
diagnostic and a code fix survive where a design note does not.

The design rule is that the correct call is the easiest call. The helper takes
a constraint that admits a union and no other type, so the compiler enforces
the shape and the analyzer explains the alternative.

## The Two Targets

| Target | What moves | What stays |
|--------|-----------|------------|
| Primary framework | `TargetFramework` in `Directory.Build.props` becomes `net11.0` | Nothing |
| Analyzer reach | Nothing | Analyzer assemblies stay `netstandard2.0`, and the Roslyn package pin stays in the `net10.0` host band |

The published package supports a consumer on `net10.0` when all three
conditions hold:

1. The analyzer assemblies target `netstandard2.0`.
2. The analyzer assemblies build against a Roslyn API band no newer than the
   `net10.0` SDK compiler provides.
3. The analyzer tests pass under the `net10.0` SDK compiler.

Condition 2 is the one an upgrade breaks by accident. A routine
`Microsoft.CodeAnalysis.CSharp` bump to the `net11.0` band raises the minimum
host and silently drops `net10.0` consumers. Roslyn analyzers load forward, so
a `net11.0` consumer still loads a `net10.0`-band analyzer. The reverse is not
true.

## Important Files

| File | Why It Matters |
|------|----------------|
| `global.json` | SDK pin, `rollForward`, and the MTP runner selection |
| `Directory.Build.props` | Default TFM, `LangVersion`, `AnalysisLevel`, Release `ReadyToRun`, the preview-feature switch |
| `Directory.Build.targets` | Adds the analyzer `ProjectReference` as an `Analyzer` to every non-Roslyn project, so the analyzer builds under the net10 leg too |
| `Directory.Packages.props` | Central package versions, including the Roslyn pin that sets the analyzer host floor |
| `.globalconfig` | Blanket `dotnet_analyzer_diagnostic.severity = error`, which turns every new SDK rule into a build failure |
| `tests/.globalconfig` | Test-only severity overrides at `global_level = 101` |
| `src/E128.Analyzers/E128.Analyzers.csproj` | Analyzer TFM, packaging layout, `_._` placeholder for `lib/netstandard2.0` |
| `src/E128.Analyzers.CodeFixers/E128.Analyzers.CodeFixers.csproj` | Second `netstandard2.0` assembly, `Microsoft.CodeAnalysis.CSharp.Workspaces` consumer |
| `tests/E128.Analyzers.Tests/E128.Analyzers.Tests.csproj` | The project that becomes multi-targeting |
| `tests/E128.Analyzers.Tests/xunit.runner.json` | Parallelism cap, unchanged by the move |
| `Dockerfile` | `sdk:10.0-resolute` and `aspnet:10.0-resolute` base images |
| `.github/workflows/ci.yml` | Installs `dotnet-version: 10.0.x` and calls raw `dotnet` |
| `.github/workflows/publish.yml` | Packs the analyzer package on SDK `10.0.x` |
| `README.md` | `.NET 10` badge, intro prose, and tooling bullet |
| `AGENTS.md` | States the supported targets in the .NET Development section |
| `src/E128.Analyzers/README.md` | Rule table and the `e128_minimum_framework_version` default of 100 |
| `lode/dotnet/project-structure.md` | Describes the solution as a .NET 10 solution |
| `lode/dotnet-reference/dep-map.md` | Records the SDK pins and the container images |
| `src/E128.Reference.Core/Services` | `GreetingService` returns the first union result, and it sets the helper shape |
| `src/E128.Reference.Core/Repositories` | The repository returns the same union, so the shape reaches a second caller |
| `src/E128.Analyzers/Design` | Home of the guidance rule family |
| `tests/E128.Analyzers.Tests` | The C# 15 snippet tests need exclusion from the `net10.0` leg |

## Architectural Decisions

| Decision | Rationale | Date |
|----------|-----------|------|
| Keep `Directory.Build.props` as the single TFM home | Every project inherits it, and no project sets its own TFM today | 2026-11-07 |
| Never move the analyzer assemblies off `netstandard2.0` | `netstandard2.0` is the mechanism that keeps a `net10.0` consumer working | 2026-11-07 |
| Pin the Roslyn packages to the oldest supported host band | A newer Roslyn band raises the minimum host. The `net10.0` SDK compiler sets that floor | 2026-11-07 |
| Prove `net10.0` support by running the analyzer suite under the `net10.0` SDK | A pack-only check proves the assembly builds. It does not prove the compiler loads it | 2026-11-07 |
| Multi-target the analyzer test project as `net10.0;net11.0` | One project proves both bands. A second compatibility project would duplicate the whole suite | 2026-11-07 |
| Run the `net10.0` leg on SDK 10 only, against the test project | The solution holds `net11.0` projects that SDK 10 cannot build. The test project builds alone | 2026-11-07 |
| Wait for GA, not RC | The compatibility page changes between RC and GA, and a preview SDK pin never reaches `main` | 2026-11-07 |
| Land the TFM with runtime async off | A TFM bump and a preview switch in one build leave two candidate causes for every regression | 2026-11-07 |
| Never apply `runtime-async=on` to a Roslyn component | The analyzer assemblies are `netstandard2.0` and load into a `net10.0` host. The feature needs the `net11.0` runtime | 2026-11-07 |
| Never apply `runtime-async=on` to the `net10.0` leg | The `net10.0` runtime has no runtime async support, so the leg would fail or change behavior | 2026-11-07 |
| Gate the shared runtime-async property group on TFM and component role | The pugworks end state is one unconditional property. That shape breaks the analyzer reach in this repo | 2026-11-07 |
| Keep the explicit SDK pin although `rollForward` is `latestPatch` | The pin keeps the local toolchain and CI on one version | 2026-11-07 |
| Add no package-compatibility script | The package set is small. `scripts/build.sh` reports `NU1202` on the first restore after the TFM edit, and one revert undoes it | 2026-11-07 |
| Keep analyzer test snippets on `ReferenceAssemblies.Net.Net100` | The snippets model the `net10.0` consumer. A `Net110` snippet belongs only to a rule that reads a `net11.0` API | 2026-11-07 |
| Treat the ReadyToRun target as a product decision | Release sets `ReadyToRun=true`, so the published container image gains a CPU floor | 2026-11-07 |
| Drop the AOT phase items | No project sets `PublishAot`, so there is no AOT output to verify | 2026-11-07 |
| Drop the allocation benchmark | The repository holds no benchmark project, so there is no baseline to compare | 2026-11-07 |
| Adopt the C# 15 forms that need no runtime attribute first | `with(...)`, extension indexers, and labeled jumps are compiler-only. `union` and `closed` ship as preview scaffolding | 2026-11-07 |
| Adopt `union` in the same plan, gated on the Phase 0 probe | The union result is the goal of the union work, not a later library decision. Phase 6 waits for the probe answer | 2026-11-10 |
| Take the first § Union Constraint Gate design that compiles | The constraint carries the teaching load. A union keyword constraint is the target, an interface constraint is the fallback, and per-type overloads are the floor | 2026-11-10 |
| Give the result union one home in `Reference.Core` | One result type per application keeps the helper generic and the call site uniform | 2026-11-10 |
| Ship a rule only for a shape the rewrite found | The rewrite is the evidence. A rule for a shape that never occurs adds cost and no guidance | 2026-11-10 |
| Write union rules without a C# 15 Roslyn API | The published package must keep its `net10.0` host reach. Syntax node names and metadata names compile at the pinned band | 2026-11-10 |
| Declare the union attributes inside each snippet test | The snippet then runs at the pinned Roslyn band without a `net11.0` reference assembly set | 2026-11-10 |
| Exclude C# 15 snippet tests from the `net10.0` leg | A `net10.0` compiler cannot parse the snippet. The leg proves reach, not union behavior | 2026-11-10 |
| Defer the web and CLI entry points | Each entry point maps a result case to a status code or an exit code, and each mapping needs its own tests | 2026-11-10 |
| Send every other new library API to the roadmap | The plan moves the TFM and adopts the result union. Each other API candidate is a feature with its own tests | 2026-11-07 |

## Source Material

The plan adapts `~/repos/e128/pugworks/plans/net11-upgrade`, read on 2026-11-07.
The pugworks plan targets a single application solution with a nushell gate
set. This repository adds a published analyzer package, so the analyzer reach
constraint and its proof are new work here.

`https://www.meziantou.net/migrating-to-runtime-async.htm` supplies the runtime
async migration order:

- Enable the switch per assembly with `<Features>$(Features);runtime-async=on</Features>`.
- Opt out per project with `<UseRuntimeAsync>false</UseRuntimeAsync>`.
- Compile assemblies either way and interop works, because both return `Task`
  and `ValueTask`.
- The absence of `[AsyncStateMachine]` on a method detects the switch.
- Behavior is unchanged: `AsyncLocal<T>` flows, cancellation reports
  `TaskStatus.Canceled`, and async streams behave the same.
- Stack traces shorten, and allocation type names change.
- No suspension inside `catch`, `filter`, `finally`, or `fault`. No `ref`
  locals, ref-like structs, pinned locals, `localloc`, or `tail` calls across a
  suspension point.
- The guide's order: plain upgrade, baseline, one leaf project, expand.

.NET 11 documentation to re-read at GA:

- [What's new in .NET 11 runtime](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime): runtime async status, the `x86-64-v2` baseline, and the `x86-64-v3` ReadyToRun target.
- [What's new in the SDK for .NET 11](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/sdk): the corrected `AnalysisLevel` resolution and the `dotnet test` run-level policy options.
- [What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15): the five new language features and the memory-safety preview.
- [Union types proposal](https://github.com/dotnet/csharplang/blob/main/proposals/union-types.md): the declaration form, `UnionAttribute`, `IUnion`, and whether the type is usable as a constraint.
- [Closed hierarchy proposal](https://github.com/dotnet/csharplang/blob/main/proposals/closed-hierarchies.md): the `closed` modifier and `ClosedAttribute`.
- [dotnet/core release notes 11.0](https://github.com/dotnet/core/blob/main/release-notes/11.0/README.md): the newest public build.
- [.NET Conf 2026](https://devblogs.microsoft.com/dotnet/get-ready-for-dotnet-conf-2026/): the GA date, 2026-11-10.

## Acceptance Evidence

Each row is the acceptance criterion for one `## Verifiable Behaviors` row in
[net11-upgrade-plan.md](net11-upgrade-plan.md).

- **AC-1:** The solution builds on `net11.0` with zero errors and zero new warnings, compared against the Phase 0 warning set: `scripts/build.sh --warnings`
- **AC-2:** The full suite passes at or above the baseline count: `scripts/test.sh --all`
- **AC-3:** The analyzer suite passes on the `net10.0` leg under SDK 10: the net10 CI job in Phase 3
- **AC-4:** The analyzer assemblies still target `netstandard2.0`: `rg -n "TargetFramework" src/E128.Analyzers src/E128.Analyzers.CodeFixers`
- **AC-5:** The Roslyn package pin still sits in the `net10.0` host band: `scripts/dep-check.sh --outdated` shows no forced Roslyn bump
- **AC-6:** Release ReadyToRun compilation succeeds, and the target per platform is recorded: Release build output in Phase 3
- **AC-7:** The container image builds and serves `GET /health`: `scripts/podman.sh`
- **AC-8:** `README.md` and `AGENTS.md` state `net11.0` as the primary target: code reading in Phase 5
- **AC-9:** The switch-off `net11.0` baseline is recorded before any project turns runtime async on
- **AC-10:** No async method in a rollout project carries `[AsyncStateMachine]` after the switch
- **AC-11:** The analyzer suite still passes on the `net10.0` leg after the full runtime-async rollout
- **AC-12:** The lode records the post-upgrade state, so no lode file describes `net10.0` as current
- **AC-13:** `Reference.Core` exposes one result union, and every service and repository returns it: code reading plus one test per case
- **AC-14:** One generic helper composes every result union, and the selected design matches the Phase 0 answer: the probe output in this file
- **AC-15:** The build rejects a non-union type argument under Design 1: one negative snippet, with the compiler error recorded
- **AC-16:** Every guidance rule compiles at the pinned Roslyn band and reports nothing on a `net10.0` compilation: `scripts/build.sh --project E128.Analyzers` and the analyzer suite
- **AC-17:** The `net10.0` leg excludes every C# 15 snippet test and still reports a nonzero count: the net10 CI job in Phase 3
- **AC-18:** Each shipped rule names a shape the Phase 6 rewrite replaced: the record in § Current State
- **AC-19:** The full suite passes at or above the baseline count after the rewrite and after each rule lands: `scripts/test.sh --all`

## Test Status

- **Baseline count:** TBD (Phase 0)
- **Current count:** TBD
- **Last run:** TBD
- **Runtime async baseline:** TBD (Phase 3, sample taken with the switch off)
- **Status:** Not started

## Current State

- **Last updated:** 2026-11-07T04:25:42Z
- **Status:** Draft plan. Phase 0 not executed.
- **Completed:** Plan, context, and tasks written from the pugworks plan. Every Claims Register row probed against this repository on 2026-11-07. The union result work and the guidance rule family added on 2026-11-10 as Phases 6 and 7.
- **Tried & failed:** N/A
- **Blockers:** No .NET 11 SDK on this machine (`dotnet --list-sdks` reports only `10.0.403`). GA ships at .NET Conf on 2026-11-10.
- **Next steps:** Run Phase 0 at GA, including the three union probes that select the constraint design. Then Phase 1.
