# E128.Analyzers

Roslyn analyzers and code fixes that enforce opinionated .NET conventions at compile time. Catch design, reliability, performance, and style issues before they reach code review.

## Installation

```xml
<PackageReference Include="E128.Analyzers" Version="1.3.0" PrivateAssets="all" />
```

> `PrivateAssets="all"` keeps the analyzers out of your consumers' dependency graph.

## Rules

All rules default to **Warning** severity. Every rule includes a code fix unless noted.

### Design

| Rule    | Title                                                    | Code Fix |
| ------- | -------------------------------------------------------- | -------- |
| E128001 | Use `FileInfo`/`DirectoryInfo` instead of `string` paths | Yes      |
| E128005 | Seal classes that have no subclasses                     | Yes      |
| E128007 | Avoid `async void` methods (non-event-handler)           | Yes      |
| E128008 | Avoid sync-over-async (`.Result` / `.GetAwaiter().GetResult()`) | Yes |

### Reliability

| Rule    | Title                                                    | Code Fix |
| ------- | -------------------------------------------------------- | -------- |
| E128003 | Use `TimeProvider` instead of `DateTime.Now` / `DateTimeOffset.Now` | Yes      |
| E128004 | Use `IHttpClientFactory` instead of `new HttpClient()`              | Yes      |
| E128011 | `[GeneratedRegex]` missing `matchTimeoutMilliseconds`               | Yes      |
| E128012 | `RegexOptions.Compiled` is redundant in `[GeneratedRegex]`          | Yes      |
| E128013 | `[GeneratedRegex]` pattern has overlapping quantifiers              | No       |
| E128014 | `[GeneratedRegex]` pattern has nested quantifiers                   | No       |

### Performance

| Rule    | Title                                                    | Code Fix |
| ------- | -------------------------------------------------------- | -------- |
| E128009 | Use `MinBy`/`MaxBy` instead of `OrderBy().First()`      | Yes      |
| E128010 | Pass `HttpCompletionOption.ResponseHeadersRead` to `HttpClient` calls | Yes |

### Style

| Rule    | Title                                                    | Code Fix |
| ------- | -------------------------------------------------------- | -------- |
| E128002 | Use `string.Empty` instead of `""`                       | Yes      |
| E128006 | Use `Encoding.UTF8` instead of `Encoding.Default`        | Yes      |

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
