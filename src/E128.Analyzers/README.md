# E128.Analyzers

Roslyn analyzers and code fixes that enforce opinionated .NET conventions at compile time. Catch design, reliability, performance, and style issues before they reach code review.

## Installation

```xml
<PackageReference Include="E128.Analyzers" Version="1.20.0" PrivateAssets="all" />
```

> `PrivateAssets="all"` keeps the analyzers out of your consumers' dependency graph.

## Rules

All rules default to **Warning** severity unless noted. Every rule includes a code fix unless noted.

### Design

| Rule    | Title                                                                                               | Code Fix |
| ------- | --------------------------------------------------------------------------------------------------- | -------- |
| E128001 | Use `FileInfo`/`DirectoryInfo` instead of `string` paths                                            | Yes      |
| E128003 | Use `TimeProvider` instead of `DateTime.Now` / `DateTimeOffset.Now`                                 | Yes      |
| E128004 | Use `IHttpClientFactory` instead of `new HttpClient()`                                              | Yes      |
| E128005 | Seal classes that have no subclasses                                                                | Yes      |
| E128007 | Avoid `async void` methods (non-event-handler)                                                      | Yes      |
| E128008 | Avoid sync-over-async (`.Result` / `.GetAwaiter().GetResult()`)                                     | Yes      |
| E128017 | Use primary constructor parameter directly                                                          | Yes      |
| E128019 | Do not pass `CancellationToken` by `in` reference                                                   | Yes      |
| E128021 | Do not use `in` modifier with ref struct parameters (default: Error)                                | Yes      |
| E128022 | Remove `ConfigureAwait(false)` in apps without `SynchronizationContext`                              | Yes      |
| E128030 | Do not compare `FileSystemInfo` types by reference (default: Info)                                   | Yes      |
| E128032 | Concrete-only DI registration with available interface                                              | Yes      |
| E128036 | `Task.Run` wrapping async lambda — unnecessary thread pool hop                                      | Yes      |
| E128042 | `Convert.ToInt32`/`ToInt64` wrapping `ExecuteScalar` without null guard                             | Yes      |
| E128044 | Type implements `IAsyncDisposable` but not `IDisposable`                                            | Yes      |
| E128045 | Avoid direct `System.Console` usage                                                                 | No       |
| E128046 | Class has excessive user-defined inheritance depth                                                   | No       |
| E128048 | Use `switch` instead of if/else-if chain on enum values                                             | Yes      |
| E128049 | Avoid `[DynamicallyAccessedMembers]` — suppress with justification if required                      | Yes      |
| E128050 | Use `TimeSpan` for time-duration values to avoid unit ambiguity (default: Error)                    | Yes      |
| E128052 | Use immutable collection interface instead of mutable concrete type (default: Info)                  | Yes      |
| E128053 | Use collection of `FileInfo`/`DirectoryInfo` instead of collection of `string` for file system paths | Yes      |
| E128058 | Return `List<T>` via `.AsReadOnly()` when exposing as `IReadOnlyList<T>`                              | Yes      |
| E128059 | Interface method parameter is unused in implementation                                                | Yes      |
| E128060 | Return `Dictionary<K,V>` via `.AsReadOnly()` when exposing as `IReadOnlyDictionary<K,V>`              | Yes      |
| E128061 | Use `ImmutableArray<T>` for static readonly arrays                                                   | Yes      |

### Reliability

