# Guidance Analyzers Batch 2

*Updated: 2026-10-31T19:20:54Z*

## Goal

Add the analyzer rules that the second read of the two guidance documents
found. The rule source is the performance skill guide in
`EgorBo/BlazingFastDotnet` and the runtime unsafe best-practices page in
`dotnet/docs`. See [context](guidance-analyzers-batch-2-context.md).

## Scope

| Change           | Detail                                                   |
| ---------------- | -------------------------------------------------------- |
| New rule IDs     | E128131 to E128138                                       |
| Category         | `Performance/` for E128131 to E128135, `Unsafe/` for E128136 to E128138 |
| Code fixes       | E128131 to E128135                                       |
| Diagnostic only  | E128136, E128137, E128138                                |
| Extended rule    | E128061 gains the RVA `ReadOnlySpan<byte>` fix for a byte literal field |
| Delivery         | One branch, one squashed commit, one pull request        |

E128130 is not used. Its candidate row folded into E128061, so the ID stays
free and the next new rule takes E128139.

Every rule reports at Warning. The blanket
`dotnet_analyzer_diagnostic.severity = error` in `.globalconfig` applies.

## Non-Goals

- A rule for a shape that a third-party analyzer already fixes. Check
  [redundant-rules.md](../../lode/dotnet/analyzers/redundant-rules.md) first.
- A rule that needs whole-solution data, runtime measurement, or memory layout.
- The `"literal"u8` rewrite for E128131. The type changes from `byte[]` to
  `ReadOnlySpan<byte>`, so the rewrite is safe only in a narrow position. Track
  it as a later candidate.

## Definition of Done

- [ ] Each rule has an analyzer, a test file, and a code fix where the scope
      table says so.
- [ ] `scripts/check.sh` passes on a clean tree.
- [ ] No new rule fires on the repository source.
- [ ] The README rule table, the per-rule sections, the release files, and the
      lode carry the new state.
