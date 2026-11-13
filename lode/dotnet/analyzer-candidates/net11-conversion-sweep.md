# .NET 11 Conversion Sweep
confidence: high
*Updated: 2026-11-13T13:54:59Z*

Five rule shapes convert code that already works to a mechanism the .NET 11 band carries. The net11-upgrade close on 2026-11-13 supplied the call-site evidence for each shape. All five ship. Scores follow [scoring-rubric.md](../../infrastructure/scoring-rubric.md). The rules live in [catalog.md](catalog.md), and the plan is [net11-conversion-rules](../../../plans/net11-conversion-rules/net11-conversion-rules-plan.md).

A shape scored under 3 sits in [skipped-decisions.md](skipped-decisions.md) with its reason. E128219 to E128221 already ship the union half of the band.

## Hand-Rolled Child-Process Capture

**Schema**: score 5, rule E128223, no code fix.

**Pattern**: a `ProcessStartInfo` that sets `RedirectStandardOutput` or `RedirectStandardError`, followed by a `ReadToEnd` or `ReadToEndAsync` call on that stream, then a `WaitForExit` on the same process local.

**Target**: `Process.RunAndCaptureText(ProcessStartInfo, TimeSpan?)` and `RunAndCaptureTextAsync(ProcessStartInfo, CancellationToken)` return `ProcessTextOutput`, which carries `StandardOutput`, `StandardError`, `ExitStatus`, and `ProcessId`. `ProcessExitStatus` exposes `ExitCode`, `Canceled`, and `Signal`.

**Why not covered**: no rule removes the hand-rolled capture, and none offers an exit-status value.

**Scope**: skip a launch that writes to a redirected standard input. Skip a launch that reads one line and stops early.

**Evidence**: four blocks in three files (as of 2026-11-13). `src/Harvest/Extraction/Ocr/TesseractOcrService.cs` and `src/Harvest/Extraction/Transcription/FfmpegAudioConverter.cs` hold one each. `src/Harvest/Utilities/ImageOptimizer.cs` holds two. The `PodmanSmokeTests` site in this repository is converted.

## Read-Only MemoryStream Over a String or Byte Array

**Schema**: score 4, rule E128224, no code fix.

**Pattern**: `new MemoryStream(Encoding.X.GetBytes(value))`, or `new MemoryStream(bytes)` with no writable argument, where the stream is read only.

**Target**: `StringStream(string, Encoding)` wraps a string without a copy. `ReadOnlyMemoryStream(ReadOnlyMemory<byte>)` wraps the bytes without a copy. This band carries no `ReadOnlySequenceStream`.

**Why not covered**: no E128 rule. CA2000 owns disposal only.

**Scope**: skip a pre-sized buffer the code writes into. `ImageSearchDownloader` and `EncodingDetector` both allocate a writable buffer with a capacity hint. The rule accepts only read-shaped uses, because the target types reject a write.

**Evidence**: nine `new MemoryStream` sites in `src/` (as of 2026-11-13). One wraps a string for a read-only parse at `src/Pug/Converters/MhtmlToHtmlConverter.cs`.

## Comparer Set Built by Constructor and Add Loop

**Schema**: score 3.5, rule E128226, no code fix.

**Pattern**: `new HashSet<T>(StringComparer.X)` with no collection initializer, where a later loop in the same method adds items.

**Target**: `[with(StringComparer.X), .. items]` passes the comparer as a collection-expression argument.

**Why not covered**: E128027 owns a `static readonly` set and asks for a frozen set. No rule reads the constructor-plus-loop shape.

**Scope**: exclude a field initializer, because E128027 owns that declaration. Exclude a set that outlives the method, because the expression form fits a local. Exclude a set with an `Add` call outside the loop.

**Evidence**: the net11 survey names four call sites (as of 2026-11-13). `src/Harvest/Extraction/Extractors/EpubExtractor.cs`, `src/Harvest/Weave/WeaveService.cs`, `src/TravelSearch/Cruise/CruiseSearchFilters.cs`, and `src/WatchEngine.Avalonia/Services/FileSystemWatcherAdapter.cs` each build a case-insensitive set and then add to it.

## Closed Hierarchy With Abstract Members

**Schema**: score 4, rule E128225, code fix.

**Pattern**: an `abstract` class with members whose every derivation lives in the same assembly.

**Target**: the `closed` modifier on the base. The compiler then reports `CS8509` on a switch that misses a leaf.

**Why not covered**: E128219 stops at a memberless base.

**Resolved question**: a `closed` class that declares an `abstract` member compiles on SDK 11.0.100. The `closed` modifier implies abstract. CS9384 rejects only the `closed abstract` modifier pair on the type.

**Scope**: a record declaration stays silent, because the compiler synthesizes members from the parameter list and the equality contract. A public base stays silent, because an outside assembly may derive from it.

**Evidence**: `src/Harvest/Extraction/Extractors/BaseTextExtractor.cs` and `src/Harvest/Conversion/BaseDocumentConverter.cs` are the two abstract bases in `src/`, and every derivation lives in the Harvest assembly (as of 2026-11-13).

## Closed-Hierarchy Switch With a Discard Arm

**Schema**: score 3, extension of E128220, no new rule ID.

**Pattern**: a switch whose governing type is a `closed` class hierarchy, and whose last arm is `_` or `var _`.

**Target**: a named arm or a `null` arm keeps `CS8509` alive for a missing leaf.

**Why not covered before**: E128220 confirmed `UnionTypes.IsUnion` on the governing type. A closed class hierarchy is not a union. The rule also reads the closed attribute the compiler emits on the type.

**Scope**: an open hierarchy stays silent, because a later external leaf is legal.

**Evidence**: no site today. The repo adopted `closed` on 2026-11-13, and the rule exists so the first hand-written switch cannot drop the guarantee.

## Rules the Band Retires

A rule that guards a shape the band removes becomes noise once the migration lands. Recorded decision per rule:

| Rule | Decision | Reason |
| ---- | -------- | ------ |
| E128160 | retired | `Process.RunAndCaptureText` removes the hand-rolled capture, and E128223 reports the read-then-wait shape that replaced it. No caller can write the deadlock the rule guarded. |
| E128192 | keep, re-read after the stream migration | The read-only stream types remove the `new MemoryStream(byte[])` wrap that hides a buffer. The rule still covers `GetBuffer` on a writable stream. |
| E128005 | keep, no change | The `closed` base carries derivations, so the seal rule does not report it. The rule still owns the leaf class. |
| E128049 | keep, no change | The JSON discriminated-union note is guidance text. Re-read it when a union serializer lands. |

## Related

- [index.md](index.md): open register rows
- [skipped-decisions.md](skipped-decisions.md): the net11 shapes checked and declined
- [catalog.md](catalog.md): the shipped rule set
- [rule-details-conversion.md](../analyzers/rule-details-conversion.md): detection shape for E128223 to E128226
