# .NET Performance
*Updated: 2026-10-31T16:06:37Z*

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
- Prefer a `MemoryExtensions` method (`SequenceEqual`, `IndexOf`,
  `IndexOfAnyExcept`, `LastIndexOf`, `CommonPrefixLength`) over a manual
  loop. The library method is already vectorized.
- Use `BitOperations`, `BinaryPrimitives`, `BitConverter`, and the numeric
  helpers (`uint.Log2`, `PopCount`, `TrailingZeroCount`) for bit work. Do
  not write the shift loop.
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
  field read. A local also enables a bounds check removal on an array.
- Use a `static readonly` field for immutable process-wide configuration.
  The JIT can fold the value and remove a disabled branch.
- Embed immutable primitive data as an RVA-backed span:
  `static ReadOnlySpan<byte> Data => [1, 2, 3];`. The runtime stores the data
  in metadata, so a read is a constant.
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

For ASCII data, use `System.Text.Ascii`. Its `EqualsIgnoreCase`, `ToUpper`,
`ToLower`, and `IsValid` are vectorized and take a span.

When the request body and the response body are both UTF-8, stay in UTF-8.
Search, parse, and format a `ReadOnlySpan<byte>` directly. Use a `u8` literal,
`SearchValues<byte>`, the byte `TryParse` and `TryFormat` overloads, and
`Utf8.TryWrite`. Convert to a string only at the last step.

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
- Keep the host log level above Information. `Program.cs` sets the minimum
  level to Warning. See [Request Logging](#request-logging).
- Register the source-generated JSON metadata. `AppJsonContext` in the Web
  project owns it, and `Program.cs` inserts its resolver at the head of the
  resolver chain. Register a new response type in that context.
- Reuse one `Utf8JsonWriter` through `Reset(response.BodyWriter)` for a
  handwritten response.

`JavaScriptEncoder.UnsafeRelaxedJsonEscaping` is safe only for a JSON
response. Never place its output in HTML or a script block. A custom encoder
also disables the source-generated serialization fast path. Benchmark the
pair before a switch, because the two gains can cancel.

Both `CreateSlimBuilder` and the source-generated JSON metadata were measured
on this app. Neither moved `GET /` or `GET /greetings` beyond the run-to-run
noise of about 2 percent. Cold start moved by 4 percent, which is inside the
same noise. The app keeps the generated context for AOT readiness and does not
claim a latency win. `CreateSlimBuilder` is not in use.

## Request Logging

`Program.cs` sets the minimum log level to Warning. A per-request Information
log costs a console write and a monitor lock on the request thread.

On a Release publish over loopback, the change moved the median service time
on `GET /` from 0.072 ms to 0.062 ms at `-c 1`. It removed two log lines per
request. Throughput at `-c 20` stayed inside the noise.

## Change Protocol

Apply one performance change at a time. Keep the change only on a measured
win. This loop applies to every new app path, not only to a fix.

1. Profile the running process with `dotnet-counters` before you guess at a
   cause.
2. Publish both builds with `dotnet publish -c Release`. A Debug build measures
   unoptimized code, and no script publishes Release today.
3. Run the two builds on separate ports and interleave the load runs.
4. Compare the median of three or more runs. Discard a change that sits inside
   the run-to-run noise.

Read two numbers. A loopback run at `-c 20` reports throughput, but it swings
by 30 percent on a busy machine. A run at `-c 1` reports serial service time
and is stable enough to compare a CPU change. Prefer the `-c 1` number.

## Verification

A performance change needs a measurement before and after it. Record the
command and both results in the pull request. Follow the change protocol
above. See [testing.md](testing.md) for the test entry point.

The reference app was measured on a Release publish over loopback. Load came
from four parallel `ab` jobs at `-c 20`. Service time came from `ab -c 1`.
Seeding used `POST /greetings`. Run a change three times and compare medians,
because run-to-run noise reaches 2 percent.
