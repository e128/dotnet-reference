# Roadmap
*Updated: 2026-11-13T12:14:15Z*

Unplanned work awaiting a plan.

## Active

### .NET 11 analyzer refresh: ship the five conversion rules, retire the four the band supersedes
- **Added:** 2026-11-13T12:14:15Z
- **Prompt:** Build the analyzer set that moves working code onto .NET 11 mechanisms, and delete the rules those mechanisms replace. The whole brief lives in [net11-conversion-sweep.md](../lode/dotnet/analyzer-candidates/net11-conversion-sweep.md), with one register row per shape in [index.md](../lode/dotnet/analyzer-candidates/index.md).
- **Add:** child-process capture to `Process.RunAndCaptureText` (5/5, four blocks in three Harvest files), read-only `MemoryStream` to `StringStream` or `ReadOnlyMemoryStream` (4/5, nine sites), an `abstract` base with members to `closed` (4/5, two bases with every leaf in-assembly), a comparer set built by constructor and `Add` loop to `[with(comparer), .. items]` (3.5/5, four sites), and the E128220 discard-arm shape over a closed hierarchy (3/5, an extension with no new ID). Take rule IDs from `AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md`. The union family in `src/E128.Analyzers/Design/` is the pattern to copy.
- **Remove or re-read:** E128160 retires once the pugworks capture migration lands, because the new API leaves no hand-rolled capture to deadlock. E128192 re-reads after the read-only stream migration, E128005 beside the `closed` modifier, and E128049's JSON discriminated-union allowlist beside the net11 union serializer.
- **Open question:** whether the pinned compiler accepts a `closed` class that declares an `abstract` member. Resolve it before the closed-hierarchy rule reaches Phases beyond Phase 0.
- **Trigger:** plan when the pugworks `Process.RunAndCaptureText` adoption lands, or when a second occurrence of any shape shows up in review.