| Rule    | Title                                                                                    | Code Fix |
| ------- | ---------------------------------------------------------------------------------------- | -------- |
| E128011 | `[GeneratedRegex]` missing `matchTimeoutMilliseconds`                                    | Yes      |
| E128012 | `RegexOptions.Compiled` is redundant in `[GeneratedRegex]`                               | Yes      |
| E128013 | `[GeneratedRegex]` pattern has overlapping quantifiers                                   | Yes      |
| E128014 | `[GeneratedRegex]` pattern has nested quantifiers                                        | Yes      |
| E128016 | `DateTime.Parse`/`ParseExact` missing `DateTimeStyles` parameter                         | Yes      |
| E128020 | Do not use `in` modifier with mutable structs                                            | Yes      |
| E128023 | Avoid hardcoded `/tmp` path                                                              | Yes      |
| E128028 | `Task.FromResult` wraps sync I/O that has an async alternative                           | Yes      |
| E128031 | `AddSingleton` factory returns `IDisposable`                                             | Yes      |
| E128033 | Options class bound via `.Bind()` has init-only property                                 | Yes      |
| E128034 | Constructor `new`s a DI-registered type — inject via DI instead                          | Yes      |
| E128035 | Concrete-type DI dependency without direct registration                                  | Yes      |
| E128037 | Unbounded `Task.WhenAll` over async `Select`                                             | Yes      |
| E128038 | `Task.WhenAll` async lambda missing `CancellationToken` propagation                      | Yes      |
| E128039 | Catch filter must exclude `OperationCanceledException`                                   | Yes      |
| E128040 | Concurrency limit must be positive                                                       | Yes      |
| E128041 | `JsonDocument.RootElement` must not escape the document's `using` scope                   | Yes      |
| E128051 | Broad catch in async `HttpClient` method missing `OperationCanceledException` handler    | No       |
| E128056 | `FileInfo.Exists` TOCTOU race condition                                                  | Yes      |
| E128057 | Unprotected cleanup in finally block                                                     | Yes      |
| E128064 | Disk write-then-read round-trip — use the in-memory value instead of reading back         | Yes      |

### Performance

| Rule    | Title                                                                 | Code Fix |
| ------- | --------------------------------------------------------------------- | -------- |
| E128009 | Use `MinBy`/`MaxBy` instead of `OrderBy().First()`                    | Yes      |
| E128010 | Pass `HttpCompletionOption.ResponseHeadersRead` to `HttpClient` calls | Yes      |
| E128015 | Use string interpolation instead of `string.Format`                   | Yes      |
| E128018 | Use `ToArray()` instead of `ToList()` for read-only `foreach`         | Yes      |
| E128026 | Redundant `HashSet` allocation in `FrozenSet` creation                | Yes      |
| E128027 | Use `FrozenSet`/`FrozenDictionary` for static readonly collections    | Yes      |
| E128029 | Replace multi-string OR-chain with `HashSet.Contains`                 | Yes      |

### Style

| Rule    | Title                                                                     | Code Fix |
| ------- | ------------------------------------------------------------------------- | -------- |
| E128002 | Use `string.Empty` instead of `""`                                        | Yes      |
| E128006 | Use `Encoding.UTF8` instead of `Encoding.Default`                         | Yes      |
| E128024 | Non-XML-doc comment above method declaration                              | Yes      |
| E128025 | Use `Path.GetRandomFileName()` instead of `Guid.NewGuid()` in temp paths  | Yes      |
| E128043 | Do not use the null-forgiving operator                                    | Yes      |
| E128047 | `#pragma warning disable` without justification comment                   | Yes      |
| E128055 | Unbalanced `#pragma warning disable` without matching restore             | Yes      |
| E128063 | Mid-name underscore in private static member (IDE1006 rename artifact) (default: Error) | Yes      |

### Testing

| Rule    | Title                                                                     | Code Fix |
| ------- | ------------------------------------------------------------------------- | -------- |
| E128054 | Class creates temp directory without cleanup interface                     | Yes      |
| E128062 | Test uses outdated `ReferenceAssemblies` — does not match project TFM      | Yes      |

## What each rule catches

### E128001 &mdash; File system path types

Flags `string` parameters, `Option<string>`, and `Argument<string>` that look like file system paths (by name or usage). Suggests `FileInfo` or `DirectoryInfo` instead.

```csharp
// Before (warns)
void Save(string filePath) { }

// After
void Save(FileInfo filePath) { }
```

### E128003 &mdash; TimeProvider over DateTime

Flags `DateTime.Now`, `DateTime.UtcNow`, `DateTime.Today`, `DateTimeOffset.Now`, and `DateTimeOffset.UtcNow`. These are untestable &mdash; inject `TimeProvider` via DI instead.

```csharp
// Before (warns)
var now = DateTime.UtcNow;

// After — inject TimeProvider
var now = timeProvider.GetUtcNow().UtcDateTime;
```

### E128004 &mdash; IHttpClientFactory

Flags `new HttpClient()`. Direct instantiation leaks sockets and ignores DI-managed handlers.

### E128005 &mdash; Sealed by default

Flags non-abstract, non-static classes with no derived types in the compilation. Sealing enables devirtualization and communicates design intent.

```csharp
// Before (warns)
public class MyService { }

// After
public sealed class MyService { }
```

### E128007 &mdash; Async void

Flags `async void` methods that are not event handlers. Exceptions in `async void` crash the process.

