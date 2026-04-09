# Project Conventions (Optional)

This file is read by the overhauler if present. It contains project-specific coding standards,
analyzer configuration, and test relaxations that customize the overhaul behavior.

**If this file does not exist, the overhauler runs with sensible .NET defaults.**

Delete or modify this file to match your project's conventions.

## Coding Standards

- Use `string.Empty` instead of `""` for empty string literals
- Never use `!` (null-forgiving operator) to silence CS8600-CS8604 — these require human judgment
- Never suppress analyzer rules without explicit approval (`#pragma warning disable`, `[SuppressMessage]`)

## Analyzer Packages

If your project uses analyzers, list them here so the overhauler can interpret their output:

| Package | Rules | Focus |
|---------|-------|-------|
| AsyncFixer | AF* | async/await anti-patterns |
| Meziantou.Analyzer | MA* | code quality, performance |
| Microsoft.VisualStudio.Threading.Analyzers | VSTHRD* | threading |
| Roslynator.Analyzers | RCS* | general analyzers |
| SharpSource | SS* | runtime correctness |
| SonarAnalyzer.CSharp | S* | code smells, bugs |

## Severity Triage

Override the default severity triage by adding custom mappings:

| Severity | Criteria | Examples |
|----------|----------|---------|
| HIGH | Build errors, null-ref warnings, security | CS0246, CS8602, CS8604, S* |
| MEDIUM | Analyzer violations, format, unused code | IDE0005, IDE0011, CA1822, MA* |
| LOW | Style, naming, documentation | IDE0062, CS1591 |

## Auto-Approved Fixes

These fixes align with common auto-approval policies — apply without prompting:

| Code | Fix |
|------|-----|
| IDE0005 | Remove unused `using` statements |
| IDE0161 | Convert to file-scoped namespace |
| Format | Run `dotnet format --include <files>` |
| Using sort | Sort `using` directives |

## Never Auto-Fix

| Code | Reason |
|------|--------|
| CS8600-CS8604 | Null-ref warnings require human judgment — list in findings, don't add `!` |
| Any `#pragma` | Suppressions require explicit approval |

## Test Project Relaxations

Rules that are NOT violations in test code (files under `tests/` or `*Tests*` directories):

| Rule | Reason |
|------|--------|
| CA1707 | Underscores in test method names |
| CA2007, MA0004, VSTHRD111 | No ConfigureAwait required in tests |
| VSTHRD200 | Test methods don't need Async suffix |
| CA1515 | Test classes can be public |
| MA0040, xUnit1051 | Ambient CancellationToken not required |
