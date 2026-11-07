# Mr. Sparkle Plan: performance analyzers (working tree, 2026-11-07)
*Updated: 2026-11-07T17:38:37Z*

Target: working tree against `ed9e5e31` on `feature/performance-analyzers`.
49 files: 11 tracked modified, 38 new.

Verdict at review time: Request changes. Two confirmed High findings. Both are
fixed in the working tree, and `scripts/check.sh` passes.

## Findings

| # | Severity | File | Problem | Status |
| - | -------- | ---- | ------- | ------ |
| 1 | High | `src/E128.Analyzers/Performance/LargeStructByValueAnalyzer.cs` | Unbounded recursion on a self-referential struct crashes the compiler | Fixed |
| 2 | High | `src/E128.Analyzers/Performance/PresizeCollectionAnalyzer.cs` | `SortedSet` and `SortedDictionary` have no `(int)` constructor, so the fix emits code that does not compile | Fixed |
| 3 | Medium | `src/E128.Analyzers/Performance/RegexInstanceInLoopAnalyzer.cs` | Constant pattern in a loop reported by neither E128210 nor E128123 | Fixed |
| 4 | Medium | `src/E128.Analyzers/LoopContext.cs` | Third copy of loop detection, and a deconstruction `foreach` was not recognised | Partly fixed |
| 5 | Medium | `src/E128.Analyzers/LoopContext.cs` | `HasConstantBound` matched any ancestor loop and any literal, including `null` | Fixed |
| 6 | Medium | `src/E128.Analyzers/Performance/ConcurrentDictionaryDoubleLookupAnalyzer.cs` | Fired on an indexer write and missed literal keys | Fixed |
| 7 | Medium | `src/E128.Analyzers/LoopCount.cs` | Accepted a private, static, or `long` `Count`, so the fix could fail to compile | Fixed |
| 8 | Medium | `src/E128.Analyzers/Performance/PresizeCollectionAnalyzer.cs` | `Queue` and `Stack` were dead configuration, and no capacity constructor was checked | Fixed |
| 9 | Medium | `src/E128.Analyzers/Performance/StringBuilderCapacityAnalyzer.cs` | False positive when `Capacity` is set in an object initializer | Fixed |
| 10 | Medium | `src/E128.Analyzers/Performance/StringBuilderCapacityAnalyzer.cs` | Counted an append inside a lambda or local function | Fixed |
| 11 | Medium | `src/E128.Analyzers/Performance/UnboundedChannelAnalyzer.cs` | Missed `CreateUnboundedPrioritized` | Fixed |
| 12 | Medium | `src/E128.Analyzers/Performance/HandlerNoDecompressionAnalyzer.cs` | False positive on a field initializer configured in a constructor | Fixed |
| 13 | Medium | `src/E128.Analyzers/Performance/LargeStructByValueAnalyzer.cs` | A nullable large struct parameter was never reported | Fixed |
| 14 | Medium | `src/E128.Analyzers/Performance/LargeStructByValueAnalyzer.cs` | Fixed-buffer size multiplied a pointer size by the element count | Fixed |
| 15 | Medium | `src/E128.Analyzers/Performance/LargeStructByValueAnalyzer.cs` | Explicit-layout structs summed overlapping fields | Fixed |
| 16 | Medium | `src/E128.Analyzers/Performance/LargeStructByValueAnalyzer.cs` | Interface scan ran before the cheap size gate | Partly fixed |
| 17 | Medium | `src/E128.Analyzers/Performance/ReflectionLookupInLoopAnalyzer.cs` | A `while` loop with a per-iteration key was reported | Fixed |
| 18 | Medium | `src/E128.Analyzers/Performance/PollingLoopNoTokenAnalyzer.cs` | Flagged the cancellation-aware polling loop | Fixed |
| 19 | Medium | `src/E128.Analyzers/Reliability/UnboundedTaskWhenAllAnalyzer.cs` | Flagged a bounded `for` loop and treated any `WaitAsync` as a throttle | Fixed |
| 20 | Medium | `src/E128.Analyzers/Reliability/UnboundedTaskWhenAllAnalyzer.cs` | A `WhenAll` in a nested block missed the outer fill loop | Fixed |
| 21 | Medium | `src/E128.Analyzers/PublicAPI.Unshipped.txt` | 17 new public types had no API entry | Fixed |

## Phases

1. High fixes. Recursion guard, target list, constructor gate.
2. Shared-helper fixes. `LoopContext` and `LoopCount`.
3. Rule fixes. E128205 to E128217 plus E128037.
4. Tests. One regression test per fixed behaviour.
5. Docs. README, lode, release notes.
6. Gate. `scripts/check.sh`.
