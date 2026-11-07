# Mr. Sparkle Tasks: performance analyzers
*Updated: 2026-11-07T17:38:37Z*

- [x] `src/E128.Analyzers/Performance/LargeStructByValueAnalyzer.cs:163` Unbounded recursion. Fix: visited set, depth cap, early exit. Severity: High. Source: adversarial-security, code-reviewer
- [x] `src/E128.Analyzers/Performance/PresizeCollectionAnalyzer.cs:32` `SortedSet` and `SortedDictionary` have no `(int)` constructor. Fix: drop both, gate on a public `(int)` constructor. Severity: High. Source: adversarial-functionality, adversarial-quality, code-reviewer
- [x] `src/E128.Analyzers/Performance/RegexInstanceInLoopAnalyzer.cs:73` Constant pattern in a loop reported by no rule. Fix: report it. Severity: Medium. Source: code-reviewer, adversarial-functionality
- [x] `src/E128.Analyzers/LoopContext.cs:21` Deconstruction `foreach` not recognised. Fix: use `CommonForEachStatementSyntax`. Severity: Medium. Source: adversarial-functionality, adversarial-quality
- [x] `src/E128.Analyzers/LoopContext.cs:156` `HasConstantBound` too broad. Fix: nearest loop, numeric bound side. Severity: Medium. Source: adversarial-functionality, adversarial-quality
- [x] `src/E128.Analyzers/Performance/ConcurrentDictionaryDoubleLookupAnalyzer.cs:114` Fired on an indexer write and missed literal keys. Fix: skip writes, compare constants. Severity: Medium. Source: code-reviewer, adversarial-functionality
- [x] `src/E128.Analyzers/LoopCount.cs:108` `Count` accessibility and type unchecked. Fix: public instance `int` only. Severity: Medium. Source: code-reviewer
- [x] `src/E128.Analyzers/Performance/PresizeCollectionAnalyzer.cs:33` `Queue` and `Stack` never fill through `Add`. Fix: per-type fill method. Severity: Medium. Source: adversarial-functionality
- [x] `src/E128.Analyzers/Performance/StringBuilderCapacityAnalyzer.cs:62` Initializer `Capacity` ignored. Fix: skip the initializer. Severity: Medium. Source: code-reviewer, adversarial-functionality
- [x] `src/E128.Analyzers/Performance/StringBuilderCapacityAnalyzer.cs:121` Lambda append counted. Fix: skip lambda and local function subtrees. Severity: Medium. Source: code-reviewer
- [x] `src/E128.Analyzers/Performance/UnboundedChannelAnalyzer.cs:53` Missed `CreateUnboundedPrioritized`. Fix: match both factories. Severity: Medium. Source: code-reviewer
- [x] `src/E128.Analyzers/Performance/HandlerNoDecompressionAnalyzer.cs:129` Field initializer false positive. Fix: stay silent for a non-local target. Severity: Medium. Source: code-reviewer, adversarial-functionality
- [x] `src/E128.Analyzers/Performance/LargeStructByValueAnalyzer.cs:63` Nullable struct parameter never reported. Fix: unwrap `Nullable<T>`. Severity: Medium. Source: adversarial-functionality
- [x] `src/E128.Analyzers/Performance/LargeStructByValueAnalyzer.cs:224` Fixed-buffer size wrong. Fix: element size times count. Severity: Medium. Source: adversarial-functionality
- [x] `src/E128.Analyzers/Performance/LargeStructByValueAnalyzer.cs:180` Explicit layout summed overlapping fields. Fix: skip explicit layout. Severity: Medium. Source: adversarial-functionality
- [x] `src/E128.Analyzers/Performance/LargeStructByValueAnalyzer.cs:60` Interface scan before the size gate. Fix: size gate first. Severity: Medium. Source: adversarial-quality
- [x] `src/E128.Analyzers/Performance/ReflectionLookupInLoopAnalyzer.cs:86` `while` loop with a per-iteration key reported. Fix: treat a nested call and a body-declared local as varying. Severity: Medium. Source: code-reviewer, adversarial-functionality
- [x] `src/E128.Analyzers/Performance/PollingLoopNoTokenAnalyzer.cs:76` Cancellation-aware condition reported. Fix: skip when the condition reads a token. Severity: Medium. Source: code-reviewer, adversarial-functionality
- [x] `src/E128.Analyzers/Reliability/UnboundedTaskWhenAllAnalyzer.cs:130` Bounded loop and timeout `WaitAsync` treated as unbounded. Fix: skip a literal-bound loop, require `SemaphoreSlim.WaitAsync`. Severity: Medium. Source: code-reviewer, adversarial-functionality
- [x] `src/E128.Analyzers/Reliability/UnboundedTaskWhenAllAnalyzer.cs:114` Nested-block `WhenAll` missed the outer loop. Fix: walk blocks outward. Severity: Low. Source: code-reviewer
- [x] `src/E128.Analyzers/PublicAPI.Unshipped.txt` 17 public types missing. Fix: add type and constructor pairs. Severity: Medium. Source: adversarial-quality
- [x] Docs: rule count, E128205 types, E128207 exclusions, E128210 constant pattern, E128211 condition, E128214 caveat, E128215 exclusions, E128216 factory, E128217 limits. Severity: Medium. Source: code-reviewer, adversarial-functionality
