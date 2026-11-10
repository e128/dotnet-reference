# .NET 11 Upgrade: Tasks
*Created: 2026-11-07T04:25:42Z*

*Updated: 2026-11-10T20:42:20Z*

*Status: draft. Phase 0 not executed. The plan waits for the .NET 11 GA SDK.*

## Phase 0: Claim Verification and Baseline

**Complexity:** Simple

- [ ] Create the feature branch: `git checkout -b feature/net11-upgrade`
- [ ] Confirm the GA SDK is installed: `dotnet --list-sdks` reports an `11.0` band version
- [ ] For each row in § Claims Register, run the verification command, then record `confirmed`, `corrected`, or `refuted`. A refuted row blocks Phase 1
- [ ] Reconcile this plan against every `corrected` or `refuted` row
- [ ] Re-read the GA compatibility page, and add any missing item to § Phase 2 with a verdict slot
- [ ] Record the Roslyn version that the GA SDK ships, and compare it with the pinned band in `Directory.Packages.props`
- [ ] Record whether the GA runtime page still documents runtime async as opt-in, in context.md § Architectural Decisions
- [ ] Probe `union` and `closed` in a throwaway project under `.claude/tmp/`. Record whether `UnionAttribute` and `ClosedAttribute` need `EnablePreviewFeatures`
- [ ] Probe the constraint: write `static TOut Match<TUnion, TOut>(this TUnion value, Func<TUnion, TOut> match) where TUnion : union`, then a call site with a declared union and a call site with an `int`. Record which design in § Union Constraint Gate compiles
- [ ] Probe the pinned Roslyn band: compile a snippet that declares a union against `Microsoft.CodeAnalysis.CSharp` at the `Directory.Packages.props` pin. Record whether the parser accepts it, which decides the § Union Rule Reach Gate exits
- [ ] Record the highest assigned diagnostic ID with `scripts/analyzer-stats.sh --json`
- [ ] Run `scripts/runtime-matrix.sh --json` to record the SDK pin, the per-project TFM set, and the two Docker base images
- [ ] Run `scripts/internal/analyzer-release-check.sh --json` and record the result as the pre-upgrade baseline
- [ ] Run `scripts/check.sh --all --no-format --json` for one full-suite pass. Record the pass count, the fail count, and the warning set in context.md § Test Status
- [ ] Run `scripts/diagnostics.sh --group` and record the pre-existing diagnostic set
- [ ] Record the test baseline count in context.md § Test Status, with the command used
- [ ] Stage and commit: `scripts/internal/stage.sh --include-new`, then `scripts/internal/commit.sh "plan: verify net11 claims and record the baseline"`

## Acceptance Contract

**Complexity:** Simple

Workflow plan: the deliverables are config changes proven by commands, not new public API. No test stubs. Each behavior is verified by the named command or by code reading.

- [ ] Confirm every § Verifiable Behaviors row is testable by command or code reading
- [ ] Record one `- **AC-{n}:**` line per behavior under § Acceptance Evidence in context.md
- [ ] Record the ReadyToRun target that this machine produces on `net10.0`, so Phase 3 has a comparison value
- [ ] Confirm every task above is checked before Phase 1 starts. The repository gate covers plan closure only, so the contract closes by this list

## Phase 1: Analyzer Cliff Sweep

**Complexity:** Moderate

Raise the SDK band with the TFM still on `net10.0`, so every new diagnostic surfaces in isolation.

- [ ] Update `global.json` to the GA SDK version, and keep `rollForward` and `allowPrerelease` as they are
- [ ] Update the `README.md` badge, the README intro prose, the README summary bullet, and the `AGENTS.md` target list
- [ ] Run `scripts/runtime-matrix.sh --json` and confirm the SDK pin reads the GA band
- [ ] Run `scripts/build.sh --warnings` and record the diagnostic delta against the Phase 0 set
- [ ] Record the delta from the corrected `AnalysisLevel=latest-all` resolution separately, because the .NET 11 rules now apply where the `net10.0` rules did
- [ ] Sweep `goto`-based loop exits and boolean-flag loop exits, the shapes IDE0410 flags
- [ ] Adopt the C# 15 forms that need no runtime attribute where the sweep found the old shape: labeled `break` and `continue`, collection expression arguments, extension indexers
- [ ] Fix every newly active diagnostic at the source. No suppression without a written reason
- [ ] Confirm the two analyzer projects still target `netstandard2.0`, and no project gained a TFM override
- [ ] Run `scripts/test.sh --all` and confirm no regression against the baseline count
- [ ] Run `scripts/format.sh --changed` after the sweep
- [ ] Stage and commit: `scripts/internal/stage.sh --include-new`, then `scripts/internal/commit.sh "build: raise the SDK pin to the .NET 11 GA band"`

