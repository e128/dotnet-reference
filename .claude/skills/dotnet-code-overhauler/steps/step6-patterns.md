# Concurrency Review — Grep Patterns & Checklist

Reference file for Step 6 of dotnet-code-overhauler. Contains anti-pattern grep patterns
and analysis checklist previously embedded in the `dotnet-concurrency-specialist` agent.

## Anti-Pattern Grep Patterns

```
# Sync-over-async (deadlock risk)
\.Result\b                       # .Result property access (not .Results, .ResultSet)
\.Wait\(\)                       # Task.Wait()
\.GetAwaiter\(\)\.GetResult\(\)  # sync bridge

# Async void (unobservable exceptions)
async\s+void\s+(?!Main)         # async void (except Main entry point)

# Fire-and-forget (lost exceptions)
Task\.Run\(.*\);                 # Task.Run without await (check context)
_\s*=\s*.*Async\(               # discard pattern on async call

# Shared mutable state
static\s+(?!readonly|const|event)\w+.*=   # mutable static field
ConcurrentDictionary.*\[.*\]\s*=          # read-modify-write on concurrent dict (not atomic)

# Timing assumptions (flaky test indicators)
Thread\.Sleep\(                  # blocking sleep
Task\.Delay\(.*\d{3,}           # delays >100ms used as synchronization

# Non-concurrent collections as shared state
new\s+(Dictionary|List|HashSet)<  # may be unsafe if shared (trace usage)

# Synchronization primitives (map acquisition order for deadlock analysis)
lock\s*\(                        # lock statement
SemaphoreSlim|Semaphore          # semaphore usage
Monitor\.(Enter|Exit|TryEnter)   # explicit monitor
ReaderWriterLockSlim             # RW lock
Mutex|AutoResetEvent|ManualResetEvent  # kernel-mode primitives

# DI registration (determine sharing scope)
AddSingleton|AddScoped|AddTransient

# CancellationToken gaps
async\s+Task.*\((?!.*CancellationToken)  # async method without CancellationToken parameter

# Channel and async enumerable (check for disposal and completion)
Channel<|ChannelReader|ChannelWriter
IAsyncEnumerable|IAsyncEnumerator
```

## Analysis Checklist

| # | Check | Severity |
|---|-------|----------|
| 1 | async void methods (except event handlers) | CRITICAL |
| 2 | Sync-over-async in request/UI context (.Result, .Wait()) | CRITICAL |
| 3 | Shared mutable state without synchronization | CRITICAL |
| 4 | Read-modify-write on concurrent collections (not atomic) | HIGH |
| 5 | Check-then-act / TOCTOU (ContainsKey then Add, File.Exists then Open) | HIGH |
| 6 | Lock ordering inconsistency across methods | HIGH |
| 7 | Fire-and-forget tasks swallowing exceptions | HIGH |
| 8 | Disposal race — using an object another thread may dispose | HIGH |
| 9 | Missing CancellationToken propagation in async chains | MEDIUM |
| 10 | Event handler registration/deregistration without lock | MEDIUM |
| 11 | Timer callback overlap (re-entrant before previous completes) | MEDIUM |
| 12 | IAsyncDisposable used with `using` instead of `await using` | MEDIUM |
| 13 | Missing volatile/Interlocked for simple flags across threads | MEDIUM |
| 14 | Lazy<T> with LazyThreadSafetyMode.None in shared context | LOW |
| 15 | Channel not completed (writer never calls Complete()) | LOW |

## Severity Definitions

- **CRITICAL**: Data corruption, deadlock, or crash under concurrent access. Fix before production.
- **HIGH**: Race window reachable under normal load. Will manifest as intermittent bugs.
- **MEDIUM**: Incorrect pattern that causes issues under specific conditions (high load, cancellation, disposal timing).
- **LOW**: Defensive improvement. Unlikely to cause issues in current usage.

## Domain Knowledge

Check `${CLAUDE_SKILL_DIR}/lessons/*.md` for project-specific false positives and known-good patterns.

### DI Lifetime Reference

| Lifetime | Thread safety requirement |
|----------|--------------------------|
| Singleton | Must be fully thread-safe — all mutable state needs synchronization |
| Scoped | Safe in single-request contexts; unsafe if parallelized within a scope |
| Transient | Safe — new instance per use |

### Analysis Constraints

- Trace the full access path before calling something a race — confirm multiple threads actually reach it
- Distinguish theoretical from practical races (is the code actually used concurrently?)
- Check DI lifetime before flagging mutable state
- Prefer lock-free patterns (Interlocked, immutable state, concurrent collections) over adding locks