```csharp
// Before (warns)
async void LoadData() { await db.QueryAsync(); }

// After
async Task LoadData() { await db.QueryAsync(); }
```

### E128008 &mdash; Sync-over-async

Flags `.Result`, `.GetAwaiter().GetResult()`, and `.Wait()` on tasks. These block the calling thread and risk deadlocks.

```csharp
// Before (warns)
var data = httpClient.GetAsync("/api").Result;

// After
var data = await httpClient.GetAsync("/api");
```

### E128009 &mdash; OrderBy + First

Flags `OrderBy(...).First()` and `OrderByDescending(...).First()` patterns. `MinBy`/`MaxBy` run in O(n) instead of O(n log n).

```csharp
// Before (warns)
var cheapest = products.OrderBy(p => p.Price).First();

// After
var cheapest = products.MinBy(p => p.Price);
```

### E128010 &mdash; HttpClient response buffering

Flags `GetAsync`, `PostAsync`, `SendAsync`, etc. called without `HttpCompletionOption.ResponseHeadersRead`. Without it, the entire response body is buffered into memory before control returns.

```csharp
// Before (warns)
var response = await client.GetAsync("/large-file");

// After
var response = await client.GetAsync("/large-file", HttpCompletionOption.ResponseHeadersRead);
```

### E128015 &mdash; String interpolation over string.Format

Flags `string.Format(...)` calls that can be replaced with string interpolation (`$"..."`). Interpolation is faster (avoids boxing and parsing) and more readable.

```csharp
// Before (warns)
var msg = string.Format("Hello, {0}!", name);

// After
var msg = $"Hello, {name}!";
```

### E128016 &mdash; DateTime.Parse roundtrip

Flags `DateTime.Parse` and `DateTime.ParseExact` calls missing a `DateTimeStyles` parameter. Without `DateTimeStyles.RoundtripKind`, parsing an ISO 8601 UTC string silently converts it to local time.

```csharp
// Before (warns)
var dt = DateTime.Parse(isoString);

// After
var dt = DateTime.Parse(isoString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
```

### E128017 &mdash; Primary constructor backing field

Flags fields that are identity assignments from primary constructor parameters. Use the parameter directly in method bodies instead of creating a redundant backing field.

```csharp
// Before (warns)
public class Service(ILogger logger)
{
    private readonly ILogger _logger = logger;
    public void Run() => _logger.LogInformation("running");
}

// After
public class Service(ILogger logger)
{
    public void Run() => logger.LogInformation("running");
}
```

### E128018 &mdash; ToList in foreach

Flags `ToList()` calls used only as the source of a `foreach` loop. `ToArray()` allocates less memory for read-only iteration.

```csharp
// Before (warns)
foreach (var item in items.ToList()) { }

// After
foreach (var item in items.ToArray()) { }
```

### E128019 &mdash; CancellationToken by `in` reference

Flags `in CancellationToken` parameters. The `in` modifier makes `CancellationToken` a by-ref parameter, which breaks reflection-based frameworks (e.g., Microsoft.Extensions.AI). `CancellationToken` is a small struct &mdash; pass it by value.

```csharp
// Before (warns)
async Task RunAsync(in CancellationToken ct) { }

// After
async Task RunAsync(CancellationToken ct) { }
```

### E128020 &mdash; `in` modifier with mutable structs

Flags `in` on mutable (non-readonly) struct parameters. The compiler creates a hidden defensive copy on every member access, silently changing behavior.

```csharp
// Before (warns)
void Process(in Batch<Activity> batch) { }

// After
void Process(Batch<Activity> batch) { }
```

### E128021 &mdash; `in` modifier with ref structs

Flags `in` on ref struct parameters (`Span<T>`, `ReadOnlySpan<T>`, etc.). Ref structs are already passed by reference &mdash; `in` is redundant and a compile error on extension method `this` parameters. Default severity: **Error**.

```csharp
// Before (error)
void Read(in ReadOnlySpan<byte> buffer) { }

// After
void Read(ReadOnlySpan<byte> buffer) { }
```

### E128022 &mdash; ConfigureAwait(false)

Flags `.ConfigureAwait(false)` in executable application code (console apps, ASP.NET Core, Worker Service). These hosts have no `SynchronizationContext`, so the call is unnecessary noise. Skips Blazor WASM projects.

```csharp
// Before (warns)
var data = await client.GetAsync("/api").ConfigureAwait(false);

// After
var data = await client.GetAsync("/api");
```