## Phase 2: Behavior Audits

**Complexity:** Moderate

Fix or prove every .NET 11 behavior change that alters a result without a TFM move. Each item gets a verdict row in context.md.

- [ ] Read the GA breaking-change list, and write one verdict row per item that touches `System.*`, ASP.NET Core, or `System.CommandLine`
- [ ] `Math.Round` and `MathF.Round`: list every call site, and confirm the new rounding leaves each one unchanged
- [ ] `DateOnly` parsing: find every `TryParse` and `Parse` call site, and confirm no catch block depends on a non-throwing `TryParse`
- [ ] `Nullable.GetUnderlyingType`: confirm no reflection path relies on the old behavior for a custom `Type` subclass
- [ ] ZIP CRC32 validation: confirm no `src/` path reads a ZIP stream, since only the analyzer tests build archives
- [ ] Bump `Microsoft.Extensions.*`, `Microsoft.AspNetCore.Mvc.Testing`, and `Microsoft.Extensions.Diagnostics.Testing` to the `net11.0` band in `Directory.Packages.props`
- [ ] Confirm the analyzer test project references none of those packages, so the bump leaves the `net10.0` leg alone
- [ ] Keep `Microsoft.CodeAnalysis.*` at the pinned `net10.0` band. Record the decision and the reason in context.md
- [ ] Run `scripts/dep-check.sh --outdated --json` and confirm no Roslyn entry demands a bump
- [ ] Run `scripts/test.sh --all` and confirm no regression
- [ ] Stage and commit: `scripts/internal/stage.sh --include-new`, then `scripts/internal/commit.sh "build: audit net11 behavior changes and bump the extension pins"`

## Phase 3: TFM Move and the net10 Leg

**Complexity:** Complex

The committed move, with runtime async still off. The `net10.0` leg lands in the same phase, so the reach proof exists before anything else changes.

- [ ] Set `TargetFrameworks` to `net10.0;net11.0` in `tests/E128.Analyzers.Tests/E128.Analyzers.Tests.csproj`
- [ ] Clear the inherited single TFM in the same property group. A project that sets both `TargetFramework` and `TargetFrameworks` fails to build
- [ ] Run `scripts/build.sh --project E128.Analyzers.Tests --verbose` and confirm both legs build
- [ ] Record the local suite wall-clock delta from the doubling in context.md. Add a framework flag to `scripts/test.sh` only when the delta exceeds the recorded budget
- [ ] Add a `net10-consumer` job to `.github/workflows/ci.yml`. It installs SDK `10.0.x`, writes a CI-local `global.json` that keeps `test.runner`, and runs `dotnet test tests/E128.Analyzers.Tests/E128.Analyzers.Tests.csproj -f net10.0`
- [ ] Confirm the job reports a nonzero test count, and fails the build on an analyzer load warning
- [ ] Set `TargetFramework` to `net11.0` in `Directory.Build.props`
- [ ] Update `Dockerfile` to the `11.0` base image bands, and keep the existing Debian tag suffix
- [ ] Run `scripts/runtime-matrix.sh --json` and confirm the pin, the TFM set, and the base images agree
- [ ] Fix the obsoletions that `scripts/build.sh --warnings` reports
- [ ] Run `dotnet pack src/E128.Analyzers/E128.Analyzers.csproj -c Release -o ./artifacts/nupkgs` and confirm two `analyzers/dotnet/cs` entries and the `lib/netstandard2.0/_._` placeholder
- [ ] Run `scripts/build.sh --warnings` and confirm the Release ReadyToRun compilation succeeds
- [ ] Record the ReadyToRun target Linux produced, and compare it with the Phase 0 value
- [ ] Run `scripts/podman.sh test` and confirm `GET /health` passes
- [ ] Confirm one async method still carries `[AsyncStateMachine]`, which proves nothing activated by default
- [ ] Record the switch-off baseline: run `dotnet-counters` against `System.Runtime` during a suite run, then write the allocation rate, the gen0 count, and the p99 latency to context.md § Test Status
- [ ] Run `scripts/test.sh --all` and confirm the suite passes at or above the baseline
- [ ] Stage and commit: `scripts/internal/stage.sh --include-new`, then `scripts/internal/commit.sh "build: target net11 as primary and keep the net10 analyzer leg"`

## Phase 4: Runtime Async Rollout

