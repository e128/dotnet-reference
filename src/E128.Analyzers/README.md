# E128.Analyzers

Roslyn analyzers and code fixes that enforce opinionated .NET conventions at compile time. Catch design, reliability, performance, and style issues before they reach code review.

## Installation

```xml
<PackageReference Include="E128.Analyzers" Version="1.5.0" PrivateAssets="all" />
```

> `PrivateAssets="all"` keeps the analyzers out of your consumers' dependency graph.

## Rules

All rules default to **Warning** severity unless noted. Every rule includes a code fix unless noted.

### Design

| Rule    | Title                                                                    | Code Fix |
| ------- | ------------------------------------------------------------------------ | -------- |
| E128001 | Use `FileInfo`/`DirectoryInfo` instead of `string` paths                 | Yes      |
| E128005 | Seal classes that have no subclasses                                     | Yes      |
| E128007 | Avoid `async void` methods (non-event-handler)                           | Yes      |
| E128008 | Avoid sync-over-async (`.Result` / `.GetAwaiter().GetResult()`)          | Yes      |
| E128017 | Use primary constructor parameter directly                               | Yes      |
| E128019 | Do not pass `CancellationToken` by `in` reference                        | Yes      |
| E128021 | Do not use `in` modifier with ref struct parameters (default: Error)     | Yes      |
| E128022 | Remove `ConfigureAwait(false)` in apps without `SynchronizationContext`   | Yes      |
| E128030 | Do not compare `FileSystemInfo` types by reference (default: Info)        | Yes      |

### Reliability

| Rule    | Title                                                               | Code Fix |
| ------- | ------------------------------------------------------------------- | -------- |
| E128003 | Use `TimeProvider` instead of `DateTime.Now` / `DateTimeOffset.Now` | Yes      |
| E128004 | Use `IHttpClientFactory` instead of `new HttpClient()`              | Yes      |
| E128011 | `[GeneratedRegex]` missing `matchTimeoutMilliseconds`               | Yes      |
| E128012 | `RegexOptions.Compiled` is redundant in `[GeneratedRegex]`          | Yes      |
| E128013 | `[GeneratedRegex]` pattern has overlapping quantifiers              | Yes      |
| E128014 | `[GeneratedRegex]` pattern has nested quantifiers                   | Yes      |
| E128016 | `DateTime.Parse`/`ParseExact` missing `DateTimeStyles` parameter    | Yes      |
| E128020 | Do not use `in` modifier with mutable structs                       | Yes      |
| E128023 | Avoid hardcoded `/tmp` path                                         | Yes      |
| E128028 | `Task.FromResult` wraps sync I/O that has an async alternative      | Yes      |

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
