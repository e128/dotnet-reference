# .NET Performance
*Updated: 2026-10-25T02:21:36Z*

Performance guidance for the app projects. The source is the BlazingFastDotnet
notes (`github.com/EgorBo/BlazingFastDotnet`). The notes warn that some advice
dates quickly. Apply a tip only after a measurement shows a problem.

## Scope

The guidance applies to `E128.Reference.Web`, `E128.Reference.Cli`, and
`E128.Reference.Core`. The `E128.Analyzers` project targets `netstandard2.0`
for Roslyn. Most runtime tips do not apply there. See
[project-structure.md](project-structure.md).

## Settings Already In Place

`Directory.Build.props` owns the build-level tuning. Do not copy a value from
it into a project file. It sets these:

- `ReadyToRun` and tiered compilation in Release, to reduce startup latency.
- `InvariantGlobalization` true in Release, so the app does not load ICU.
- `IlcFoldIdenticalMethodBodies` true, to fold identical method bodies.
- `OptimizationPreference` Size, `TrimMode` link, and
  `InvariantGlobalization` true when a project sets `PublishAot`.

No project sets `PublishAot` today. A project that needs an AOT binary sets
that one property and inherits the rest.

Invariant globalization is the default. The app must not depend on
culture-specific formatting or comparison. Pass `CultureInfo.InvariantCulture`
to a format call and use an ordinal comparison. A culture-sensitive call is
wrong here, not only slow. The notes name CA1307, CA1309, and CA1310 as the
rules for this pattern.

## Runtime Knobs

Set a `DOTNET_` variable only to solve a measured problem. State the reason in
a comment next to the setting.

- `DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS=1` runs I/O continuations on
  poller threads. Use it for many small requests.
- `DOTNET_gcServer=1` selects server GC. Use it for a console tool that
  allocates heavily.
- `DOTNET_GCDynamicAdaptationMode=0` disables DATAS for server GC. It can
  raise throughput and cost memory.
- `DOTNET_GCDGen0GrowthPercent` and `DOTNET_GCDTargetTCP` tune DATAS.
- `DOTNET_ThreadPool_UnfairSemaphoreSpinLimit` trades wake latency against
  spinning.
- `DOTNET_SYSTEM_NET_SOCKETS_THREAD_COUNT` sets the poller count.
- `DOTNET_TieredCompilation=0` and `DOTNET_TC_CallCountingDelayMs=0` help a
  short-lived tool that pays JIT warmup.

An AOT binary has no JIT and no tiers. The CoreCLR variables do not apply to
it.

## Profiling Ladder

Start cheap. Go deeper only when the cheap step does not answer the question.

1. Take a first look with counters:
   `dotnet-counters monitor -p <pid>`.
2. Follow a call path with a trace:
   `dotnet-trace collect -p <pid>`. Convert the trace to Speedscope to read it.
3. On Linux, use `perf` for a native view. Set `DOTNET_PerfMapEnabled=1` first.
   Then run `perf record -k 1 -g -p <pid>`, `perf inject --jit`, and
   `perf report`. Set `DOTNET_PerfMapStubGranularity=3` to keep the symbol map
   small.
4. Read the generated code for one method. Set `DOTNET_JitDisasm="<Method>"`
   and `DOTNET_JitStdOutFile=<path>`. Read the Tier1 result, because Tier0 is
   unoptimized. Dynamic PGO changes the result, so measure a steady state.

The common low-level causes are cache-line contention, a bounds check the JIT
cannot remove, poor locality, and code alignment noise.

## Safe C# Tips

The notes cover safe C# only and discourage unsafe code. This repo also
requires approval for any analyzer suppression. A tip that needs a `#pragma`
waits for that approval.

- Serialize JSON with a source-generated `JsonTypeInfo<T>`. Avoid reflective
  metadata discovery.
- Log with `[LoggerMessage]`. Check `IsEnabled` before you build an expensive
  argument.
- Match a known pattern with `[GeneratedRegex]` on a partial property.
- Use the span bulk APIs (`CopyTo`, `Fill`, `SequenceEqual`, `IndexOf`)
  instead of a handwritten loop.
- Use a static lambda in a hot path and the overload that takes state. A
  capturing lambda allocates a closure.
