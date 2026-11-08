# Roadmap
*Updated: 2026-11-08T17:36:49Z*

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
- Consolidate the FIPS hash-name and OID tables in `FipsSymbolHelpers` and the security analyzers that repeat them.
- Replace the private `VerifyAsync` wrappers in the analyzer test suite with `AnalyzerTestRunner.VerifyAsync`.
- Move the syntax root from the code fix base class into the registered action, so the fix providers stop fetching it again.
- Pass `SuggestedType` through `FileSystemPathAnalyzer` instead of converting it to a string and back.
- Give the SHA-3 name spellings in `FipsSha3Analyzer`, `FipsHmacKeyLengthAnalyzer`, and `FipsUnapprovedHashAnalyzer` one owner.
- Derive `DiskIoCatalog.FileMethodKind` from the method-name sets, so membership has one source.
- Give the `Reference.Core` default name `"World"` one owner.

