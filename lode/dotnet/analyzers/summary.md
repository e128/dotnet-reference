# Analyzers Summary
*Updated: 2026-11-06T15:25:45Z*

`src/E128.Analyzers` holds every E128 Roslyn rule. `src/E128.Analyzers.CodeFixers`
holds the fix providers. The analyzer assembly targets `netstandard2.0` and
holds no Workspaces reference. Rules use semantic checks when the subject
requires symbols. A rule with a code fix reports only the shapes that its fix
can repair.

## Reading Order

| Question                                         | File                                              |
| ------------------------------------------------ | ------------------------------------------------- |
| How do I add a rule?                             | [new-analyzer-checklist.md](new-analyzer-checklist.md) |
| How does the rule read a user option?            | [configuration.md](configuration.md)              |
| Which Roslyn API answers this question?          | [symbol-lookups.md](symbol-lookups.md)            |
| How do I test the rule?                          | [testing.md](testing.md)                          |
| How do I keep a rule fast?                       | [performance.md](performance.md)                  |
| How do I write the fix?                          | [code-fix-patterns.md](code-fix-patterns.md)      |
| What text may the fix write?                     | [code-fix-output.md](code-fix-output.md)          |
| Which edit API do I use?                         | [code-fix-techniques.md](code-fix-techniques.md)  |
| How is the package built and packed?             | [package-layout.md](package-layout.md)            |
| Which buffer rules exist?                        | [pooled-buffer-rules.md](pooled-buffer-rules.md)  |
| Which rules are disabled as redundant?           | [redundant-rules.md](redundant-rules.md)          |
| What is the FIPS policy set?                     | [fips-rules.md](fips-rules.md)                    |

## Shared Invariants

- A rule reports a construct only after the semantic model confirms the symbol.
- A rule reports error severity only where policy demands a hard gate. The FIPS
  set is that case.
- A code fix returns a changed document or offers no action. An offered fix
  never leaves the document unchanged.
- The fix and the analyzer read one table. The table is `internal static
  readonly` on the analyzer.
- A rule that scans a block for a later use skips a nested lambda body and a
  local function body. The block order does not give the time that body runs.

## Related

- [../analyzers.md](../analyzers.md): analyzer strategy and the category map
- [../analyzer-candidates.md](../analyzer-candidates.md): the ID map and free IDs
- [../release-tracking.md](../release-tracking.md): Shipped and Unshipped files