**Complexity:** Complex

Application projects only, one per step, leaf first. A step reaches the next project only when its own suite run is green.

- [ ] Confirm `src/E128.Reference.Core` is the leaf, since `Web` and `Cli` reference it. Record the order: Core, Web, Cli
- [ ] Confirm the switch works while `EnablePreviewFeatures` is false in Release, and record the answer in context.md
- [ ] Add `<Features>$(Features);runtime-async=on</Features>` to `src/E128.Reference.Core/E128.Reference.Core.csproj`
- [ ] Build Core, and confirm one of its async methods carries no `[AsyncStateMachine]` attribute
- [ ] Run `scripts/test.sh --all` and confirm the suite passes
- [ ] Repeat the previous three steps for `src/E128.Reference.Web`, then for `src/E128.Reference.Cli`
- [ ] Move the switch to a property group in `Directory.Build.props`, gated on `'$(TargetFramework)' == 'net11.0'` and `'$(IsRoslynComponent)' != 'true'`
- [ ] Delete every per-project `Features` line
- [ ] Confirm the switch carries no literal project name: read `Directory.Build.props` and both analyzer project files. Neither analyzer file names `Features`
- [ ] Run `scripts/test.sh --all` and confirm the suite passes against the shared-property end state
- [ ] Run the analyzer suite on the `net10.0` leg and confirm it still passes
- [ ] Re-check every `ValueTask` return against its reason, and record any that existed only to shorten a deep chain
- [ ] Confirm no project uses `[AsyncMethodBuilder]`, which would opt its methods out
- [ ] Stage and commit: `scripts/internal/stage.sh --include-new`, then `scripts/internal/commit.sh "perf: enable runtime async for the application projects"`

## Phase 5: Verification and Hardening

**Complexity:** Simple

- [ ] Run `scripts/check.sh --all --json` and confirm a clean result against the baseline
- [ ] Run `scripts/diagnostics.sh --group` and confirm zero diagnostics
- [ ] Compare the counters sample against the Phase 3 baseline, and record both in context.md
- [ ] Run the CLI and the web service locally, and confirm normal behavior
- [ ] Record the ReadyToRun CPU floor in the container notes and in `lode/dotnet-reference/dep-map.md`
- [ ] Run `scripts/internal/analyzer-release-check.sh --json`. Bump the package version with `scripts/internal/version-bump.sh E128.Analyzers` only when a rule changed
- [ ] Run `scripts/readme-table-diff.sh --analyzer` and fix any drift in `src/E128.Analyzers/README.md`
- [ ] Update `lode/dotnet/project-structure.md` and `lode/dotnet-reference/dep-map.md` to the current TFM, SDK pin, and base images
- [ ] Update `lode/summary.md` when it names the target framework
- [ ] Add the § Roadmap Handoff rows to `plans/roadmap.md`
- [ ] Run `scripts/lode-check.sh --changed` and `scripts/lode-ts.sh --changed`
- [ ] Stage and commit: `scripts/internal/stage.sh --include-new`, then `scripts/internal/commit.sh "docs: record the net11 upgrade state"`

## Phase 6: Union Result Rewrite

**Complexity:** Complex

The first union in the solution. Every service and repository returns it, so the shape reaches the call sites before any rule is written.

- [ ] Declare the result union in `src/E128.Reference.Core`. Use the Case names the Phase 0 probe proved
- [ ] Declare the generic helper in the same file, with the constraint design § Union Constraint Gate selected
- [ ] Write one negative snippet or test that passes a non-union type argument. Record the compiler error, which proves Design 1 holds
- [ ] Add the `Match` and `Map` call sites the service needs. Do not add a helper that no call site uses
- [ ] Rewrite `IGreetingService` and `GreetingService` so an expected failure returns the union instead of a `bool`, a nullable, or an exception
- [ ] Rewrite `IGreetingRepository` and `InMemoryGreetingRepository` the same way
- [ ] Add one test per case, and one test that proves the composed path returns the value the exception path returned
- [ ] Record every replaced shape in context.md § Current State. Phase 7 ships a rule only for a shape that appears there
- [ ] Run `scripts/test.sh --all` and confirm the suite passes at or above the baseline
- [ ] Run `scripts/format.sh --changed`, then stage and commit: `scripts/internal/stage.sh --include-new`, then `scripts/internal/commit.sh "refactor: return a union result from the core services"`

## Phase 7: Guidance Rule Family

**Complexity:** Complex

One rule per shape Phase 6 recorded. The rule family is the part a developer or an agent meets before the rewrite.

