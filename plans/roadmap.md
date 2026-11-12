# Roadmap
*Updated: 2026-11-12T15:07:37Z*

## Analyzer Detection and Code Fix

Found 2026-11-12 while fixing `GeneratedRegexAnalyzer` in 2.9.7.

### Audit every rule registration for silently dead detection

- **Added:** 2026-11-12T04:00:00Z
- `GeneratedRegexAnalyzer` registered on `SyntaxKind.Attribute`. The compiler never hands that action an attribute attached to a source-generated partial property, and symbol actions skip the property symbol, so all four rules stayed silent across 273 patterns in pugworks while the class passed its own fixtures.
- Sweep every `RegisterSyntaxNodeAction`, `RegisterSymbolAction`, `RegisterSyntaxTreeAction`, and `RegisterOperationAction` for an input the compiler synthesizes. Add one fixture per analyzer that proves the rule fires on a partial property and on a generated member, not only on a hand-written syntax tree.
- **Probed 2026-11-12:** at the Roslyn version this repository pins, none of the dead shapes reproduce. `SyntaxKind.Attribute` node actions fire for a partial property (with and without an implementation part), a partial method (with and without an implementation), an implementation part in a second file, and an implementation part in a generated file. Symbol actions deliver partial property and partial method symbols once per part, and partial class symbols once. The pugworks silence therefore depends on the consumer Roslyn version, and the `CompilationUnit` registration already shipped for `GeneratedRegexAnalyzer` stays the defensive pattern.
- **New finding 2026-11-12:** symbol actions fire once per part of a partial property or partial method, so member-level rules that report at `symbol.Locations[0]` report the same diagnostic twice for one logical member (`MidNameUnderscoreAnalyzer` reproduced it). Partial classes are deduplicated by the driver. **Fixed 2026-11-12:** the member-level symbol rules (`MidNameUnderscoreAnalyzer`, `ByteSizeForDataSizeAnalyzer`, `TimeSpanForDurationAnalyzer`, `MutableCollectionExposureAnalyzer`) now call `PartialMembers.IsImplementationPart` and skip the implementing event. Each carries a partial-shape fixture that proves one report per member.
- **Fixture gap:** attribute-node analyzers that can appear on a partial property or partial method still need a partial-shape fixture: `ObsoleteWithoutMessageAnalyzer`, `UnjustifiedSuppressionAttributeAnalyzer`, `DynamicallyAccessedMembersGuardAnalyzer`, `AggressiveOptimizationAnalyzer`, `DllImportToLibraryImportAnalyzer`, `MissingTraitCategoryAnalyzer`, `UntrackedSkipAnalyzer`.
- **Fixture gap closed 2026-11-12:** the first five analyzers in that list now each carry a partial-shape fixture that fires. `MissingTraitCategoryAnalyzer` and `UntrackedSkipAnalyzer` stay out of scope: their targets are xUnit test methods, and a partial test method is not a real-world shape. The member-level symbol rules carry their partial-shape fixtures through the duplicate-report fix above. No open work remains in this item.
- **Code fix audit closed 2026-11-12:** every analyzer that ships a `CodeFixProvider` registers that fix for each rule id it reports. The multi-descriptor analyzers all hold: `GeneratedRegexAnalyzer` maps four rules to four fixers with fixtures, `LowValueTestAnalyzer` maps two rules to one fixer that registers both ids, and `PooledBufferZeroingAnalyzer` leaves E128111 unfixed on purpose. `SortInLoopAnalyzer` ships no fix, and its README rows document no fix for all three ids. The one missing fixture, the E128108 shape of `LowValueTestCodeFixProvider`, is now in `LowValueTestCodeFixTests`. No broken fix found.

### Audit code fix providers against the shapes their rule reports

- **Added:** 2026-11-12T04:00:00Z
- Unverified. No broken fix is known yet. For every rule that ships a `CodeFixProvider`, confirm the fix registers for each shape the analyzer reports and add one fixture per shape. A rule that intentionally offers no fix stays out of scope.

## .NET 11 Follow-Up

### Awaiting Approval

- Rewrite a union rule against the typed `UnionDeclarationSyntax`. It needs an `RSEXPERIMENTAL006` suppression, which this repository does not take without approval.
