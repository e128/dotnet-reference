# .NET 11 Conversion Sweep
confidence: high
*Updated: 2026-11-13T12:08:30Z*

Five rule shapes convert code that already works to a mechanism the .NET 11 band carries. The net11-upgrade close on 2026-11-13 supplied the call-site evidence for each shape. Scores follow [scoring-rubric.md](../../infrastructure/scoring-rubric.md), and the open register row for each shape sits in [index.md](index.md).

A shape scored under 3 sits in [skipped-decisions.md](skipped-decisions.md) with its reason. E128219 to E128221 already ship the union half of the band. See [catalog.md](catalog.md).

## Hand-Rolled Child-Process Capture

**Score**: 5 (frequency 2, coverage gap 1, expressibility 1, value 1)

**Pattern**: a `ProcessStartInfo` that sets `RedirectStandardOutput` or `RedirectStandardError`, followed by a `ReadToEnd` or `ReadToEndAsync` call on that stream, then a `WaitForExit` on the same process local.

**Target**: `Process.RunAndCaptureText` and `RunAndCaptureTextAsync` return stdout, stderr, and the exit status in one call. `Process.ReadAllTextAsync` and `Process.ReadAllLinesAsync` cover the single-stream shapes, and `ReadAllLinesAsync` returns `ProcessOutputLine` values that keep stdout separate from stderr.

**Why not covered**: E128160 guards the read-then-wait ordering when both streams are redirected. It does not remove the hand-rolled capture, and it offers no exit-status value.

**Scope**: skip a launch that writes to a redirected standard input. Skip a launch that reads one line and stops early.

**Evidence**: four blocks in three files (as of 2026-11-13). `src/Harvest/Extraction/Ocr/TesseractOcrService.cs` and `src/Harvest/Extraction/Transcription/FfmpegAudioConverter.cs` hold one each. `src/Harvest/Utilities/ImageOptimizer.cs` holds two.

**Code fix**: replace the launch, both reads, and the wait with one call. The fix changes error handling, so it needs a review rather than a Fix All.

## Read-Only MemoryStream Over a String or Byte Array

**Score**: 4 (frequency 1, coverage gap 1, expressibility 1, value 1)

**Pattern**: `new MemoryStream(Encoding.X.GetBytes(value))`, or `new MemoryStream(bytes)` with no writable argument, where the stream is read only.

**Target**: `StringStream` wraps a `string` with an encoding and no copy. `ReadOnlyMemoryStream` wraps a `ReadOnlyMemory<byte>`, and `ReadOnlySequenceStream` wraps a `ReadOnlySequence<byte>`.

**Why not covered**: no E128 rule. CA2000 owns disposal only.

**Scope**: skip a pre-sized buffer the code writes into. `ImageSearchDownloader` and `EncodingDetector` both allocate a writable buffer with a capacity hint.

**Evidence**: nine `new MemoryStream` sites in `src/` (as of 2026-11-13). One wraps a string for a read-only parse at `src/Pug/Converters/MhtmlToHtmlConverter.cs`.

## Comparer Set Built by Constructor and Add Loop

**Score**: 3.5 (frequency 1, coverage gap 1, expressibility 1, value 0.5)

**Pattern**: `new HashSet<T>(StringComparer.X)`, with or without a collection initializer, where a later loop in the method adds more items.

**Target**: `[with(StringComparer.X), .. items]` passes the comparer as a collection-expression argument.

**Why not covered**: E128027 owns a `static readonly` set and asks for a frozen set. No rule reads the constructor-plus-loop shape.

**Scope**: exclude a field initializer, because E128027 owns that declaration. Exclude a set that outlives the method, because the expression form fits a local.

**Evidence**: the net11 survey names four call sites (as of 2026-11-13). `src/Harvest/Extraction/Extractors/EpubExtractor.cs`, `src/Harvest/Weave/WeaveService.cs`, `src/TravelSearch/Cruise/CruiseSearchFilters.cs`, and `src/WatchEngine.Avalonia/Services/FileSystemWatcherAdapter.cs` each build a case-insensitive set and then add to it.

## Closed Hierarchy With Abstract Members

**Score**: 4 (frequency 2, coverage gap 1, expressibility 0.5, value 0.5)

**Pattern**: an `abstract` base class with members whose every derivation lives in the same assembly.

**Target**: the `closed` modifier on the base. The compiler then reports `CS8509` on a switch that misses a leaf.

**Why not covered**: E128219 stops at a memberless base. A positional record synthesizes members from its parameter list. The rule cannot tell that shape from a base with a hand-written member at the syntax level it reads.

**Open question**: whether the pinned compiler accepts a `closed` class that declares an `abstract` member. CS9384 rejects `closed` combined with `abstract` on the type. Verify the member case before a plan.

**Evidence**: `src/Harvest/Extraction/Extractors/BaseTextExtractor.cs` and `src/Harvest/Conversion/BaseDocumentConverter.cs` are the two abstract bases in `src/`, and every derivation lives in the Harvest assembly (as of 2026-11-13).

## Closed-Hierarchy Switch With a Discard Arm

**Score**: 3 (frequency 0, coverage gap 1, expressibility 1, value 1)

**Pattern**: a switch whose governing type is a `closed` class hierarchy, and whose last arm is `_` or `var _`.

**Target**: extend E128220 from unions to closed hierarchies. A named arm or a `null` arm keeps `CS8509` alive for a missing leaf.

**Why not covered**: E128220 confirms `UnionTypes.IsUnion` on the governing type. A closed class hierarchy is not a union.

**Scope**: an open hierarchy stays silent, because a later external leaf is legal. The rule reads the closed marker the compiler emits on the type.

**Evidence**: no site today. The repo adopted `closed` on 2026-11-13, and the rule exists so the first hand-written switch cannot drop the guarantee. The extension needs no new rule ID.

## Rules the Band Retires

A rule that guards a shape the band removes becomes noise once the migration lands. The pugworks roadmap holds the work item.

| Rule | Fate | Reason |
| ---- | ---- | ------ |
| E128160 | retire after the capture migration | `Process.RunAndCaptureText` removes the hand-rolled capture. No site can write the deadlock the rule guards. |
| E128192 | revisit after the stream migration | The read-only stream types remove the `new MemoryStream(byte[])` wrap that hides a buffer. |
| E128005 | re-read the fix guidance | The `closed` modifier from E128219 sits beside `sealed` for a base class. The rule still owns the leaf class. |
| E128049 | refresh the allowlist | Its JSON discriminated-union justification predates the net11 union serializer support. |

## Related

- [index.md](index.md): open register rows for these shapes
- [skipped-decisions.md](skipped-decisions.md): the net11 shapes checked and declined
- [catalog.md](catalog.md): the shipped union family, E128219 to E128221
