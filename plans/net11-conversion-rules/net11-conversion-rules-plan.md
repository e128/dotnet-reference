# .NET 11 Conversion Rules: Plan
*Updated: 2026-11-13T12:20:28Z*

Ship the analyzer set that moves working code onto .NET 11 mechanisms, then retire
the rules those mechanisms replace. Source of truth for every shape:
[lode/dotnet/analyzer-candidates/net11-conversion-sweep.md](../../lode/dotnet/analyzer-candidates/net11-conversion-sweep.md).
Roadmap item: [roadmap.md](../roadmap.md).

## Success Criteria

1. Each shipped rule has an analyzer, tests, a README row, and a README section.
2. `scripts/check.sh` passes with zero warnings.
3. Every new rule appears in `AnalyzerReleases.Unshipped.md`.
4. The retire list has a recorded decision for each of the four rules.
5. The lode candidate register matches the shipped set.

## Phases

### Phase 0: Evidence And Open Questions

1. Confirm the `closed` with an abstract member compiles. **Done**: a `closed` class
   that declares an `abstract` member compiles on SDK 11.0.100. `closed` plus
   `abstract` on the type still fails with CS9384.
2. Confirm the exact `Process` capture API surface. **Done**:
   `ProcessTextOutput RunAndCaptureText(ProcessStartInfo, TimeSpan?)` and
   `Task<ProcessTextOutput> RunAndCaptureTextAsync(ProcessStartInfo, CancellationToken)`.
   `ProcessTextOutput` carries `StandardOutput`, `StandardError`, `ExitStatus`,
   `ProcessId`. There is no static `Process.ReadAllTextAsync`.

### Phase 1: E128223 Hand-Rolled Process Capture

Report a process launch that redirects a standard stream, reads it with
`ReadToEnd` or `ReadToEndAsync`, and then waits for exit. Diagnostic only, because
the replacement changes error handling. Tests use the net10 reference set; the
detection reads the existing `Process` and `ProcessStartInfo` surface.
**Shipped** as E128223. The repository's own `PodmanSmokeTests` site converts to
`Process.RunAndCaptureTextAsync`.

### Phase 2: E128224 Read-Only MemoryStream Over a String Or Byte Array

Report `new MemoryStream(Encoding.X.GetBytes(value))` and
`new MemoryStream(bytes)` when the stream is read only. **Shipped** as E128224,
diagnostic only. The rule accepts only read-shaped uses, so a write cannot break.

### Phase 3: E128225 Abstract Base With Members Is Closed

Extend the E128219 shape to a base that declares members. Every derivation stays
in the same assembly. **Shipped** as E128225, with a code fix that replaces
`abstract` with `closed`.

### Phase 4: E128226 Comparer Set Built By Constructor And Add Loop

Report a `HashSet<T>(StringComparer.X)` local that a later loop in the same
method adds to. **Shipped** as E128226, diagnostic only.

### Phase 5: E128220 Extension To Closed Hierarchies

Report a switch over a `closed` hierarchy that ends in a discard or `var` arm.
**Shipped** as an E128220 extension, no new ID.

### Phase 6: Retire Or Re-Read The Superseded Rules

1. E128160: keep until the consumer migration lands.
2. E128192: keep, re-read after the read-only stream migration.
3. E128005: keep, no change.
4. E128049: keep, no change.

### Phase 7: Documentation And Release

1. README rule row and section for each new rule.
2. `AnalyzerReleases.Unshipped.md` rows.
3. Lode register, catalog, and the sweep file.
4. Roadmap item removed once Phase 6 lands.

## Out Of Scope

- Applying the conversions to a consumer repository.
- A Fix All provider for the capture rule.