### E128023 &mdash; Hardcoded /tmp path

Flags string literals equal to `"/tmp"` or starting with `"/tmp/"` (and Windows equivalents). Use `Path.GetTempPath()` for cross-platform compatibility.

```csharp
// Before (warns)
var path = "/tmp/my-file.txt";

// After
var path = Path.Combine(Path.GetTempPath(), "my-file.txt");
```

### E128024 &mdash; Non-XML-doc comment above method

Flags `//` comments immediately preceding method or local function declarations. Use `/// <summary>` XML doc comments for documentation, or remove the comment entirely.

```csharp
// Before (warns)
// Does the thing
void DoThing() { }

// After (option 1: XML doc)
/// <summary>Does the thing.</summary>
void DoThing() { }

// After (option 2: remove)
void DoThing() { }
```

### E128025 &mdash; Guid.NewGuid() in temp paths

Flags `Guid.NewGuid()` inside string interpolation within `Path.Combine` or `Path.GetTempPath` calls. Use `Path.GetRandomFileName()` instead.

```csharp
// Before (warns)
var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tmp");

// After
var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
```

### E128026 &mdash; Redundant HashSet in FrozenSet

Flags `new HashSet<T>(...).ToFrozenSet()` &mdash; the intermediate `HashSet` allocation is unnecessary. Pass the collection directly to `ToFrozenSet()`.

```csharp
// Before (warns)
var set = new HashSet<string>(items).ToFrozenSet();

// After
var set = items.ToFrozenSet();
```

### E128027 &mdash; Static readonly frozen collections

Flags `static readonly HashSet<T>` and `static readonly Dictionary<TKey, TValue>` fields initialized at declaration. Frozen collections trade construction time for optimized read access &mdash; ideal for process-lifetime fields. Requires .NET 8+.

```csharp
// Before (warns)
private static readonly HashSet<string> AllowedMethods = new(StringComparer.Ordinal) { "GET", "POST" };

// After
private static readonly FrozenSet<string> AllowedMethods = new HashSet<string>(StringComparer.Ordinal) { "GET", "POST" }.ToFrozenSet();
```

### E128028 &mdash; Task.FromResult wrapping sync I/O

Flags methods that return `Task.FromResult` or `ValueTask.FromResult` while also calling synchronous I/O methods that have async equivalents (e.g., `File.ReadAllText` instead of `File.ReadAllTextAsync`). Does not flag early-return guards, null-object implementations, or methods that already use `await`.

```csharp
// Before (warns)
public Task<string> ReadConfig(string path)
{
    var content = File.ReadAllText(path);
    return Task.FromResult(content);
}

// After
public async Task<string> ReadConfig(string path)
{
    return await File.ReadAllTextAsync(path);
}
```

### E128029 &mdash; Multi-string OR-chain

Flags 3+ `||`-chained string equality tests on the same operand. Replace with `HashSet<string>.Contains()` for cleaner code and O(1) lookup.

```csharp
// Before (warns)
if (method == "GET" || method == "POST" || method == "PUT") { }

// After
private static readonly FrozenSet<string> AllowedMethods = ...;
if (AllowedMethods.Contains(method)) { }
```

### E128030 &mdash; FileSystemInfo reference equality

Flags `==`, `!=`, and `.Equals()` comparisons on `FileInfo` and `DirectoryInfo`. These types do not override `Equals` or `operator==`, so comparisons use reference equality from `System.Object` &mdash; two objects pointing to the same path are never equal. Compare `.FullName` instead. Default severity: **Info**.

```csharp
// Before (warns)
if (fileA == fileB) { }

// After
if (string.Equals(fileA.FullName, fileB.FullName, StringComparison.Ordinal)) { }
```

### E128031 &mdash; Disposable singleton factory

Flags `AddSingleton` factory registrations that return types implementing `IDisposable`. The DI container does not dispose singletons created via factory delegates &mdash; disposal must be handled manually or the registration restructured.

### E128032 &mdash; Concrete-only DI registration

Flags `AddScoped<ConcreteType>()` / `AddTransient<ConcreteType>()` / `AddSingleton<ConcreteType>()` when the concrete type implements a single non-marker interface. Register as the interface to enable testing and substitution.

### E128033 &mdash; Options class with init-only properties

Flags options classes bound via `.Bind()` that have `init`-only properties. The configuration binder uses reflection to set properties after construction &mdash; `init` setters are not assignable post-construction, causing silent binding failures.

