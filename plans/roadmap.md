# Roadmap
*Updated: 2026-11-10T23:16:27Z*

## .NET 11 Follow-Up

### Blocked

- Bump `Microsoft.CodeAnalysis.*` past the current band. nuget.org publishes no newer `Microsoft.CodeAnalysis.CSharp` release than the current pin, so the analyzer cannot move to the SDK 11 Roslyn band yet. Check the package index before the bump.
- Raise the `e128_minimum_framework_version` default from 100 to 110. `Microsoft.CodeAnalysis.Analyzer.Testing` exposes `ReferenceAssemblies.Net` codes up to `Net100` only, and its newest release adds no `Net110`. The default follows the snippet band, so it stays at 100.
- Adopt `closed` for the result error hierarchy once the error set holds more than one case. With one case the modifier adds nothing, and `CS8509` has nothing to catch.

### Awaiting Approval

- Rewrite a union rule against the typed `UnionDeclarationSyntax`. It needs an `RSEXPERIMENTAL006` suppression, which this repository does not take without approval.

## Implicit State Machine Rule

- Add explicit properties with accessor bodies. Deduplicate the backing field.
- Add cross-type transition writes through a public setter.
- Add mutable struct support.
- Evaluate a two-transition-body requirement as an option, not the default.
- Build a labeled corpus before the string status rule ships.
- Reject a code fix, a two-flag default, and a timestamp bag rule.

## Deferred

### Codebase Untangling

- Align `MetadataTypes.RegisterForType` with `MetadataTypes.Find`, so an ambiguous type name does not turn a rule off. Decide the duplicate-report behavior first.
- Consolidate the FIPS hash-name and OID tables in `FipsSymbolHelpers` and the security analyzers that repeat them. This includes the SHA-3 spellings in `FipsSha3Analyzer`, `FipsHmacKeyLengthAnalyzer`, and `FipsUnapprovedHashAnalyzer`.
- Replace the private `VerifyAsync` wrappers in the analyzer test suite with `AnalyzerTestRunner.VerifyAsync`.
- Move the syntax root from the code fix base class into the registered action, so the fix providers stop fetching it again.
- Pass `SuggestedType` through `FileSystemPathAnalyzer` instead of converting it to a string and back.
- Derive `DiskIoCatalog.FileMethodKind` from the method-name sets, so membership has one source.
- Give the `Reference.Core` default name `"World"` one owner.
- Route `ImplicitStateMachineAnalyzer.ReadMinFlags` through per-tree options. It reads `GlobalOptions` today, so a project `.editorconfig` value never applies.
- Give the mutable collection types and their read-only interfaces one catalogue. `MutableCollectionExposureAnalyzer`, `ReadOnlyCollectionExposure`, and `MutableCollectionKind` each encode the mapping.
- Give pragma directives one enumerator for `SuppressionAuditAnalyzer`, `PragmaBundlingAnalyzer`, and `PragmaBalanceAnalyzer`.
- Give the path-name heuristic one classifier for `PathNamePatterns`, `FileSystemPathHelpers`, and `CollectionPathAnalyzer`.