- Parse and format UTF-8 bytes with the `ReadOnlySpan<byte>` overloads.
- Cache `SearchValues<T>` in a `static readonly` field. Never build one per
  call.
- Use `FrozenDictionary` or `FrozenSet` for data built once and read many
  times.
- Read a string-keyed dictionary from a span with `TryGetAlternateLookup`.
  This avoids a temporary string key.
- Give a struct dictionary key `IEquatable<T>` and a good `GetHashCode`.
- Try `TensorPrimitives` before hand-written vectorization.
- Prefer `[LibraryImport]` over `[DllImport]`. The generator needs
  `AllowUnsafeBlocks`, not user-written unsafe code.
- Cache a hot field in a local inside a loop. The JIT often cannot hoist the
  field read.
- Use `[SuppressGCTransition]` only on a tiny native call that does no I/O,
  takes no lock, and does not throw.

The span loop shape below lets .NET 11 remove the bounds check when the span
holds at least one vector width:

```csharp
while (data.Length >= Vector128<int>.Count)
{
    sum += Vector128.Create(data);
    data = data.Slice(Vector128<int>.Count);
}
```

For an equality test, use `a.SequenceEqual(b)`. Do not write that loop.

Culture-sensitive by default: `string.Compare`, `StartsWith(string)`,
`ToUpper`, `ToLower`, and default sorting. Ordinal by default: `==`,
`Equals`, `Contains(string)`, `Replace`, `IndexOf(char)`, and default
dictionary hashing. Compare with `StringComparison.OrdinalIgnoreCase` instead
of lowercasing both sides.

A `Dictionary<string, T>` keeps fast hashing only with the default comparer,
`StringComparer.Ordinal`, or `StringComparer.OrdinalIgnoreCase`.

## Contention

A shared write serializes on one cache line. Split the state per core, then
combine it at read time. `Parallel.For` with `localInit` and `localFinally` is
often cheaper than a shared counter. `ThreadLocal<T>` with
`trackAllValues: true` suits a bounded set of long-lived threads.

Correctness comes before the split. `InMemoryGreetingRepository` is a
singleton, so one lock guards both the write and the read. A read walks back
from the end of the list, so the cost follows the requested count and not the
stored count. Remove the guard only after a different structure replaces the
shared list. See [test-patterns.md](test-patterns.md).

An earlier read sorted a full copy of the list on every call. At 50,000 stored
greetings that read allocated 1.9 GB per second and reached 1,655 requests per
second. The current read allocates 0.11 GB per second and reaches 7,278
requests per second on the same harness. Treat a full sort on a read path as a
defect until a measurement says otherwise.

False sharing happens when two hot fields share a cache line. Pad the hot slot
to the line size. An x64 or Neoverse arm64 line is 64 bytes. An Apple M-series
line is 128 bytes. Padding costs a field and a suppression, so add it only
after a measurement.

## ASP.NET Core

- `WebApplication.CreateBuilder` loads more than the app needs.
  `CreateSlimBuilder` reduces the set. Measure the startup and the request
  path before a switch.
- Keep the middleware count small.
- Avoid an Information-level host log per request. Raise the level or clear
  the providers.
- Register the source-generated JSON metadata.
- Reuse one `Utf8JsonWriter` through `Reset(response.BodyWriter)` for a
  handwritten response.

`JavaScriptEncoder.UnsafeRelaxedJsonEscaping` is safe only for a JSON
response. Never place its output in HTML or a script block.

Both `CreateSlimBuilder` and the source-generated JSON metadata were measured
on this app. Neither moved `GET /` or `GET /greetings` beyond the run-to-run
noise of about 2 percent. Cold start moved by 4 percent, which is inside the
same noise. Adopt either for another reason, such as AOT readiness, and not as
a latency win.

## Verification

A performance change needs a measurement before and after it. Record the
command and both results in the pull request. See
[testing.md](testing.md) for the test entry point.

The reference app was measured on a Release publish over loopback. Load came
from four parallel `ab` jobs at `-c 20`. Service time came from `ab -c 1`.
Seeding used `POST /greetings`. Run a change three times and compare medians,
because run-to-run noise reaches 2 percent.
