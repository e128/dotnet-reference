# Async Fire-And-Forget Analyzer Plan
*Updated: 2026-11-11T16:24:00Z*

Status: **closed, no rule ships.** The probe phase answered every question, and
each candidate either has an owner in the installed rule sets or fails the value
test. The permanent record is
[lode/dotnet/analyzer-candidates-async.md](../../lode/dotnet/analyzer-candidates-async.md).

## Result

| Candidate | Probe | Decision |
| --------- | ----- | -------- |
| Discarded awaitable | MA0134 and VSTHRD110 fire | Drop |
| `async` lambda to void delegate | AsyncFixer03, MA0147, VSTHRD101 fire | Drop |
| Suppressed-throw await | No owner | Drop on value, zero uses in `src/` |
| Dependency event subscription | No owner | Drop, false positives on long-lived types |

No diagnostic id was assigned. E128223 stays free.

## What Shipped Instead

`scripts/build.sh --project` now accepts a path to a `.csproj` file, so a scratch
probe under `.claude/tmp/` builds through the routed script. The guardrail that
blocks raw `dotnet build` stays intact.

## Why The Plan Changed

A ponytail review before execution cut three candidates. The probe then cut the
last one. The original plan assumed the async-drop family was unowned. Two
installed analyzer packages declare it by name.

## Related

- [async-analyzers-context.md](async-analyzers-context.md): decisions
- [async-analyzers-tasks.md](async-analyzers-tasks.md): executed checklist
