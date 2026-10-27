# Unsafe And Performance Analyzer Expansion Context

*Updated: 2026-10-27T18:20:53Z*

## Sources

Two documents seed the candidate list. Each candidate cites the section that
supports it.

### Source A: .NET Performance Skill Guide

Repository `EgorBo/BlazingFastDotnet`, file `SKILL.md`. The document covers
profiling, C# hot-path tips, strings, contention, vectorization, ASP.NET Core,
runtime knobs, and NativeAOT.

Only the C# code-shape rows can become compiler rules. The profiling,
measurement, and environment-variable rows cannot. The sections below name the
rows that produced a candidate.

| Section          | Usable rows                                        |
| ---------------- | -------------------------------------------------- |
| C# quick tips    | Source-generated JSON, source-generated logging, source-generated regex, span bulk APIs, `SearchValues` caching, `[LibraryImport]`, `MethodImplOptions` hints |
| Strings          | Ordinal operations, ASCII helpers, UTF-8 end to end |
| ASP.NET Core     | JSON metadata registration, slim builder            |

### Source B: Unsafe Code Best Practices

Repository `dotnet/docs`, file
`docs/standard/unsafe-code/best-practices.md`. The page lists 26 numbered
sections. Section numbers in this plan refer to that list.

| Section | Topic                                         | Candidate outcome          |
| ------- | --------------------------------------------- | -------------------------- |
| 1       | Untracked managed pointers                    | E128125                    |
| 5       | Reinterpret casts                             | E128112                    |
| 6       | Write barrier and GC atomicity                | E128129                    |
| 7       | Object lifetimes and finalizers               | E128128                    |
| 9       | Removing bounds checks                        | E128114                    |
| 10      | Manual memory-access coalescing               | E128121                    |
| 11      | Unaligned memory access                       | E128113                    |
| 12      | Binary serialization of non-blittable structs | E128118                    |
| 13      | Null managed pointers                         | E128127                    |
| 14      | `stackalloc`                                  | E128109 plus the new fix   |
| 15      | Fixed-size buffers                            | E128119                    |
| 17      | String mutation                               | E128126                    |
| 19      | Uninitialized locals                          | E128120                    |
| 21      | `bool` and `int` conversions                  | E128115                    |

## Coverage Baseline

`E128129` is the highest ID as of 2026-10-27. The next free ID is `E128130`.

The checks below are a snapshot from before implementation. The new analyzer
sources change some counts:

```bash
rg -o 'E1281[1-9][0-9]' -N src tests .globalconfig    # confirms E128112 is free
rg -c 'MethodImplOptions.AggressiveOptimization' --type cs src tests   # 0 hits
rg -c 'MethodImplOptions.AggressiveInlining' --type cs src tests       # 0 hits
rg -c 'DllImport' --type cs src tests                                  # 0 hits
rg -c 'new Regex\(' --type cs src tests                                # 0 hits
rg -c 'LoggerMessage' --type cs src tests                              # 0 hits
rg -c 'MemoryMarshal' --type cs src tests                              # 1 hit
rg -c 'Unsafe\.' --type cs src tests                                   # 0 hits
```

The repo has no unsafe code and no `AllowUnsafeBlocks` property. The new rules
therefore serve package consumers, and they cannot break the local build. The
one `MemoryMarshal` hit is in an analyzer source file.

## Existing Rule Overlap

| Candidate topic                     | Existing coverage                          | Verdict                     |
| ----------------------------------- | ------------------------------------------ | --------------------------- |
| `stackalloc` in a loop, large size  | E128109                                    | New code fix covers pointer assignment |
| Pooled buffer zeroing and reuse     | E128110, E128111                           | No new rule needed          |
| Frozen collections for static readonly | E128027                                 | Covered                     |
| `SearchValues<char>` for `IndexOfAny` | E128102                                  | New rule covers hoisting only |
| `GeneratedRegex` safety options     | E128011 to E128014                         | New rule covers runtime `Regex` only |
| `[GeneratedRegex]` over `new Regex` | MA0110, no fix                             | New rule is not redundant, the fix adds value |
| `[DllImport]` to `[LibraryImport]`  | SYSLIB1054, no fix                         | New rule is not redundant, the fix adds value |
| Ordinal string comparisons          | CA1307, CA1309, CA1310, MA0074             | Excluded                    |
| Reflection in tests                 | E128090, no fix                            | Out of scope                |

The redundancy policy in `lode/dotnet/analyzers/redundant-rules.md` keeps a
redundant rule when it ships a code fix. MA0110 and SYSLIB1054 have no fix, so
E128123 and E128117 add a mechanical path that those rules lack.

## Decisions

### Decision 1: New `Unsafe/` category directory

The unsafe rules describe memory-safety contracts. Folding them into
`Reliability/` would blur the category table in the package README. A separate
directory maps one-to-one to the source document.

Cost: `Directory.Build.targets` and the solution file need no change, because
the analyzer project compiles every `.cs` file under its root.

### Decision 2: Fix providers only where the rewrite is local

The request is to maximize rules that carry a fix. Five candidates carry a
diagnostic only. Inventing a fix for them would produce a wrong rewrite that
loses information. The plan keeps them visible and states the reason in the
README.

### Decision 3: One release

The set builds in one pass. A single release keeps the rule table consistent
between `AnalyzerReleases` and the README.

### Decision 4: Enable every new rule at the blanket severity

`.globalconfig` sets `dotnet_analyzer_diagnostic.severity = error`. The repo
has no trigger hits, so the rules cost nothing locally. A rule that proves
noisy later moves to `suggestion` the same way E128107 and E128108 do.

### Decision 5: Cache type facts per compilation

E128112, E128118, and E128129 decide facts about a type, not about a node.
Resolve `System.Runtime.CompilerServices.Unsafe` and `MemoryMarshal`
once in `RegisterCompilationStartAction`. Follow
[lode/dotnet/analyzers/performance.md](../../lode/dotnet/analyzers/performance.md).

## Runners-Up

Candidates that the plan drops, with the reason. Each one can return if the
evidence changes.

| Candidate                                              | Reason dropped                                   |
| ------------------------------------------------------ | ------------------------------------------------ |
| `List<T>` indexer loop to `CollectionsMarshal.AsSpan`   | High false-positive rate on short lists, and E128084 already teaches the API |
| `GetAlternateLookup` for temporary string keys          | The fix needs a cached lookup field, so it is not local |
| `Dictionary` key without `IEquatable<T>`                | The generated equality members can change semantics |
| `AggressiveInlining` overuse                            | Body-size heuristic is noisy                     |
| `TensorPrimitives` over LINQ numerics                   | Result differences across float operations make the fix unsafe |
| `Ascii.ToUpper` for ASCII-only data                     | Rare in practice and hard to prove the data is ASCII |
| Struct-tag and vector-width specialization             | Design pattern, not a defect                     |
| `Interlocked` on a shared counter                       | Detection needs runtime behavior                 |
| `RuntimeHelpers.GetFunctionPointerForDelegate` variants | Not covered, E128128 matches `Marshal` only      |

## Evidence Gaps

- No benchmark proves a win for any new fix. The rules encode published
  guidance, not local measurement. The performance guide itself asks for
  measurement, so the README wording must say "avoids allocation" and must not
  claim a speedup.
- E128118 and E128129 each test the type inline with `IsUnmanagedType`. No shared
  helper exists.