- [ ] Read the replaced-shape record in context.md. Drop every candidate that appears once and cannot recur
- [ ] For each surviving candidate, run § Union Rule Reach Gate before writing the rule. A rule that needs a C# 15 Roslyn API takes exit 2 or exit 3
- [ ] Write each analyzer under `src/E128.Analyzers/Design/`. Read the union by syntax node name and by metadata name, never by a typed C# 15 node
- [ ] Gate each rule on the compilation language version, so it reports nothing on a `net10.0` host
- [ ] Register each rule in `src/E128.Analyzers/AnalyzerReleases.Unshipped.md` with the category `E128-Design`, and continue the ID sequence from CL-35
- [ ] Add each rule to the `src/E128.Analyzers/README.md` table and its prose section, then run `scripts/readme-table-diff.sh --analyzer`
- [ ] Write one snippet test per rule. Declare `UnionAttribute` and `IUnion` inside the snippet, so the test runs at the pinned band
- [ ] Exclude every C# 15 snippet test from the `net10.0` leg, and confirm the leg still reports a nonzero count
- [ ] Add a code fix only where the rewrite is mechanical. Record the reason where it is not
- [ ] Run `scripts/test.sh --all` on both legs, then `scripts/format.sh --changed`
- [ ] Bump the package with `scripts/internal/version-bump.sh E128.Analyzers`, then run `scripts/internal/analyzer-release-check.sh --json`
- [ ] Stage and commit: `scripts/internal/stage.sh --include-new`, then `scripts/internal/commit.sh "feat(analyzers): guide union results over boolean and exception shapes"`

## Phase 8: Code Review Gate

**Complexity:** Simple

- [ ] Run the `mr-sparkle-dotnet` skill against `feature/net11-upgrade`
- [ ] Triage every finding: fix it, or record why it stands as one row in context.md § Architectural Decisions
- [ ] For every file a fix touched, re-run its targeted tests with `scripts/test.sh ClassName` or `scripts/test.sh --all`
- [ ] Run `scripts/format.sh --changed` after the fixes land
- [ ] Stage and commit: `scripts/internal/stage.sh --include-new`, then `scripts/internal/commit.sh "review: apply the net11 upgrade findings"`

## Phase 9: Algorithmic Complexity Audit

**Complexity:** Moderate

The analyzer cliff sweep rewrites loop exits, so this phase judges the loops it wrote. A `goto`-free loop exit is a new shape, not a new cost, until someone proves otherwise.

- [ ] List every file the plan changed: `scripts/diff.sh --commits 1`
- [ ] Read each loop and recursive call in those files
- [ ] Judge each one against the input size and the output size. A nested scan that reads a dictionary it should have built is the shape to catch
- [ ] Replace anything worse than linear in the input plus the output with a better data structure or algorithm. A loop moved into a helper to satisfy an analyzer stays in place, because that hides the cost
- [ ] Record the verdict per file in context.md: the loop, its cost, and whether it changed
- [ ] Run `scripts/check.sh --all --json` and confirm a clean result
- [ ] Stage and commit: `scripts/internal/stage.sh --include-new`, then `scripts/internal/commit.sh "review: audit the loops the sweep rewrote"`

## Pre-Commit Verification

- [ ] Short-lived claim sweep: CL-1, CL-6, CL-11, CL-12, CL-14, CL-17, CL-18, CL-19, CL-22, and CL-24 were rewritten or retired by the phase that changed the behavior
- [ ] Union claim sweep: CL-31, CL-32, CL-34, and CL-35 carry the Phase 0 answer. CL-33 was rewritten by Phase 6
- [ ] Reconcile the file counts: compare `scripts/status.sh` against the changes recorded in context.md
- [ ] Run `scripts/format.sh --changed`
- [ ] Run `scripts/lint-shell.sh` when any `.sh` file changed
- [ ] Run `scripts/lode-check.sh --changed`
- [ ] Run `scripts/assert.sh --build-pass`
- [ ] Run `scripts/check.sh --all --json`
- [ ] Confirm no analyzer suppression was added without written approval
- [ ] Confirm `scripts/help.sh` still lists every script the plan references

## Phase 10: Retrospective

- [ ] Record what the upgrade cost in context.md § Current State
- [ ] Close the plan: `scripts/internal/plan-close.sh --plan net11-upgrade`

## Discovered During Implementation

Add new work here as `- [ ] Discovered: <finding>`. `scripts/internal/plan-close.sh` skips a task line that contains the word `Discovered`, so an unfinished find does not block the close.