### E128034 &mdash; Constructor newing a DI-registered type

Flags constructors that `new` up a type that is registered in the DI container elsewhere. Inject the dependency instead of creating it directly.

### E128035 &mdash; Concrete-type DI dependency

Flags constructor parameters typed as concrete classes (not interfaces) that have no direct DI registration. The dependency should be registered and injected via its interface.

### E128036 &mdash; Task.Run wrapping async lambda

Flags `Task.Run(async () => ...)`. For I/O-bound work, the thread pool hop is unnecessary overhead &mdash; just call the async method directly. `Task.Run` is for CPU-bound work.

### E128037 &mdash; Unbounded Task.WhenAll

Flags `Task.WhenAll(items.Select(async ...))` where the collection is unbounded. This fans out unlimited concurrent operations. Use `Parallel.ForEachAsync` or batch with a `SemaphoreSlim`.

### E128038 &mdash; Task.WhenAll missing CancellationToken

Flags `Task.WhenAll` async lambdas that capture a `CancellationToken` in scope but don't pass it to the inner async calls. Cancellation won't propagate.

### E128039 &mdash; Catch filter excluding OCE

Flags `catch (Exception ex) when (...)` filters that don't exclude `OperationCanceledException`. Swallowing OCE prevents proper cancellation propagation.

### E128040 &mdash; Concurrency limit must be positive

Flags `SemaphoreSlim` and `MaxDegreeOfParallelism` initialized with zero or negative values. A zero semaphore deadlocks immediately.

### E128041 &mdash; JsonDocument.RootElement lifetime

Flags `JsonDocument.RootElement` returned or assigned to a field/variable that escapes the `using` scope. After disposal, the `RootElement` points to freed memory.

### E128042 &mdash; ExecuteScalar null guard

Flags `Convert.ToInt32(cmd.ExecuteScalar())` and similar patterns without a null check. `ExecuteScalar` returns `null` when there are no rows, causing an `InvalidCastException`.

### E128043 &mdash; Null-forgiving operator

Flags every use of the null-forgiving operator (`!`). The operator suppresses nullable analysis without ensuring the value is non-null, hiding potential `NullReferenceException`s.

### E128044 &mdash; IAsyncDisposable without IDisposable

Flags types that implement `IAsyncDisposable` but not `IDisposable`. Consumers using synchronous `using` will silently skip disposal.

### E128045 &mdash; Direct System.Console usage

Flags direct use of `System.Console` members. Use `ILogger` for service code or `ITerminalWriter`/`ITerminalPrompt` for CLI code instead. No code fix &mdash; the replacement depends on context.

### E128046 &mdash; Excessive inheritance depth

Flags classes with more than 3 levels of user-defined inheritance (excluding framework base classes). Deep hierarchies are fragile and hard to reason about. No code fix.

### E128047 &mdash; Suppression without justification

Flags `#pragma warning disable` directives that don't have an accompanying justification comment on the same or preceding line. Every suppression should explain why.

### E128048 &mdash; Enum if/else-if chain

Flags if/else-if chains comparing the same variable against enum constants. A `switch` statement is more readable and the compiler can warn about missing cases.

### E128049 &mdash; DynamicallyAccessedMembers guard

Flags usage of `[DynamicallyAccessedMembers]` attribute. This attribute opts out of trimming safety. Suppress with a justification comment if required for `HttpClientFactory` or JSON discriminated unions.

### E128050 &mdash; TimeSpan for durations

Flags `int` or `double` parameters/properties with names like `timeout`, `delay`, `interval`, or `duration`. Use `TimeSpan` to avoid unit ambiguity (seconds vs milliseconds). Default severity: **Error**.

### E128051 &mdash; HttpClient missing OCE catch

Flags broad `catch (Exception)` blocks inside async methods that call `HttpClient` without a preceding `catch (OperationCanceledException)`. Swallowing OCE prevents proper timeout/cancellation handling. No code fix.

### E128052 &mdash; Mutable collection exposure

Flags public/protected members returning mutable concrete collection types (`List<T>`, `Dictionary<TKey, TValue>`, etc.) instead of immutable interfaces (`IReadOnlyList<T>`, `IReadOnlyDictionary<TKey, TValue>`). Default severity: **Info**.

### E128053 &mdash; Collection of file system path strings

