# Performance Candidate Sweep
*Updated: 2026-11-07T17:31:32Z*

Source: twenty software performance practices supplied on 2026-11-07. The probe
compiled one snippet per candidate shape against the installed analyzer set:
AsyncFixer, Meziantou, Roslynator, SharpSource, Sonar, VS Threading, and E128.

A rule disabled by default stays silent in the probe. The package catalogs and
[redundant-rules.md](analyzers/redundant-rules.md) closed that gap.

Fifteen shapes ship as rules. The E128037 extension also ships. The detection
shape for each shipped rule lives in
[rule-details-performance.md](analyzers/rule-details-performance.md).

## Shipped Rules

| ID      | Rule                                              | Practice | Code fix |
| ------- | ------------------------------------------------- | -------- | -------- |
| E128203 | HTTP request inside a loop                        | 7        | No       |
| E128204 | Database command inside a loop                    | 7        | No       |
| E128205 | Collection filled in a loop with a known count    | 10       | Yes      |
| E128206 | Empty loop body                                   | 16       | No       |
| E128207 | Reflection lookup inside a loop                   | 10       | No       |
| E128208 | `ContainsKey` then indexer on a `ConcurrentDictionary` | 15  | No       |
| E128209 | `GetBytes(...).Length` inside a loop              | 10       | Yes      |
| E128210 | `new Regex` inside a loop                         | 10       | No       |
| E128211 | `Task.Delay` in a loop with no `CancellationToken` | 16      | No       |
| E128212 | `ToCharArray` used only to index or iterate       | 12       | No       |
| E128213 | `Activator.CreateInstance` inside a loop          | 10       | No       |
| E128214 | HTTP handler with no `AutomaticDecompression`     | 9        | No       |
| E128215 | `new StringBuilder()` then a known append count   | 10       | No       |
| E128216 | `Channel.CreateUnbounded` with no bound           | 14       | No       |
| E128217 | Struct above 24 bytes passed by value             | 12       | No       |

Scope notes:

- E128204 reads the async methods only. E128093 owns the synchronous shape.
- E128205 reads the statement that follows the declaration. A loop with a
  `break` that exits this loop stays silent, and so does a capacity set in an
  object initializer. `SortedSet` and `SortedDictionary` stay silent because
  neither has an `(int)` constructor. `Queue` and `Stack` use `Enqueue` and
  `Push`.
- E128206 reads an empty body only. A condition that calls a method stays silent.
- E128207 reads a loop-invariant receiver only. `item.GetType()` stays silent, and
  so does a call in the receiver or the argument. A literal-bound `for` loop
  stays silent.
- E128211 reads a `Task.Delay` overload with no `CancellationToken` parameter. A
  loop condition that reads a `CancellationToken` stays silent.
- E128212 reads the direct `foreach` source or element-access receiver only.
- E128213 skips a `for` loop with a literal bound.
- E128214 skips a derived handler type and a field or property initializer.
- E128215 reads the statement that follows the declaration, shared with E128205.
  An append inside a lambda or local function stays silent, and so does a
  capacity set in an object initializer.
- E128216 also reads `CreateUnboundedPrioritized`.
- E128217 reads a source-declared struct only, and skips overrides, interface
  members, and explicit-layout structs. The size stops at a depth cap, so a
  self-referential struct cannot recurse without end.

## Shipped Extension

`Task.Run` fan-out: a `Task.Run` call added to a task list inside a loop, then
awaited with `Task.WhenAll`. E128037 reports this shape. A `SemaphoreSlim.WaitAsync`
throttle in the lambda clears the report. A `Task.WaitAsync` timeout does not. A
loop with a numeric literal bound clears the report.

## Practice Map

| Practice | Owner |
| -------- | ----- |
| 1 Asymptotic complexity | E128066, E128068, E128069, E128152, E128153 |
| 2 Non-blocking flow | E128008, E128028, E128036, E128092, E128093, E128190, AsyncFixer02, MA0042 |
| 3 CQRS | No static shape |
| 4 Statelessness | No static shape |
| 5 Circuit breakers | E128040 checks the limit value. Registration is a composition-root choice |
| 6 Cache tiers | No static shape |
| 7 Batch and N+1 | E128064, E128180, E128185, E128203, E128204 |
| 8 Sharding | No static shape |
| 9 Compression | E128214 |
| 10 Allocations | E128018, E128067, E128081, E128083, E128084, E128085, E128109, E128205, E128207, E128209, E128210, E128213, E128215 |
| 11 Cache locality | No static shape |
| 12 Copying | E128058, E128060, E128129, E128212, E128217 |
| 13 Hardware alignment | No static shape |
| 14 Bounded concurrency | E128037, E128040, E128187, E128216. E128037 covers the `Select` and `Task.Run` shapes |
| 15 Lock-free | E128208. CA2002 covers weak-identity locks |
| 16 Busy waiting | E128206, E128211 |
| 17 Measure first | Process |
| 18 Regression testing | Process |
| 19 Premature optimization | E128116, E128120 |
| 20 Critical path | Process |

## Tier 2 Candidates

None open. All seven Tier 2 candidates shipped as E128211 to E128217.

## Checked And Rejected

| Shape | Owner |
| ----- | ----- |
| `Thread.Sleep` in an async method | AsyncFixer02, MA0042, SS032 |
| Sync `Stream.Read` in an async method | MA0042, S6966, SS033 |
| `Enum.Parse` in a loop | E128089 |
| Sync file and database I/O | E128092, E128093 |
| Entity Framework query and save in a loop | E128180, E128185 |
| `Task.WhenAll` over an async `Select` | E128037 |
| `GC.Collect` call | S1215 |
| `JsonSerializerOptions` per call | CA1869 |
| `Substring` where a span slice works | CA1846 |
| Lock on a weak-identity object | CA2002 |
| False sharing and cache-line padding | Not visible to the compiler |

## Not Analyzer Work

| Practice | Reason |
| -------- | ------ |
| 3 CQRS | Layer split, not a code shape |
| 4 Statelessness | Deployment property |
| 6 Cache tiers | Composition-root choice |
| 8 Sharding | Schema and key choice |
| 17 Measure first | Process |
| 18 Regression testing | Benchmark project and CI job |
| 20 Critical path | Process |

Practice 5 sits between. E128040 checks the limit value. The resilience handler
registration is a composition-root choice.

## Related

- [analyzer-candidates.md](analyzer-candidates.md): the review-driven catalog
- [rule-details-performance.md](analyzers/rule-details-performance.md): detection shape for E128203 to E128210
- [rule-details-performance-tier2.md](analyzers/rule-details-performance-tier2.md): detection shape for E128211 to E128217
- [analyzers.md](analyzers.md): the deny-by-default strategy
