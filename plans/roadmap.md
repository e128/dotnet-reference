# Roadmap
*Updated: 2026-11-12T04:01:14Z*

## Analyzer Detection and Code Fix

Found 2026-11-12 while fixing `GeneratedRegexAnalyzer` in 2.9.7.

### Audit every rule registration for silently dead detection

- **Added:** 2026-11-12T04:00:00Z
- `GeneratedRegexAnalyzer` registered on `SyntaxKind.Attribute`. The compiler never hands that action an attribute attached to a source-generated partial property, and symbol actions skip the property symbol, so all four rules stayed silent across 273 patterns in pugworks while the class passed its own fixtures.
- Sweep every `RegisterSyntaxNodeAction`, `RegisterSymbolAction`, `RegisterSyntaxTreeAction`, and `RegisterOperationAction` for an input the compiler synthesizes. Add one fixture per analyzer that proves the rule fires on a partial property and on a generated member, not only on a hand-written syntax tree.

### Assert every descriptor reaches SupportedDiagnostics

- **Added:** 2026-11-12T04:00:00Z
- `GeneratedRegexAnalyzer` returned two of its four descriptors from `SupportedDiagnostics`, so E128013 and E128014 could not report even when the node was reached.
- Add one test per analyzer asserting that every diagnostic id the class can construct appears in `SupportedDiagnostics`.

### Make the quantifier heuristics parse character classes

- **Added:** 2026-11-12T04:00:00Z
- `GeneratedRegexHelpers.HasNestedQuantifier` scans for a quantifier at depth 1 without parsing character classes, so a group holding `[-*_]` followed by `+` reports E128014 on a pattern that cannot backtrack.
- Replace the hand-rolled scan with `RegexParser`, or teach both helpers class parsing before any further rule consumes them.

### Audit code fix providers against the shapes their rule reports

- **Added:** 2026-11-12T04:00:00Z
- Unverified. No broken fix is known yet. For every rule that ships a `CodeFixProvider`, confirm the fix registers for each shape the analyzer reports and add one fixture per shape. A rule that intentionally offers no fix stays out of scope.

## .NET 11 Follow-Up

### Awaiting Approval

- Rewrite a union rule against the typed `UnionDeclarationSyntax`. It needs an `RSEXPERIMENTAL006` suppression, which this repository does not take without approval.