Flags `IEnumerable<string>`, `List<string>`, `string[]` etc. parameters and properties with path-like names. Use `IEnumerable<FileInfo>` or `IEnumerable<DirectoryInfo>` instead.

### E128011 &mdash; GeneratedRegex timeout

Flags `[GeneratedRegex]` attributes without `matchTimeoutMilliseconds`. Without a timeout, catastrophic backtracking on malicious input can hang the process indefinitely.

```csharp
// Before (warns)
[GeneratedRegex(@"\d+")]
private static partial Regex DigitsOnly { get; }

// After
[GeneratedRegex(@"\d+", RegexOptions.None, Timeout.Infinite)]
private static partial Regex DigitsOnly { get; }
```

### E128012 &mdash; GeneratedRegex Compiled

Flags `RegexOptions.Compiled` in `[GeneratedRegex]`. The source generator ignores this flag &mdash; it produces compiled code at build time.

```csharp
// Before (warns)
[GeneratedRegex(@"\d+", RegexOptions.Compiled, 1000)]

// After
[GeneratedRegex(@"\d+", RegexOptions.None, 1000)]
```

### E128013 &mdash; Overlapping quantifiers

Flags `[GeneratedRegex]` patterns where `\s*` or `\s+` is adjacent to a quantifier whose character set overlaps (e.g., `.*`, `.+`). This creates exponential backtracking risk.

### E128014 &mdash; Nested quantifiers

Flags `[GeneratedRegex]` patterns with nested quantifiers (e.g., `(.+)+`, `(\w+)+`, `(a*)*`). These cause exponential backtracking on non-matching input.

### E128002 &mdash; string.Empty

Flags empty string literals (`""`). `string.Empty` is clearer and avoids allocation ambiguity.

### E128006 &mdash; Encoding.UTF8

Flags `Encoding.Default` and `Encoding.ASCII`. `Encoding.Default` is platform-specific; `Encoding.ASCII` silently drops non-ASCII characters. Use `Encoding.UTF8`.

### E128054 &mdash; Temp directory cleanup

Flags classes that call `Path.GetTempPath()` in a field initializer, property initializer, or constructor without implementing `IDisposable`, `IAsyncDisposable`, or xUnit's `IAsyncLifetime`. Temp directories allocated at class level leak without a cleanup interface.

```csharp
// Before (warns)
public class TestFixture
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "test");
}

// After
public class TestFixture : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "test");
    public void Dispose() => Directory.Delete(_tempDir, recursive: true);
}
```

### E128063 &mdash; Mid-name underscore in private static member

Flags private/internal static members whose name contains an underscore at index &ge; 2 (e.g., `Nots_supportedExtensions`, `Creates_enrichmentJsonOptions`, `Spectres_terminal`). These are artifacts of IDE1006 batch-rename operations that mangle identifiers by inserting underscores at word boundaries instead of adjusting capitalization. Excludes: leading underscore (`_foo`), Hungarian prefix (`s_foo`, `m_foo`, `t_foo`), const fields, `op_` operator methods, `__` double-underscore patterns, and compiler-generated property accessors.

```csharp
// Before (warns)
private static readonly string[] Nots_supportedExtensions = [];

// After
private static readonly string[] NotsSupportedExtensions = [];
```

### E128062 &mdash; Stale ReferenceAssemblies in tests

Flags `ReferenceAssemblies.Net.Net80` / `Net90` usages in test code when the configured minimum framework version is higher. Tests that use older reference assemblies may miss API availability issues specific to the production target framework. Configurable via `e128_minimum_framework_version` in `.globalconfig` (default: 100 for net10.0).

```csharp
// Before (warns — project targets net10.0)
ReferenceAssemblies = ReferenceAssemblies.Net.Net80,

// After
ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
```

### E128064 &mdash; Disk write-then-read round-trip

Flags file-I/O sequences that write to a path and then immediately read the same path back into memory in the same method body. Returning the in-memory source value avoids a needless disk round-trip and the associated race window where another process can modify the file between the write and the read. Covers `File.WriteAllText`/`ReadAllText`, `File.WriteAllBytes`/`ReadAllBytes`, `File.WriteAllLines`/`ReadAllLines`, their `Async` variants, `AppendAllText` and `AppendAllLines`, `File.CreateText`/`AppendText`/`Create`/`OpenWrite`, `StreamWriter`/`StreamReader`, `FileStream` (write intent vs. read intent), `BinaryWriter`/`BinaryReader`, and the equivalent `FileInfo` instance methods. Cross-kind matches (text write → bytes read, or bytes write → text read) are wrapped by the code fix in `Encoding.UTF8.GetBytes`/`GetString`. Disabled for test projects via `.globalconfig` (fixtures legitimately round-trip through disk).

