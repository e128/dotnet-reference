# CRAP Score Analysis

## When to Use This Skill

- Evaluating code quality and test coverage before changes
- Identifying high-risk code that needs refactoring or testing
- Setting up or running coverage collection
- Prioritizing which code to test based on risk

---

### Manual CRAP Analysis (Current Fallback)

When tooling is unavailable, estimate CRAP manually:

1. **Count decision points** per method: +1 for each `if`, `else if`, `case`, `for`, `foreach`,
   `while`, `catch`, `?:`, `??`, `?.`, `&&`, `||`, pattern `when` guard. Starting value is 1.
2. **Estimate branch coverage** (not line coverage) from the test suite.
3. Apply the correct formula: **`CRAP = CC² × (1 − cov/100)³ + CC`**
4. **Highest ROI tests:** methods in the Moderate band (CRAP 5–30) with 0% coverage and CC 4–7.
   Pure functions (no DI, no I/O) are easiest to add — make them `internal` and use IVT.
   Methods in the High band (CRAP > 30) with CC > 10 require refactoring, not just tests.

**Coverage baselines:** capture per-project line-coverage baselines before the audit (from the coverage report) so CRAP deltas are measured against known-good numbers rather than absolute thresholds.

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

### Async State-Machine Complexity

The compiler transforms an async method into a state machine with a `MoveNext()` switch.
Compiled state-machine complexity can exceed source-level complexity.
ReportGenerator displays the metrics in the coverage file. It does not recalculate them.

| Source                   | Complexity Basis       |
| ------------------------ | ---------------------- |
| Source analysis          | Source control flow    |
| IL-level coverage report | Compiled state machine |

**Practical rule:** When ReportGenerator shows a suspiciously high CRAP score for an async
method, cross-check with the IDE's Roslyn CA1502 warning before acting. The IL inflation is
noise, not signal, for async orchestration code.

**Mitigation:** Push logic into synchronous helpers; keep async methods to `await` calls only.

### LINQ — IL Complexity Inflation

LINQ queries generate `try/finally` blocks for `IEnumerator<T>.Dispose()` and delegate
caching with conditional null checks. A `.Where().Select()` chain can show higher CC at IL
level than an equivalent `foreach` loop, even though the source logic is simpler.

### Coverage Type: Branch, Not Line

Branch coverage approximates path coverage for this audit.
A method with 100% line coverage and 40% branch coverage can look falsely clean on line coverage alone.
The measured Cobertura files contain method-level `complexity`, `line-rate`, and `branch-rate` attributes.
Use `branch-rate` for a manual CRAP estimate. Source-level complexity remains the audit's basis.

---

## Coverage Collection

```bash
scripts/coverage.sh --no-open
```

The script runs `Category=CI` tests through the in-process Microsoft Testing Platform coverage extension.
`coverage.config.xml` defines the module filters.
The script saves Cobertura reports in `test-results/` and HTML in `test-results/coverage-report/index.html`.
Use this script instead of a separate coverage collector.

---

## Reading the Report

The HTML report can show risk hotspot metrics when the input reports supply them.
An absent hotspot list does not prove that every method has a low CRAP score.

| Metric                | Meaning                    | CRAP Input      |
| --------------------- | -------------------------- | --------------- |
| Cyclomatic Complexity | Independent paths          | Yes             |
| NPath Complexity      | Acyclic paths              | Separate signal |
| CRAP Score            | `CC² × (1−cov/100)³ + CC`   | Result          |

**NPath vs. CC:** A method with 3 nested `if`s has CC=4 but NPath=8. NPath blows up much
faster than CC. ReportGenerator's default NPath threshold is 200. It is NOT part of CRAP.

### ReportGenerator Threshold Configuration

Default thresholds (configurable in `riskHotspotsAnalysisThresholds` settings):

| Metric                | Default | Maximum Threshold                       |
| --------------------- | ------- | --------------------------------------- |
| CRAP                  | 30      | `maximumThresholdForCrapScore`           |
| Cyclomatic Complexity | 15      | `maximumThresholdForCyclomaticComplexity` |
| NPath Complexity      | 200     | `maximumThresholdForNPathComplexity`     |

ReportGenerator documents `-riskHotspotsAnalysisThresholds:maximumThresholdForCrapScore=30` as a gate for scores above 30.
Verify that the input reports produce risk hotspots before you use this setting as a gate.

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
- Avoids IL complexity inflation in CRAP scores

---

## CI Gate

CI enforces line floors through `scripts/coverage-areas.sh --min-line 80 --min-assembly 80`.
No CRAP gate runs in CI. ReportGenerator documents the `maximumThreshold*` settings as failing
the run when a risk hotspot exceeds them. A probe against this repository's reports exited 0
with a maximum of 1, so verify the gate against known hotspots before you wire it into CI.

---

## Coverage Exclusions

`coverage.config.xml` owns the exclusions:

| Exclusion                              | Reason                                  |
| -------------------------------------- | --------------------------------------- |
| Module paths that match `.*Tests\.dll$` | Test assemblies                         |
| `GeneratedCodeAttribute`               | Untestable builder and host boilerplate |
| `ExcludeFromCodeCoverageAttribute`      | Explicit developer opt-out              |

`CompilerGeneratedAttribute` stays measured, because excluding it removes the whole Web assembly.

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
