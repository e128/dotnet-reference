# .NET 11 Conversion Rules: Context
*Updated: 2026-11-13T12:20:28Z*

## Problem

Five conversion shapes scored at or above 3 in the 2026-11-13 net11-upgrade
close. None has an owner in the installed rule sets. The rules convert code that
already works onto a .NET 11 mechanism. The same mechanisms make four shipped
rules redundant.

## Evidence

| Shape | Score | Sites (as of 2026-11-13) |
| ----- | ----- | ------------------------ |
| Hand-rolled process capture | 5 | 4 blocks in 3 files |
| Read-only `MemoryStream` | 4 | 9 `MemoryStream` sites, 1 string wrap |
| `abstract` base with members | 4 | 2 bases, every leaf in-assembly |
| Comparer set by constructor and `Add` loop | 3.5 | 4 sites |
| Closed-hierarchy switch with a discard arm | 3 | 0 sites today |

The detail for each shape lives in
[net11-conversion-sweep.md](../../lode/dotnet/analyzer-candidates/net11-conversion-sweep.md).

## Decisions

1. New rule IDs from E128223. E128222 is taken by the path containment rule.
2. The capture rule ships diagnostic only. The replacement rewrites the error
   handling around the launch, so an automatic fix can change behavior.
3. E128225 is a new ID, not a widening of E128219. A shipped rule keeps its
   contract, and the release manifest can state the change.
4. The closed-hierarchy switch shape extends E128220. It shares the catch-all
   contract exactly, so a second ID would report the same line.
5. Tests keep the net10 reference set. Every rule binds the existing `Process`,
   `ProcessStartInfo`, and `MemoryStream` surface. The `.NET 11` target types
   appear only in the message and the fix.

## Closed Questions

1. **Does `closed` accept an abstract member?** Yes. A probe on SDK 11.0.100
   compiles a `closed` class with an `abstract` member. CS9384 rejects only the
   `closed abstract` modifier pair on the type.
2. **What is the capture API?** `Process.RunAndCaptureText(startInfo, timeout)`
   returns `ProcessTextOutput`, which carries `StandardOutput`,
   `StandardError`, `ExitStatus`, and `ProcessId`. The async form takes a
   `CancellationToken`. The sweep text names a static `Process.ReadAllTextAsync`
   that this band does not carry, so the plan drops it.

## Runners-Up

- Extend E128219 in place. Rejected: the release manifest cannot express a
  widened detection contract without a Changed Rules row, and the two shapes
  carry different fix risk.
- Ship a Fix All for the capture rule. Rejected: the fix changes error handling
  on every site.