```csharp
// Before (warns)
File.WriteAllText(path, content);
return File.ReadAllText(path);

// After
File.WriteAllText(path, content);
return content;
```

### E128055 &mdash; Unbalanced pragma warning disable

Flags `#pragma warning disable` directives that are never matched by a corresponding `#pragma warning restore` in the same file. An unbalanced disable suppresses diagnostics for the rest of the file, silently hiding real issues.

```csharp
// Before (warns — no restore)
#pragma warning disable CS8600

// After
#pragma warning disable CS8600
string? value = GetValue();
#pragma warning restore CS8600
```

### E128056 &mdash; FileInfo.Exists TOCTOU race condition

Flags file-read calls (e.g., `File.ReadAllText`, `File.OpenRead`) that immediately follow a `FileInfo.Exists` check without a `try/catch` guard. Another process may delete the file between the check and the read, causing an unhandled `IOException`.

```csharp
// Before (warns)
if (file.Exists)
    content = File.ReadAllText(file.FullName);  // race window

// After
try
{
    content = File.ReadAllText(file.FullName);
}
catch (FileNotFoundException) { }
```

### E128057 &mdash; Unprotected cleanup in finally block

Flags cleanup calls (`File.Delete`, `Directory.Delete`, `Dispose`, etc.) in `finally` blocks that are not wrapped in their own `try/catch`. An exception thrown during cleanup will replace the original exception, making the root cause invisible.

```csharp
// Before (warns)
finally
{
    File.Delete(tempPath);  // exception here swallows the original
}

// After
finally
{
    try { File.Delete(tempPath); } catch (IOException) { }
}
```

### E128058 &mdash; Return List&lt;T&gt; via .AsReadOnly()

Flags methods that return a `List<T>` field directly when the declared return type is `IReadOnlyList<T>`. Callers can cast back to `List<T>` and mutate the internal list. Use `.AsReadOnly()` to return a true read-only wrapper.

```csharp
// Before (warns)
public IReadOnlyList<string> Items => _items;

// After
public IReadOnlyList<string> Items => _items.AsReadOnly();
```

### E128059 &mdash; Unused interface method parameter

Flags interface method implementations where a parameter declared in the interface contract is never referenced in the implementation body. The interface promises callers that the parameter affects behavior; ignoring it silently violates that contract.

```csharp
// Before (warns)
public string Format(string input, IFormatProvider? provider) =>
    input.ToUpperInvariant();  // provider is ignored

// After — rename unused parameter to _ to signal intent
public string Format(string input, IFormatProvider? _) =>
    input.ToUpperInvariant();
```

### E128060 &mdash; Return Dictionary&lt;K,V&gt; via .AsReadOnly()

Flags methods and properties that return a `Dictionary<K,V>` field directly when the declared return type is `IReadOnlyDictionary<K,V>`. Callers can cast back to `Dictionary<K,V>` and mutate the internal collection. Use `.AsReadOnly()` (requires .NET 9+) or wrap in `ReadOnlyDictionary<K,V>`.

```csharp
// Before (warns)
public IReadOnlyDictionary<string, int> Counts => _counts;

// After
public IReadOnlyDictionary<string, int> Counts => _counts.AsReadOnly();
```

### E128061 &mdash; Static readonly array should be ImmutableArray

Flags `private static readonly T[]` and `internal static readonly T[]` fields. Arrays are reference types &mdash; `readonly` prevents reassignment but callers can still mutate contents via the indexer. Use `ImmutableArray<T>` for true immutability.

```csharp
// Before (warns)
private static readonly string[] Names = ["a", "b"];

// After
private static readonly ImmutableArray<string> Names = ["a", "b"];
```

## Configuration

Override severity per rule in `.editorconfig`:

```ini
# Promote to error
dotnet_diagnostic.E128008.severity = error

# Disable a rule
dotnet_diagnostic.E128005.severity = none
```

## Requirements

- .NET SDK 8.0+ (targets `netstandard2.0`, runs in any modern Roslyn host)
- Visual Studio 2022 17.8+, Rider 2024.1+, or `dotnet build` from CLI

## License

MIT
