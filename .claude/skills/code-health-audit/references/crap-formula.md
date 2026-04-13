---
name: crap-analysis
description: Analyze code coverage and CRAP (Change Risk Anti-Patterns) scores to identify high-risk code. Use OpenCover + ReportGenerator for Risk Hotspots. Covers the correct CRAP formula, C#-specific CC counting (async/await IL inflation, LINQ, null-conditional), manual estimation fallback, and project constraints.
invocable: true
---

# CRAP Score Analysis

## When to Use This Skill

- Evaluating code quality and test coverage before changes
- Identifying high-risk code that needs refactoring or testing
- Setting up or running coverage collection
- Prioritizing which code to test based on risk

---

## Project Notes (Read First)

This project uses `dotnet-coverage collect` (not Coverlet). Two limitations apply:

1. **No automated CRAP scores.** Cobertura format (produced by `dotnet-coverage collect`) does not include cyclomatic complexity. OpenCover format (Coverlet) is required for ReportGenerator Risk Hotspots with CRAP scores. Adding Coverlet to test projects would enable OpenCover output.

2. **Profiler fails on macOS ARM64 + MTP.** `dotnet-coverage collect` cannot attach its profiler to MTP-based test processes on macOS ARM64. The profiler initializes successfully on Linux CI. See [Code Coverage lode](../../lode/dotnet/code-coverage.md#known-limitations) for details.

### Manual CRAP Analysis (Current Fallback)

When tooling is unavailable, estimate CRAP manually:

1. **Count decision points** per method: +1 for each `if`, `else if`, `case`, `for`, `foreach`,
   `while`, `catch`, `?:`, `??`, `?.`, `&&`, `||`, pattern `when` guard. Starting value is 1.
2. **Estimate branch coverage** (not line coverage) from the test suite.
3. Apply the correct formula: **`CRAP = CC² × (1 − cov/100)³ + CC`**
4. **Highest ROI tests:** methods in the Moderate band (CRAP 5–30) with 0% coverage and CC 4–7.
   Pure functions (no DI, no I/O) are easiest to add — make them `internal` and use IVT.
   Methods in the High band (CRAP > 30) with CC > 10 require refactoring, not just tests.

**Coverage thresholds for this project:** ado-reports ~87.5%, harvest ~89.9%, Pug ~63.0%, DashCsv.Core ~27.8% (baselines from 2026-03-01).

---

## The Correct CRAP Formula

> **Many secondary sources get this wrong.** The canonical formula (Savoia & Evans, 2007) is:

```
CRAP(m) = CC(m)² × (1 − cov(m)/100)³ + CC(m)
```

where `CC(m)` = cyclomatic complexity and `cov(m)` = branch coverage percentage (0–100).

The widespread simplified variant `CC × (1−coverage)²` is **incorrect** — it understates risk for high-CC methods by an order of magnitude. At CC=54, 52% coverage: correct = **376**, simplified = 12.

**Key properties of the correct formula:**

| Property | Implication |
|---|---|
| Quadratic in CC | Doubling complexity quadruples the risk term |
| Cubic uncovered fraction | Partial coverage reduces risk steeply; 80% cov is much safer than 50% |
| `+CC` tail | Even with 100% coverage, CRAP = CC (never 0 for complex methods) |
| **CC > 30: unrescuable** | `CC² × 0 + CC > 30` — no amount of coverage brings it below 30 |

### Coverage Needed to Stay Below CRAP = 30

| CC | Min branch coverage required | At 0% coverage |
|---|---|---|
| 1–4 | 0% (CRAP < 30 regardless) | 2, 10, 21 |
| **5** | **0% (CRAP = exactly 30)** | **30** |
| 10 | **~42%** | 110 |
| 15 | **~63%** | 240 |
| 25 | **~80%** | 650 |
| **> 30** | **Impossible** | > 930 |

### CRAP Score Examples (Correct Formula)

| Method | CC | Coverage | CRAP | Risk |
|--------|-----|----------|------|------|
| `GetId()` | 1 | 0% | **2** | Low (1–5) |
| `GetId()` | 1 | 100% | **1** | Low (1–5) |
| `ParseDate()` | 5 | 60% | **6.6** | Moderate (5–30) |
| `Validate()` | 10 | 42% | **29.5** | Moderate (5–30) |
| `ParseDate()` | 5 | 0% | **30** | High (30+) — threshold |
| `Validate()` | 10 | 0% | **110** | High (30+) |
| `ParseRecord()` | 25 | 80% | **30** | High (30+) — threshold |
| `ImportData()` | 25 | 0% | **650** | High (30+) — refactor only |

### Risk Bands (Uncle Bob / Savoia)

| CRAP Score | Risk | Action |
|------------|------|--------|
| **1–5** | Low — clean code | No action needed |
| **5–30** | Moderate — refactor or add tests | Address before next change |
| **30+** | High — complex and under-tested | Refactor mandatory (CC > 30 cannot be rescued by tests) |

---

## C#-Specific Complexity Counting

### What Increments Cyclomatic Complexity (+1 each)

| Construct | Notes |
|---|---|
| `if` / `else if` | `else` alone does not |
| `while` / `for` / `foreach` | Each loop head |
| `case` in `switch` | Each non-fall-through case including `default` |
| `&&` / `\|\|` | Short-circuit operators |
| `?:` ternary | One decision point |
| `??` null-coalescing | One decision point |
| `?.` null-conditional | One decision point at IL level |
| `catch` | Each catch clause |
| Pattern `when` guards | Each guard adds a point |
| `switch` expression arms | Each arm; `when` guards add more |

### async/await — IL Complexity Inflation (Critical for .NET)

The compiler transforms async methods into a state machine with a `MoveNext()` switch. Each
`await` adds a state. **IL-level tools (OpenCover, ReportGenerator) measure the compiled state
machine** — not the source code — producing CC values 3–5× higher than source-level analysis.

**Same method, two tools:**

| Tool | What it measures | CC for a 3-await method |
|---|---|---|
| NDepend / Roslyn CA1502 | Source AST | ~4 |
| OpenCover / ReportGenerator | Compiled state machine | ~12–18 |

**Practical rule:** When ReportGenerator shows a suspiciously high CRAP score for an async
method, cross-check with the IDE's Roslyn CA1502 warning before acting. The IL inflation is
noise, not signal, for async orchestration code.

**Mitigation:** Push logic into synchronous helpers; keep async methods to `await` calls only.

### LINQ — IL Complexity Inflation

LINQ queries generate `try/finally` blocks for `IEnumerator<T>.Dispose()` and delegate
caching with conditional null checks. A `.Where().Select()` chain can show higher CC at IL
level than an equivalent `foreach` loop, even though the source logic is simpler.

### Coverage Type: Branch, Not Line

The CRAP formula specifies path coverage; OpenCover approximates this with **branch coverage**.
Using line coverage (Cobertura's native output) understates risk — a method with 100% line
coverage but 40% branch coverage will show a falsely clean CRAP score. Always use OpenCover
format for accurate CRAP scores.

---

## Coverage Collection Setup

### coverage.runsettings

Create `coverage.runsettings` in the repository root. **OpenCover format is required** for CRAP scores.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat code coverage">
        <Configuration>
          <Format>cobertura,opencover</Format>
          <Exclude>[MyProject.Tests]*,[MyProject.Analyzers.Tests]*</Exclude>
          <ExcludeByAttribute>Obsolete,GeneratedCodeAttribute,CompilerGeneratedAttribute,ExcludeFromCodeCoverageAttribute</ExcludeByAttribute>
          <ExcludeByFile>**/obj/**/*,**/*.g.cs,**/*.designer.cs</ExcludeByFile>
          <IncludeTestAssembly>false</IncludeTestAssembly>
          <SingleHit>false</SingleHit>
          <UseSourceLink>true</UseSourceLink>
          <SkipAutoProps>true</SkipAutoProps>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

---

## Collecting Coverage

```bash
rm -rf coverage/ TestResults/

scripts/test.sh --all --json
# Coverage collection requires raw dotnet test with runsettings — no pug wrapper yet:
# dotnet test --settings coverage.runsettings --filter-trait "Category=CI" \
#   --collect:"XPlat Code Coverage" --results-directory ./TestResults

dotnet reportgenerator \
  -reports:"TestResults/**/coverage.opencover.xml" \
  -targetdir:"coverage" \
  -reporttypes:"Html;TextSummary;MarkdownSummaryGithub"
```

---

## Reading the Report

The HTML report's **Risk Hotspots** section shows three metrics:

| Metric | Meaning | CRAP uses it? |
|---|---|---|
| **Cyclomatic Complexity** | Independent paths (measured at IL level) | Yes — inflated for async/LINQ |
| **NPath Complexity** | Acyclic paths (grows combinatorially with nesting) | No — separate signal |
| **CRAP Score** | `CC² × (1−cov/100)³ + CC` | Yes |

**NPath vs. CC:** A method with 3 nested `if`s has CC=4 but NPath=8. NPath blows up much
faster than CC. ReportGenerator's default NPath threshold is 200. It is NOT part of CRAP.

### ReportGenerator Threshold Configuration

Default thresholds (configurable in `riskHotspotsAnalysisThresholds` settings):

| Metric | Default | CI fail gate |
|---|---|---|
| CRAP | 30 | `maximumThresholdForCrapScore` |
| Cyclomatic complexity | 15 | `maximumThresholdForCyclomaticComplexity` |
| NPath complexity | 200 | `maximumThresholdForNPathComplexity` |

To enforce CRAP as a CI quality gate, pass `-riskHotspotsAnalysisThresholds:maximumThresholdForCrapScore=30` to ReportGenerator.

---

## Practical Reduction Strategies

### Reduce Complexity First (Preferred)

- **Extract methods** to CC ≤ 5 — each extracted method independently has CRAP < 30 even untested
- **Guard clauses** (early returns) flatten nesting without adding branches
- **Switch expressions** replace multi-branch `if-else` chains (but count each arm)
- **Dictionary/polymorphism dispatch** eliminates `switch` on type entirely

### Add Tests (When Refactoring Is Risky)

- For CC=10: getting to 42% branch coverage clears CRAP=30
- For CC=25: need 80% branch coverage — often more work than extracting methods
- **Never worth testing your way out of CC > 30** — refactor is mandatory

### async/await Specific

- Keep async methods thin (`await` calls only, no logic)
- Extract synchronous helpers — they have accurate CC and are easy to test
- Avoids IL complexity inflation in OpenCover CRAP scores

---

## CI/CD Integration (GitHub Actions)

```yaml
- name: Run tests with coverage
  run: |
    dotnet test \
      --settings coverage.runsettings \
      --filter-trait "Category=CI" \
      --collect:"XPlat Code Coverage" \
      --results-directory ./TestResults

- name: Generate report
  run: |
    dotnet reportgenerator \
      -reports:"TestResults/**/coverage.opencover.xml" \
      -targetdir:"coverage" \
      -reporttypes:"Html;MarkdownSummaryGithub;Cobertura" \
      "-riskHotspotsAnalysisThresholds:maximumThresholdForCrapScore=30"

- name: Upload coverage report
  uses: actions/upload-artifact@v4
  with:
    name: coverage-report
    path: coverage/

- name: Add coverage to PR
  uses: marocchino/sticky-pull-request-comment@v2
  with:
    path: coverage/SummaryGithub.md
```

---

## Quick Reference

```bash
rm -rf coverage/ TestResults/ && \
dotnet test --settings coverage.runsettings \
  --filter-trait "Category=CI" \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults && \
dotnet reportgenerator \
  -reports:"TestResults/**/coverage.opencover.xml" \
  -targetdir:"coverage" \
  -reporttypes:"Html;TextSummary"

cat coverage/Summary.txt
open coverage/index.html
```

---

## What Gets Excluded

| Pattern | Reason |
|---|---|
| `[MyProject.Tests]*`, `[MyProject.Analyzers.Tests]*` | Test assemblies |
| `GeneratedCodeAttribute`, `CompilerGeneratedAttribute` | Source/compiler generators |
| `ExcludeFromCodeCoverageAttribute` | Explicit developer opt-out |
| `*.g.cs`, `*.designer.cs` | Generated files |
| `SkipAutoProps` | Auto-properties have no branches |

---

## Sources

- [NDepend: CRAP Metric Is a Thing](https://blog.ndepend.com/crap-metric-thing-tells-risk-code/) (2026-03-12)
- [Artima: Original Savoia/Evans CRAP post](https://www.artima.com/weblogs/viewpost.jsp?thread=215899) (2026-03-12)
- [ReportGenerator Wiki: Calculation Details](https://github.com/danielpalme/ReportGenerator/wiki/Calculation-details) (2026-03-12)
- [ReportGenerator Wiki: Settings](https://github.com/danielpalme/ReportGenerator/wiki/Settings) (2026-03-12)
- [OpenCover Issue #620: CRAP feature](https://github.com/OpenCover/opencover/issues/620) (2026-03-12)
- [OtterWise: Understanding CRAP and Cyclomatic Complexity](https://getotterwise.com/blog/understanding-crap-and-cyclomatic-complexity-metrics) (2026-03-12)
- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [ReportGenerator](https://github.com/danielpalme/ReportGenerator)

---

## Lessons Learned

*Updated 2026-03-12. This section accumulates cross-session insights.*

### Formula correction (2026-03-12)

The correct formula `CC² × (1−cov/100)³ + CC` circulates far less than the incorrect simplified
`CC × (1−cov)²`. Verify any CRAP score table by spot-checking: CC=5, cov=0% should yield
exactly 30. If it yields 5, the source is using the simplified (wrong) formula.

### async/await IL gap (2026-03-12)

For async-heavy code, cross-check ReportGenerator CRAP scores against Roslyn CA1502 or NDepend
source-level CC before triaging. An async method showing CRAP=150 in ReportGenerator may only
have source CC=4 — the difference is the compiled state machine.

### Source quality for this domain (2026-03-12)

- **High quality:** NDepend blog, original Artima posts, ReportGenerator wiki, OpenCover GitHub issues
- **Low quality / wrong formula:** Most Medium posts, Stack Overflow answers, random blog summaries
- **Blocked (403):** bytedev.medium.com
