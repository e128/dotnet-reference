# Unsafe And Performance Analyzer Expansion Plan

*Updated: 2026-10-27T18:20:53Z*

## Goal

Add E128 rules derived from two published guidance documents. Thirteen rules
ship a code fix provider. Five rules ship a diagnostic only, because the
remediation needs context that a fix provider cannot supply.

The two sources are the .NET performance skill guide (EgorBo/BlazingFastDotnet)
and the runtime unsafe-code best-practices page (dotnet/docs). See
[context](unsafe-perf-analyzers-context.md) for the source mapping.

## Status

The analyzers, the code fix providers, and the tests are built. `scripts/check.sh`
passes. The rules sit in `AnalyzerReleases.Unshipped.md` and wait for a release.
Remaining work is the release steps in [tasks](unsafe-perf-analyzers-tasks.md).

## Scope

| Change                 | Detail                                                          |
| ---------------------- | --------------------------------------------------------------- |
| New category directory | `src/E128.Analyzers/Unsafe/` holds E128112 to E128115 and E128118 to E128129 |
| Extended category      | `src/E128.Analyzers/Performance/` holds E128116, E128117, and E128121 to E128124 |
| Analyzer IDs           | E128112 to E128129, contiguous, next free ID E128130            |
| Release                | One release carries the full set                                 |
| Test files per rule    | `{Name}AnalyzerTests.cs`, plus `{Name}CodeFixTests.cs` where a fix exists |

Every rule reports at Warning. The blanket
`dotnet_analyzer_diagnostic.severity = error` in `.globalconfig` applies, so no
new severity entry is needed. The repo has zero hits for every trigger.

## Non-Goals

- Profiling, benchmark runs, and runtime knob settings. Those rows in the
  performance guide cannot become compiler rules.
- Rules that need whole-solution data, runtime measurement, or memory-layout
  knowledge the compiler does not expose.
- Rules a third-party analyzer already fixes mechanically. The redundancy
  policy in `lode/dotnet/analyzers/redundant-rules.md` still applies.

## Rules With A Code Fix

Thirteen rules. Each fix is a local rewrite of one expression, one attribute, or
one declaration.

| ID      | Rule                                                            | Fix shape                                            |
| ------- | --------------------------------------------------------------- | ---------------------------------------------------- |
| E128112 | `Unsafe.As<S,T>` between two value types of equal size          | Rename to `Unsafe.BitCast`, drop the `ref` argument  |
| E128113 | `Unsafe.Read` and `Unsafe.Write` on possibly misaligned data     | Rename to the `Unaligned` variant                    |
| E128114 | `Unsafe.Add` over `MemoryMarshal.GetArrayDataReference`          | Replace the expression with `array[index]`           |
| E128115 | `Unsafe.As<int,bool>` or `Unsafe.As<bool,int>`                   | Replace with a comparison or a ternary               |
| E128116 | `MethodImplOptions.AggressiveOptimization` on a method           | Remove the argument, or the whole attribute          |
| E128117 | `[DllImport]` on a P/Invoke declaration                          | Rewrite to `[LibraryImport]`, add `partial`           |
| E128118 | `MemoryMarshal.Read<T>` or `Cast` on a non-blittable type       | Rewrite a zero byte read as a comparison             |
| E128119 | `fixed byte field[N]` buffer field                               | Rewrite the field as an `[InlineArray]` struct       |
| E128120 | `Unsafe.SkipInit` on a local                                     | Replace the call with `T name = default;`            |
| E128121 | Element loop that duplicates `SequenceEqual`                     | Replace the loop with the span call                  |
| E128122 | Logger call that builds an argument before the level check       | Wrap the statement in a `logger.IsEnabled` guard     |
| E128123 | Runtime `new Regex(...)` in a `static readonly` field            | Convert to a `[GeneratedRegex]` partial property     |
| E128124 | `SearchValues.Create` outside a `static readonly` field          | Hoist the call to a static field                     |

### Fix Constraints

- E128112 needs both type arguments to be value types of equal fixed size.
- E128117 applies only to a signature with blittable parameters and return
  type. A string parameter needs `StringMarshalling`, which the user must choose.
- E128119 generates the buffer struct in the same file.
- E128121 accepts one loop shape: an element-wise equality loop with a `false`
  return on the first mismatch.
- E128123 needs a constant pattern literal. A computed pattern keeps the
  diagnostic and loses the fix.
- E128124 needs the argument list to be constant. A method parameter as
  argument keeps the diagnostic and loses the fix.

## Diagnostic-Only Rules

Five rules have no mechanical fix. The remediation depends on intent that the
syntax tree does not carry. Ship the diagnostic, and record the reason in the
README entry.

| ID      | Rule                                                     | Why no fix                                          |
| ------- | -------------------------------------------------------- | --------------------------------------------------- |
| E128125 | `Unsafe.AsPointer(ref x)` outside a `fixed` statement   | The pin scope is a caller decision                  |
| E128126 | Write through `fixed (char* p = text)` on a string        | The replacement depends on the new string build     |
| E128127 | `Unsafe.NullRef<T>()` or `Unsafe.AsRef<T>((void*)0)`      | Safe code has no null byref equivalent              |
| E128128 | `Marshal.GetFunctionPointerForDelegate` with no anchor    | The anchor field must be placed by the user         |
| E128129 | `Unsafe.Copy` over a type that contains a reference       | The replacement depends on a deep or a shallow copy |

## Deliberate Exclusions

| Source row                                  | Why it stays a document, not a rule         |
| ------------------------------------------- | ------------------------------------------- |
| Ordinal string comparisons                  | CA1307, CA1309, and CA1310 already cover it |
| `ToLower()` equality idiom                  | Meziantou covers it                         |
| Runtime knob tuning                         | Environment variables, not source           |
| False sharing and cache-line padding        | Not visible to the compiler                 |
| Thread-local and striped counters           | Detection needs runtime behavior            |
| `WebApplication.CreateSlimBuilder`          | Project-level choice, not a code defect     |
| Source-generated JSON metadata registration | Whole-project configuration, no local fix   |
| Object header and MethodTable access        | No supported replacement to offer           |
| Fuzzing and raw IL validation               | Process guidance, not a code shape          |
| `AggressiveInlining` overuse                | The size heuristic is too noisy for a rule  |

## Success Criteria

1. Each rule has an analyzer test file and, where a fix exists, a code fix test
   file.
2. `scripts/check.sh` passes.
3. `src/E128.Analyzers/README.md` has a row per rule and a section per rule.
4. `AnalyzerReleases.Unshipped.md` has a row per rule.
5. `PublicAPI.Unshipped.txt` has two lines per new type.
6. No rule fires on the repo's own source.

## Related

- [Context and evidence](unsafe-perf-analyzers-context.md)
- [Tasks](unsafe-perf-analyzers-tasks.md)
- [Analyzer strategy](../../lode/dotnet/analyzers.md)
- [Analyzer candidates](../../lode/dotnet/analyzer-candidates.md)
