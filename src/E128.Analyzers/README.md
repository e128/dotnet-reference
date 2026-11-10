# E128.Analyzers

Roslyn analyzers and code fixes that enforce opinionated .NET conventions at compile time. Catch design, reliability, performance, and style issues before they reach code review.

## Installation

```xml
<PackageReference Include="E128.Analyzers" Version="2.6.6" PrivateAssets="all" />
```

> `PrivateAssets="all"` keeps the analyzers out of your consumers' dependency graph.

## Rules

All rules default to **Warning** severity unless noted. Every rule includes a code fix unless noted.

### Design

| Rule    | Title                                                                                                | Code Fix |
| ------- | ---------------------------------------------------------------------------------------------------- | -------- |
| E128001 | Use `FileInfo`/`DirectoryInfo` instead of `string` paths                                             | Yes      |
| E128003 | Use `TimeProvider` instead of `DateTime.Now` / `DateTimeOffset.Now`                                  | Yes      |
| E128004 | Use `IHttpClientFactory` instead of `new HttpClient()`                                               | Yes      |
| E128005 | Seal classes that have no subclasses                                                                 | Yes      |
| E128007 | Avoid `async void` methods (non-event-handler)                                                       | Yes      |
| E128008 | Avoid sync-over-async (`.Result` / `.GetAwaiter().GetResult()`)                                      | Yes      |
| E128017 | Use primary constructor parameter directly                                                           | Yes      |
| E128019 | Do not pass `CancellationToken` by `in` reference                                                    | Yes      |
| E128021 | Do not use `in` modifier with ref struct parameters (default: Error)                                 | Yes      |
| E128030 | Do not compare `FileSystemInfo` types by reference (default: Info)                                   | Yes      |
| E128032 | Concrete-only DI registration with available interface                                               | Yes      |
| E128036 | `Task.Run` wrapping async lambda — unnecessary thread pool hop                                       | Yes      |
| E128042 | `Convert.ToInt32`/`ToInt64` wrapping `ExecuteScalar` without null guard                              | Yes      |
| E128044 | Type implements `IAsyncDisposable` but not `IDisposable`                                             | Yes      |
| E128045 | Avoid direct `System.Console` usage                                                                  | No       |
| E128046 | Class has excessive user-defined inheritance depth                                                   | No       |
| E128048 | Use `switch` instead of if/else-if chain on enum values                                              | Yes      |
| E128049 | Avoid `[DynamicallyAccessedMembers]` — suppress with justification if required                       | Yes      |
| E128050 | Use `TimeSpan` for time-duration values to avoid unit ambiguity (default: Error)                     | Yes      |
| E128052 | Use immutable collection interface instead of mutable concrete type (default: Info)                  | Yes      |
| E128053 | Use collection of `FileInfo`/`DirectoryInfo` instead of collection of `string` for file system paths | Yes      |
| E128058 | Return `List<T>` via `.AsReadOnly()` when exposing as `IReadOnlyList<T>`                             | Yes      |
| E128059 | Interface method parameter is unused in implementation (default: Error)                              | Yes      |
| E128060 | Return `Dictionary<K,V>` via `.AsReadOnly()` when exposing as `IReadOnlyDictionary<K,V>`             | Yes      |
| E128061 | Use an immutable type for static readonly arrays                                                     | Yes      |
| E128074 | Readonly struct property should use `init` accessor                                                  | Yes      |
| E128080 | Use `ByteSize` for data-size values to avoid unit ambiguity (default: Error)                         | Yes      |
| E128082 | Do not unwrap `ByteSize` via cast                                                                    | Yes      |
| E128087 | Static numeric field should not be incremented with ++/--                                            | Yes      |
| E128218 | Boolean flags encode one state (default: Info)                                                       | No       |

### Reliability

| Rule    | Title                                                                                 | Code Fix |
| ------- | ------------------------------------------------------------------------------------- | -------- |
| E128011 | `[GeneratedRegex]` missing `matchTimeoutMilliseconds`                                 | Yes      |
| E128012 | `RegexOptions.Compiled` is redundant in `[GeneratedRegex]`                            | Yes      |
| E128013 | `[GeneratedRegex]` pattern has overlapping quantifiers                                | Yes      |
| E128014 | `[GeneratedRegex]` pattern has nested quantifiers                                     | Yes      |
| E128016 | `DateTime.Parse`/`ParseExact` missing `DateTimeStyles` parameter                      | Yes      |
| E128020 | Do not use `in` modifier with mutable structs                                         | Yes      |
| E128023 | Avoid hardcoded `/tmp` path                                                           | Yes      |
| E128028 | `Task.FromResult` wraps sync I/O that has an async alternative                        | Yes      |
| E128031 | `AddSingleton` factory returns `IDisposable`                                          | Yes      |
| E128033 | Options class bound via `.Bind()` has init-only property                              | Yes      |
| E128034 | Constructor `new`s a DI-registered type — inject via DI instead                       | Yes      |
| E128035 | Concrete-type DI dependency without direct registration                               | Yes      |
| E128037 | Unbounded `Task.WhenAll` over async `Select` or a `Task.Run` list                      | Yes      |
| E128038 | `Task.WhenAll` async lambda missing `CancellationToken` propagation                   | Yes      |
| E128039 | Catch filter must exclude `OperationCanceledException`                                | Yes      |
| E128040 | Concurrency limit must be positive                                                    | Yes      |
| E128041 | `JsonDocument.RootElement` must not escape the document's `using` scope               | Yes      |
| E128051 | Broad catch in async `HttpClient` method missing `OperationCanceledException` handler | Yes      |
| E128056 | File existence check TOCTOU race condition                                            | Yes      |
| E128057 | Unprotected cleanup in finally block                                                  | Yes      |
| E128064 | Disk write-then-read round-trip — use the in-memory value instead of reading back     | Yes      |
| E128070 | Pool `Rent()` capacity must be bounded — guard with `Math.Min`                        | Yes      |
| E128076 | Materialize `QuerySelectorAll` result before iterating                                | Yes      |
| E128077 | `TextContent` string match requires a preceding length guard                          | Yes      |
| E128078 | `GetAttribute("href")` on element that does not carry href                            | No       |
| E128079 | `CompositeDetection` with single generic ID selector lacks specificity                | No       |
| E128086 | `ArrayPool` buffer used as `SqliteParameter` value without `Size`                     | Yes      |
| E128089 | Avoid bare `.Parse()` — use `TryParse` to handle invalid input                        | No       |
| E128092 | Use async `File.*Async` overload instead of sync method                               | Yes      |
| E128093 | Use async `DbConnection`/`DbCommand` `*Async` overload instead of sync method         | Yes      |
| E128096 | Detect synchronous call to async local function via `.Result` or `.Wait()`            | Yes      |
| E128099 | Wait for process exit with a provable timeout                                         | Yes      |
| E128100 | Catch filter reads negated token state on `OperationCanceledException`                | Yes      |
| E128101 | Verify process output file exists before use                                          | Yes      |
| E128103 | DI resolve or named HttpClient resolve with no registration                           | No       |
| E128109 | `stackalloc` must be bounded and outside loops                                        | Yes      |
| E128110 | Zero a pooled buffer in a `finally` block before `Return`                             | Yes      |
| E128111 | Pooled buffer used after `Return`                                                     | No       |
| E128154 | Singleton service depends on a scoped service or DbContext                            | No       |
| E128155 | Mutating the loop source inside its own foreach                                       | Yes      |
| E128159 | TaskCompletionSource without RunContinuationsAsynchronously                           | Yes      |
| E128160 | Redirected process output never read before WaitForExit                               | No       |
| E128161 | Path.Combine with a rooted or parent segment                                          | Yes      |
| E128162 | Local used after an explicit Dispose                                                  | No       |
| E128163 | ImmutableArray field left at its default                                              | Yes      |
| E128170 | `Split(...)[n]` index with no length guard                                            | No       |
| E128171 | Committed `Assert.Fail` red-phase stub                                                | No       |
| E128172 | `catch` swallows an assertion failure                                                 | No       |
| E128173 | `[SuppressMessage]` or `[ExcludeFromCodeCoverage]` with no justification              | No       |
| E128174 | Constructor past the `e128_max_ctor_dependencies` ceiling                             | No       |
| E128175 | `SemaphoreSlim` wait with no `finally` release guarantee                              | No       |
| E128176 | Event subscription that keeps the subscriber alive                                    | No       |
| E128177 | Report a record member with a mutable collection type                                 | No       |
| E128178 | Report two members that map to one JSON property name (default: Error)                | No       |
| E128179 | Options type bound from configuration with no validation call                         | No       |
| E128181 | HttpClient.Timeout set to an infinite value hangs the caller                          | No       |
| E128182 | IAsyncEnumerable value enumerated twice                                               | No       |
| E128183 | Task.WhenAny result is never awaited                                                  | No       |
| E128186 | Await inside a finally block                                                          | No       |
| E128188 | Exception filter reads the message text                                               | No       |
| E128189 | GCHandle with no Free call                                                            | No       |
| E128190 | FileStream created without asynchronous I/O                                           | No       |
| E128192 | GetBuffer on a MemoryStream with a hidden buffer                                      | No       |
| E128193 | Disposable field overwritten in a loop without a dispose                              | No       |
| E128194 | Lazy<T> field without thread safety                                                   | No       |
| E128195 | Configuration key absent from the appsettings files                                   | No       |
| E128196 | JsonSerializerOptions mutated after first use                                         | No       |
| E128197 | Fixed day count used as a calendar month (default: Info)                              | No       |
| E128198 | Substring count built from a length minus a constant                                  | No       |

### Performance

| Rule    | Title                                                                                                            | Code Fix |
| ------- | ---------------------------------------------------------------------------------------------------------------- | -------- |
| E128009 | Use `MinBy`/`MaxBy` instead of `OrderBy().First()`                                                               | Yes      |
| E128010 | Pass `HttpCompletionOption.ResponseHeadersRead` to `HttpClient` calls                                            | Yes      |
| E128015 | Use string interpolation instead of `string.Format`                                                              | Yes      |
| E128018 | Use `ToArray()` instead of `ToList()` for read-only `foreach`                                                    | Yes      |
| E128026 | Redundant `HashSet` allocation in `FrozenSet` creation                                                           | Yes      |
| E128027 | Use `FrozenSet`/`FrozenDictionary` for static readonly collections                                               | Yes      |
| E128029 | Replace multi-string OR-chain with `HashSet.Contains`                                                            | Yes      |
| E128066 | Linear lookup inside loop creates O(n²) complexity                                                               | Yes      |
| E128067 | String concatenation in loop creates O(n²) allocations                                                           | Yes      |
| E128068 | Sort operation inside loop creates O(n² log n) complexity                                                        | No       |
| E128069 | `List.Insert(0, ...)` in loop creates O(n²) complexity                                                           | No       |
| E128072 | Prefer `SHA256.HashData()` over `SHA256.Create()` (default: Info)                                                | Yes      |
| E128081 | Use `StringBuilderPool` instead of `new StringBuilder()` (default: Info)                                         | Yes      |
| E128083 | Use `ImmutableCollectionsMarshal.AsImmutableArray` instead of `ImmutableArray.Create/CreateRange` with `ToArray` | Yes      |
| E128084 | Use `CollectionsMarshal.AsSpan` with `Slice` instead of `List.GetRange`                                          | Yes      |
| E128085 | Use `foreach` + `AddRange` instead of `SelectMany.ToList`                                                        | Yes      |
| E128098 | Collapse chained or looped `string.Replace` calls into a single pass                                             | Yes      |
| E128102 | Use `SearchValues<char>` for `IndexOfAny` scans                                                                  | Yes      |
| E128106 | Index a collection with a bound taken from a different collection                                                | No       |
| E128116 | Do not use `MethodImplOptions.AggressiveOptimization`                                                            | Yes      |
| E128117 | Use `LibraryImport` instead of `DllImport`                                                                       | Yes      |
| E128121 | Use `SequenceEqual` instead of an element-by-element loop                                                        | Yes      |
| E128122 | Guard an expensive log argument with `IsEnabled`                                                                 | Yes      |
| E128123 | Use `[GeneratedRegex]` for a constant pattern                                                                    | Yes      |
| E128124 | Cache `SearchValues` in a static readonly field                                                                  | Yes      |
| E128131 | Parse UTF-8 bytes with the span overload instead of a decoded string                                             | Yes      |
| E128132 | Do not use a culture-aware comparer for a collection key                                                         | Yes      |
| E128133 | Use an alternate lookup instead of a temporary string key                                                        | Yes      |
| E128134 | Use `BitOperations.PopCount` instead of a shift loop                                                             | Yes      |
| E128135 | Use `TensorPrimitives.Sum` instead of a sum loop                                                                 | Yes      |
| E128140 | Use `Span.Fill`/`Clear`/`CopyTo` instead of an element loop                                                      | Yes      |
| E128141 | Use a `u8` literal instead of `Encoding.UTF8.GetBytes`                                                           | Yes      |
| E128152 | Complexity past the `e128_max_complexity` ceiling (nested loops, recursion)                                      | Yes      |
| E128153 | List.RemoveAt(0) in loop creates O(n²) complexity                                                               | No       |
| E128180 | Entity Framework Core query executed inside a loop                                                               | No       |
| E128185 | Entity Framework Core SaveChanges inside a loop                                                                  | No       |
| E128187 | Parallel.ForEachAsync without a degree cap (default: Info)                                                       | No       |
| E128203 | HTTP request inside a loop creates N+1 round trips                                                               | No       |
| E128204 | Database command inside a loop runs one round trip per iteration                                                 | No       |
| E128205 | Collection filled in a loop has a known final count                                                              | Yes      |
| E128206 | Empty loop body spins the CPU                                                                                    | No       |
| E128207 | Reflection lookup inside a loop                                                                                  | No       |
| E128208 | `ContainsKey` then indexer on a `ConcurrentDictionary`                                                           | No       |
| E128209 | `GetBytes(...).Length` inside a loop allocates a byte array for its length                                       | Yes      |
| E128210 | `new Regex` inside a loop with a loop-invariant pattern                                                          | No       |
| E128211 | `Task.Delay` in a loop has no cancellation token                                                                | No       |
| E128212 | `ToCharArray` result used only for indexing or iteration                                                        | No       |
| E128213 | `Activator.CreateInstance` inside a loop                                                                        | No       |
| E128214 | HTTP handler does not enable automatic decompression                                                            | No       |
| E128215 | `StringBuilder` created without capacity for a known append count                                               | No       |
| E128216 | Unbounded channel can grow without limit                                                                        | No       |
| E128217 | Large struct passed by value                                                                                    | No       |

### Security

| Rule    | Title                                                                          | Code Fix |
| ------- | ------------------------------------------------------------------------------ | -------- |
| E128071 | Use an approved cryptographic algorithm                                        | Yes      |
| E128075 | Use `RandomNumberGenerator` instead of `Random` in crypto context              | Yes      |
| E128091 | Use `ArgumentList` instead of a built `Arguments` string on `ProcessStartInfo` | Yes      |
| E128142 | Do not instantiate a Managed or CNG cryptography type                          | No       |
| E128143 | Use an RSA key of at least 3072 bits or an approved curve                      | No       |
| E128144 | Use an authenticated cipher mode                                               | No       |
| E128145 | Use a strong PBKDF2 configuration                                              | No       |
| E128146 | Use TLS 1.2 or later                                                           | No       |
| E128147 | RNGCryptoServiceProvider is deprecated                                         | No       |
| E128148 | Do not use a cipher excluded by policy                                         | No       |
| E128149 | Use the full 16-byte AES-GCM tag                                               | No       |
| E128150 | Do not use SHA-3, SHAKE or KMAC                                                | No       |
| E128151 | Use an HMAC key at least as long as the hash output                            | No       |
| E128156 | HttpClient certificate validation accepts every certificate (default: Error)   | No       |
| E128157 | Fixed-time comparison of a secret-derived value                                | Yes      |
| E128164 | Hardcoded absolute URL in source                                               | No       |
| E128165 | Hardcoded secret or connection string in source                                | No       |
| E128166 | Sensitive value passed to a logger call                                        | No       |
| E128167 | Deferred-work comment with no work-item reference                              | No       |
| E128168 | Skipped test with no work-item reference in its reason                         | No       |
| E128169 | `[Obsolete]` with no message                                                   | No       |
| E128184 | Recursive delete outside a proven safe root (default: Info)                    | No       |
| E128191 | PFX import without EphemeralKeySet                                             | No       |
| E128202 | Unvalidated JWT claims used as an identity                                     | No       |

### Style

| Rule    | Title                                                                                   | Code Fix |
| ------- | --------------------------------------------------------------------------------------- | -------- |
| E128002 | Use `string.Empty` instead of `""`                                                      | Yes      |
| E128006 | Use `Encoding.UTF8` instead of `Encoding.Default`                                       | Yes      |
| E128024 | Non-XML-doc comment above method declaration                                            | Yes      |
| E128025 | Use `Path.GetRandomFileName()` instead of `Guid.NewGuid()` in temp paths                | Yes      |
| E128043 | Do not use the null-forgiving operator                                                  | Yes      |
| E128047 | `#pragma warning disable` without justification comment                                 | Yes      |
| E128055 | Unbalanced `#pragma warning disable` without matching restore                           | Yes      |
| E128063 | Mid-name underscore in private static member (IDE1006 rename artifact) (default: Error) | Yes      |
| E128065 | `#pragma warning disable` with multiple IDs — use one pragma per ID                     | Yes      |
| E128094 | `#pragma warning disable` before file-scoped namespace (default: Error)                 | Yes      |

### Testing

| Rule    | Title                                                                 | Code Fix |
| ------- | --------------------------------------------------------------------- | -------- |
| E128054 | Class creates temp directory without cleanup interface                | Yes      |
| E128062 | Test uses outdated `ReferenceAssemblies` — does not match project TFM | Yes      |
| E128073 | Test method missing `[Trait("Category", ...)]` attribute              | Yes      |
| E128097 | Comment inside test code                                              | Yes      |
| E128104 | Assertion with literal operands                                       | No       |
| E128105 | Test method calls an out-of-repo resolver without a skip guard        | No       |
| E128107 | Test matches one low-value test condition (off by default)            | Yes      |
| E128108 | Test matches two or more low-value test conditions (off by default)   | Yes      |

### Maintainability

| Rule    | Title                                                   | Code Fix |
| ------- | ------------------------------------------------------- | -------- |
| E128088 | Switch over an enum does not case every member          | Yes      |
| E128090 | Avoid reflection in test methods (default: Info)        | No       |
| E128199 | Method calls itself directly (default: Info)            | No       |
| E128201 | Conditional directives past the ceiling (default: Info) | No       |

### Unsafe

| Rule    | Title                                                                                                | Code Fix |
| ------- | ---------------------------------------------------------------------------------------------------- | -------- |
| E128112 | Use `Unsafe.BitCast` instead of `Unsafe.As`                                                          | Yes      |
| E128113 | Use the unaligned `Unsafe` read and write methods                                                    | Yes      |
| E128114 | Index the array instead of bypassing its bounds check                                                | Yes      |
| E128115 | Do not reinterpret between `bool` and `int`                                                          | Yes      |
| E128118 | Do not reinterpret bytes as a non-blittable type                                                     | Yes      |
| E128119 | Use an inline array instead of a fixed-size buffer                                                   | Yes      |
| E128120 | Do not skip local initialization without a measured reason                                           | Yes      |
| E128125 | Pin the object explicitly with `fixed`                                                               | No       |
| E128126 | Do not write to a string through a fixed pointer                                                     | No       |
| E128127 | Do not create a null managed reference                                                               | No       |
| E128128 | Keep the delegate alive while native code holds its function pointer                                 | No       |
| E128129 | Do not bulk-copy a type that holds references                                                        | No       |
| E128136 | Do not pair a pointer parameter with a separate length parameter                                     | No       |
| E128137 | Do not bind to a non-public BCL member through reflection                                            | No       |
| E128138 | Do not build dynamic methods with raw IL                                                             | No       |
| E128139 | Do not move a non-blittable type through an unaligned access                                         | Yes      |
| E128200 | Do not declare a function pointer type                                                               | No       |

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

Flags non-abstract, non-static classes with no derived types in the compilation. Sealing enables devirtualization and communicates design intent. The rule never reports a class that any type parameter, method, or local function names as a generic constraint, because a sealed class cannot be a constraint (CS0701).

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

The analyzer is `NonXmlDocCommentE128Analyzer` in `Style/NonXmlDocCommentE128Analyzer.cs`. It skips a member whose leading trivia holds a `#pragma warning disable` directive (a `restore` does not skip), because the comment explains the suppression. The old review notes call this analyzer `XmlDocCommentAnalyzer`, which does not exist.

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

Flags methods that return `Task.FromResult` or `ValueTask.FromResult` while also calling synchronous I/O methods that have async equivalents (e.g., `File.ReadAllText` instead of `File.ReadAllTextAsync`). Does not flag early-return guards, null-object implementations, or methods that already use `await`. The rule stays silent when the target framework has no async member for the pair, for example `File.ReadAllTextAsync` on `netstandard2.0`.

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

Flags `AddScoped<ConcreteType>()` / `AddTransient<ConcreteType>()` / `AddSingleton<ConcreteType>()` when the concrete type implements a non-marker interface. Register as that interface to enable testing and substitution. The code fix offers one action per interface, and it writes the type arguments and the qualification, so a generic type converts cleanly.

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

The rule also flags a `Task.Run` call added to a task list inside a loop, then awaited with `Task.WhenAll`. That is the same unbounded fan-out with a different syntax. A `SemaphoreSlim.WaitAsync` throttle in the `Task.Run` lambda clears the report. A `Task.WaitAsync` timeout does not. A loop with a numeric literal bound clears the report, because its fan-out is bounded.

### E128038 &mdash; Task.WhenAll missing CancellationToken

Flags `Task.WhenAll` async lambdas that capture a `CancellationToken` in scope but don't pass it to the inner async calls. Cancellation won't propagate.

### E128039 &mdash; Catch filter excluding OCE

Flags `catch (Exception ex) when (...)` filters that don't exclude `OperationCanceledException`. Swallowing OCE prevents proper cancellation propagation.

### E128040 &mdash; Concurrency limit must be positive

Flags a negative `SemaphoreSlim` initial count, and a `MaxDegreeOfParallelism` or a `Channel.CreateBounded` capacity of zero or negative. A zero `SemaphoreSlim` initial count is legal and stays silent.

### E128041 &mdash; JsonDocument.RootElement lifetime

Flags `JsonDocument.RootElement` returned or assigned to a field/variable that escapes the `using` scope. After disposal, the `RootElement` points to freed memory.

### E128042 &mdash; ExecuteScalar null guard

Flags `Convert.ToInt32(cmd.ExecuteScalar())` and similar patterns without a null check. `ExecuteScalar` returns `null` when there are no rows, causing an `InvalidCastException`.

### E128043 &mdash; Null-forgiving operator

Flags every use of the null-forgiving operator (`!`). The operator suppresses nullable analysis without ensuring the value is non-null, hiding potential `NullReferenceException`s.

### E128044 &mdash; IAsyncDisposable without IDisposable

Flags types that implement `IAsyncDisposable` but not `IDisposable`. Consumers using synchronous `using` will silently skip disposal. The code fix adds `IDisposable` to the base list, and adds a `Dispose()` stub only when the type does not already declare one.

### E128045 &mdash; Direct System.Console usage

Flags direct use of `System.Console` members. Use `ILogger` for service code or `ITerminalWriter`/`ITerminalPrompt` for CLI code instead. No code fix &mdash; the replacement depends on context.

### E128046 &mdash; Excessive inheritance depth

Flags classes with 3 or more levels of user-defined inheritance (excluding framework base classes). Set `e128_max_inheritance_depth` to change the threshold. Deep hierarchies are fragile and hard to reason about. No code fix.

### E128047 &mdash; Suppression without justification

Flags `#pragma warning disable` directives that don't have an accompanying justification comment on the same or preceding line. Every suppression should explain why.

### E128048 &mdash; Enum if/else-if chain

Flags if/else-if chains comparing the same variable against enum constants. A `switch` statement is more readable and the compiler can warn about missing cases.

### E128049 &mdash; DynamicallyAccessedMembers guard

Flags usage of `[DynamicallyAccessedMembers]` attribute. This attribute opts out of trimming safety. Suppress with a justification comment if required for `HttpClientFactory` or JSON discriminated unions.

### E128050 &mdash; TimeSpan for durations

Flags `int` or `double` parameters/properties with names like `timeout`, `delay`, `interval`, or `duration`. Use `TimeSpan` to avoid unit ambiguity (seconds vs milliseconds). Default severity: **Error**.

### E128051 &mdash; HttpClient missing OCE catch

Flags broad `catch (Exception)` blocks inside async methods that call `HttpClient` without a preceding `catch (OperationCanceledException)`. Swallowing OCE prevents proper timeout/cancellation handling. The code fix inserts a `catch (OperationCanceledException) { throw; }` clause before the broad catch.

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

Flags classes that call `Path.GetTempPath()` in a field initializer, property initializer, or constructor without implementing `IDisposable`, `IAsyncDisposable`, or xUnit's `IAsyncLifetime`. Temp directories allocated at class level leak without a cleanup interface. The code fix adds `IAsyncLifetime`, and adds `InitializeAsync` or `DisposeAsync` only when the class does not already declare that member.

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

Flags file-I/O sequences that write to a path and then immediately read the same path back into memory in the same method body. Returning the in-memory source value avoids a needless disk round-trip and the associated race window where another process can modify the file between the write and the read. Covers `File.WriteAllText`/`ReadAllText`, `File.WriteAllBytes`/`ReadAllBytes`, `File.WriteAllLines`/`ReadAllLines`, their `Async` variants, `AppendAllText` and `AppendAllLines`, `File.CreateText`/`AppendText`/`Create`/`OpenWrite`, `StreamWriter`/`StreamReader`, `FileStream` (write intent vs. read intent), `BinaryWriter`/`BinaryReader`, the equivalent `FileInfo` instance methods, and opaque write-to-disk calls (methods whose name contains `ToDisk`, `ToFile`, `SaveTo`, `WriteTo`, or `DownloadTo`) whose result is subsequently passed to `File.Read*`. Cross-kind matches (text write → bytes read, or bytes write → text read) are wrapped by the code fix in `Encoding.UTF8.GetBytes`/`GetString`. Opaque write-to-disk matches fire the diagnostic but offer no code fix (no in-memory source to substitute).

```csharp
// Before (warns)
File.WriteAllText(path, content);
return File.ReadAllText(path);

// After
File.WriteAllText(path, content);
return content;
```

### E128065 &mdash; Bundled pragma warning disable

Flags `#pragma warning disable` directives that list more than one diagnostic ID. Each suppression requires independent justification; bundling multiple IDs on one line hides weak reasoning and makes suppression audits harder. Split into one pragma per ID so each can carry its own justification comment. Companion to E128055.

```csharp
// Before (warns)
#pragma warning disable CS8600, CS8602

// After
#pragma warning disable CS8600
#pragma warning disable CS8602
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

### E128056 &mdash; File existence check TOCTOU race condition

Flags file-read calls (e.g., `File.ReadAllText`, `File.OpenRead`, `File.OpenText`) that follow a `FileInfo.Exists` property check or a `File.Exists(path)` call without a `try/catch` guard. Another process may delete the file between the check and the read, causing an unhandled `IOException`.

The rule compares the guarded value by symbol. It reports a read only when the checked value names the same storage location. A call argument such as `Path.Combine(dir, name)` names no storage location, so it is not matched.

```csharp
// Before (warns)
if (file.Exists)
    content = File.ReadAllText(file.FullName);  // race window

// After
try
{
    content = File.ReadAllText(file.FullName);
}
catch (IOException) { }
```

Catch `IOException`, not `FileNotFoundException`. An absent parent directory raises
`DirectoryNotFoundException`, which `FileNotFoundException` does not cover.

```csharp
// Wrong: misses the absent-directory case
try { content = File.ReadAllText(path); } catch (FileNotFoundException) { }

// Right: both types derive from IOException
try { content = File.ReadAllText(path); } catch (IOException) { }
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

Flags methods that return a `List<T>` field directly when the declared return type is `IReadOnlyList<T>` or `IReadOnlyCollection<T>`. It also covers a `Task<T>` or `ValueTask<T>` return that carries the interface. Callers can cast back to `List<T>` and mutate the internal list. Use `.AsReadOnly()` to return a true read-only wrapper.

```csharp
// Before (warns)
public IReadOnlyList<string> Items => _items;

// After
public IReadOnlyList<string> Items => _items.AsReadOnly();
```

### E128059 &mdash; Unused interface method parameter

Flags interface method implementations where a parameter declared in the interface contract is never referenced in the implementation body. The interface promises callers that the parameter affects behavior; ignoring it silently violates that contract.

```csharp
// Before (error)
public string Format(string input, IFormatProvider? provider) =>
    input.ToUpperInvariant();  // provider is ignored

// After — rename unused parameter to _ to signal intent
public string Format(string input, IFormatProvider? _) =>
    input.ToUpperInvariant();
```

### E128060 &mdash; Return Dictionary&lt;K,V&gt; via .AsReadOnly()

Flags methods and properties that return a `Dictionary<K,V>` field directly when the declared return type is `IReadOnlyDictionary<K,V>`, including a `Task<T>` or `ValueTask<T>` return that carries the interface. Callers can cast back to `Dictionary<K,V>` and mutate the internal collection. Use `.AsReadOnly()` (requires .NET 9+) or wrap in `ReadOnlyDictionary<K,V>`.

```csharp
// Before (warns)
public IReadOnlyDictionary<string, int> Counts => _counts;

// After
public IReadOnlyDictionary<string, int> Counts => _counts.AsReadOnly();
```

### E128061 &mdash; Static readonly array should be immutable

Flags `private static readonly T[]` and `internal static readonly T[]` fields. Arrays are reference types &mdash; `readonly` prevents reassignment but callers can still mutate contents via the indexer.

An initializer of constant `byte` values becomes a `ReadOnlySpan<byte>` property. The compiler stores those bytes as read-only data, so the property allocates nothing and copies nothing. Every other element type becomes `ImmutableArray<T>`.

```csharp
// Before (warns)
private static readonly string[] Names = ["a", "b"];
private static readonly byte[] Magic = [0x50, 0x4B, 0x03, 0x04];

// After
private static readonly ImmutableArray<string> Names = ["a", "b"];
private static ReadOnlySpan<byte> Magic => [0x50, 0x4B, 0x03, 0x04];
```

### E128066 &mdash; Linear lookup in loop (O(n²))

Flags linear-time methods (`.Contains`, `.Any`, `.IndexOf`, `.Remove`, `.Exists`, `.Find`, `.FindAll`, `.FindIndex`) called on list/array/`IEnumerable<T>` types inside loops or LINQ lambdas. Also detects `.Where(...).Count()` chains. Excludes O(1) types (`HashSet<T>`, `Dictionary<K,V>`, `FrozenSet<T>`, etc.), an `IQueryable<T>` receiver that E128180 owns, and constant-bound `for` loops.

```csharp
// Before (warns)
foreach (var item in items)
    if (lookup.Contains(item)) { }

// After
var lookupSet = lookup.ToHashSet();
foreach (var item in items)
    if (lookupSet.Contains(item)) { }
```

Code fix converts receiver to `HashSet<T>` via `.ToHashSet()` (offered for `.Contains` and `.Any` only).

### E128067 &mdash; String concatenation in loop

Flags `string +=` inside loops. Each iteration allocates a new string, creating O(n²) total allocations. Use `StringBuilder` instead.

```csharp
// Before (warns)
foreach (var line in lines)
    result += line;

// After
var sb = new StringBuilder();
foreach (var line in lines)
    sb.Append(line);
var result = sb.ToString();
```

### E128068 &mdash; Sort operation inside loop

Flags `.Sort()` (`List<T>`, `Array`), `.OrderBy()`, `.OrderByDescending()`, `.ThenBy()`, `.ThenByDescending()` (`System.Linq.Enumerable`) inside loops. Sorting per iteration is O(n² log n) overall. Sort once before the loop or use a sorted collection.

```csharp
// Before (warns)
foreach (var batch in batches)
    items.Sort();

// After
items.Sort();
foreach (var batch in batches) { }
```

### E128069 &mdash; List.Insert(0, ...) in loop

Flags `list.Insert(0, ...)` inside loops. Each insert at index 0 shifts all existing elements, making the loop O(n²). Use `LinkedList<T>.AddFirst` or collect and reverse.

```csharp
// Before (warns)
foreach (var item in source)
    result.Insert(0, item);

// After
var result = source.Reverse().ToList();
```

### E128070 &mdash; Pool Rent() capacity guard

Flags `Rent(arg)` calls on pool types (`ArrayPool<T>`, `StringBuilderPool`, or any type with "Pool" in the name) where the capacity argument is not bounded. Passing `int.MaxValue` or an unbounded variable causes an OOM crash. Guard with `Math.Min(value, cap)`. The fix wraps the argument in `Math.Min`. The cap comes from the `e128_pool_rent_max_capacity` option, which defaults to 1,048,576.

```csharp
// Before (warns)
var buf = ArrayPool<char>.Shared.Rent(length);

// After
var buf = ArrayPool<char>.Shared.Rent(Math.Min(length, maxCapacity));
```

### E128071 &mdash; Approved cryptographic algorithm

Flags algorithms that cryptography policy blocks: `MD5`, `SHA1`, `RIPEMD160`, `DES`, `RC2`, `TripleDES`, the MAC types built on them, `ChaCha20Poly1305` and `PasswordDeriveBytes`. It covers constructors, static `Create` and `HashData` members, method groups, legacy factories that select a weak algorithm by default, and any cryptography API that takes a blocked `HashAlgorithmName`.

It also flags a constant blocked XML-DSig or XML-Enc URI, `CmsSigner.DigestAlgorithm` set to a blocked object identifier, `PbeEncryptionAlgorithm.TripleDes3KeyPkcs12`, `RSAEncryptionPadding.Pkcs1` on an RSA encryption call, the PKCS#1 key exchange formatters, and DSA signature generation. DSA verification stays allowed.

```csharp
// Before (warns)
using var md5 = MD5.Create();
using var hmac = new HMACSHA1(key);
var cipher = rsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);

// After
using var sha256 = SHA256.Create();
using var hmac = new HMACSHA256(key);
var cipher = rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
```

### E128072 &mdash; SHA256.Create() obsolete pattern (Info)

Flags `SHA256.Create()` followed by `.ComputeHash()`. The static `SHA256.HashData()` method introduced in .NET 5 performs the same operation without allocating a disposable object.

```csharp
// Before (info)
using var sha = SHA256.Create();
var hash = sha.ComputeHash(data);

// After
var hash = SHA256.HashData(data);
```

### E128073 &mdash; Missing [Trait("Category", ...)]

Flags xUnit test methods that are missing a `[Trait("Category", "...")]` attribute. The test runner uses category traits to filter CI, Podman, and Manual test runs; methods without a category are silently excluded from all filtered runs.

```csharp
// Before (warns)
[Fact]
public async Task MyTest() { }

// After
[Fact]
[Trait("Category", "CI")]
public async Task MyTest() { }
```

### E128074 &mdash; Readonly struct property should use init accessor

Flags properties in `readonly struct` types that use a private setter instead of an `init` accessor. Properties with private setters in readonly structs allow mutation at construction time but do not communicate immutability to callers. Use `init` to make the intent explicit and enforce immutability post-construction.

```csharp
// Before (warns)
public readonly struct Point
{
    public int X { get; private set; }
}

// After
public readonly struct Point
{
    public int X { get; init; }
}
```

### E128075 &mdash; Random in crypto context

Flags `System.Random`, and a class derived from it, in two cases. First, a random value flows into a variable, field, property or buffer with a security-sensitive name such as `token`, `salt`, `sessionId` or `key`. Second, a new instance or `Random.Shared` appears in a file that references a type from `System.Security.Cryptography` or `Microsoft.AspNetCore.Cryptography`. A file also counts as a cryptography file when it names a member of a cryptography type. `System.Random` is not cryptographically secure. Use `RandomNumberGenerator` instead.

The check is a heuristic. It judges only the receiving name, and the file check reports every `Random` creation in a cryptography file. A sensitive name matches as a whole word, so `secretKey` reports and `secretary` does not. The name set also comes from the referenced assemblies.

The code fix replaces `Random.Shared.Next` with `RandomNumberGenerator.GetInt32`. `Next()` becomes `GetInt32(int.MaxValue)`. The fix declines a variable bound, an empty range and `NextDouble`.

```csharp
// Before (warns)
var token = Random.Shared.Next(100000, 999999).ToString();

// After
var token = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
```

### E128091 &mdash; ProcessStartInfo.Arguments built string

Flags a `ProcessStartInfo` object initializer where `Arguments` is assigned a non-empty string (literal, interpolated, identifier, or otherwise) and `ArgumentList` isn't populated in the same initializer. A hand-built `Arguments` string needs its own shell-style quoting/escaping; `ArgumentList` passes each argument through untouched, avoiding injection risk from unescaped or mis-escaped values.

A fix is offered only when the assigned expression is a plain string literal or a single interpolated string with a whitespace/quote-splittable shape — arbitrary concatenation, method calls, and identifiers still get the diagnostic but no automatic fix.

```csharp
// Before (warns)
var startInfo = new ProcessStartInfo
{
    FileName = "tool",
    Arguments = $"-x \"{value}\""
};

// After
var startInfo = new ProcessStartInfo
{
    FileName = "tool",
    ArgumentList = { "-x", value }
};
```

### E128083 &mdash; ImmutableArray.Create with ToArray copies the array

Flags `ImmutableArray.Create(x.ToArray())` and `ImmutableArray.CreateRange(x.ToArray())`. Both patterns copy the array unnecessarily. `ImmutableCollectionsMarshal.AsImmutableArray` wraps an existing array in zero copies. The rule fires only when `ToArray` comes from an allowlisted BCL type (`Enumerable`, `List<T>`, `HashSet<T>`, `Queue<T>`, `Stack<T>`).  A user-defined `ToArray` may return a shared array.

```csharp
// Before (warns)
var result = ImmutableArray.Create(items.ToArray());

// After
var result = ImmutableCollectionsMarshal.AsImmutableArray(items.ToArray());
```

### E128084 &mdash; List.GetRange allocates intermediate list

Flags `list.GetRange(start, count)`. `GetRange` allocates an intermediate `List<T>`. Use `CollectionsMarshal.AsSpan(list).Slice(start, count).ToArray()` to copy directly from the internal array without the intermediate allocation. The rule fires only when flow analysis proves the receiver non-null, because `AsSpan(null)` returns an empty span where `GetRange` throws. A bad range throws `ArgumentOutOfRangeException` from `Slice`.

```csharp
// Before (warns)
var slice = list.GetRange(2, 5);

// After
var slice = CollectionsMarshal.AsSpan(list).Slice(2, 5).ToArray();
```

### E128085 &mdash; SelectMany.ToList creates LINQ iterator allocations

Flags `.SelectMany(lambda).ToList()`. This pattern creates intermediate LINQ iterator allocations and grows the result list without known capacity. A `foreach` loop with `AddRange` avoids both.

```csharp
// Before (warns)
var result = groups.SelectMany(g => g.Items).ToList();

// After
var result = new List<Item>();
foreach (var g in groups)
    result.AddRange(g.Items);
```

### E128086 &mdash; ArrayPool buffer without SqliteParameter.Size

Flags `ArrayPool<byte>.Shared.Rent()` buffers passed as `SqliteParameter` values without an explicit `.Size` assignment. `Rent()` returns a buffer with `Length >= requested`, so without `.Size` SQLite reads the full oversized buffer, writing garbage bytes into the BLOB column.

```csharp
// Before (warns)
var buf = ArrayPool<byte>.Shared.Rent(dataLength);
// ... fill buf[0..dataLength]
cmd.Parameters.AddWithValue("@data", buf);  // reads full oversized buffer

// After
var buf = ArrayPool<byte>.Shared.Rent(dataLength);
// ... fill buf[0..dataLength]
var param = new SqliteParameter("@data", buf) { Size = dataLength };
cmd.Parameters.Add(param);
```

The code fix sets `Size` to the length passed to `Rent` when that length is a literal or a plain identifier. It offers no fix when the code does not show the length.

### E128087 &mdash; Static numeric field should not be incremented with ++/--

Flags `static` numeric fields mutated with `++`, `--`, `+=`, or `-=`. These operations are not atomic — concurrent invocations produce data races, and the accumulated value leaks across call contexts. Use `Interlocked.Increment`/`Decrement`/`Add` for int/long fields, or remove the `static` keyword.

```csharp
// Before (warns)
private static int _count;
void Track() => _count++;

// After — atomic
private static int _count;
void Track() => Interlocked.Increment(ref _count);
```

### E128089 &mdash; Bare .Parse() without TryParse

Flags `.Parse(...)` calls on types that also expose a static `TryParse` method (e.g., `int`, `double`, `DateTime`, `Guid`, `Enum`). `Parse` throws `FormatException` on invalid input; `TryParse` lets callers handle failure without exceptions. No code fix &mdash; the failure-handling branch is context-specific.

```csharp
// Before (warns)
var value = int.Parse(input);

// After
if (!int.TryParse(input, out var value)) { /* handle invalid input */ }
```

### E128090 &mdash; Reflection in test methods (Info)

Flags `GetMethod`, `GetProperty`, and `GetField` reflection calls inside test methods, plus a use of the `BindingFlags` type. A type that derives from `System.Type`, such as `TypeBuilder`, counts as a reflection call. Reflection couples tests to private implementation detail; prefer testing through the public API. Default severity: **Info**. No code fix.

```csharp
// Before (info)
var m = typeof(Service).GetMethod("Compute", BindingFlags.NonPublic | BindingFlags.Instance);

// After — exercise the public surface
var result = new Service().Run();
```

### E128092 &mdash; Sync File I/O with an async sibling

Flags a synchronous `System.IO.File.*` call (`ReadAllText`, `WriteAllBytes`, etc.) that has an `Async` sibling, when the containing method/local function/lambda is not already `async`. CA1849/VSTHRD103 only fire inside an already-async method; this rule also catches the fully-synchronous case, where those analyzers give no signal at all.

```csharp
// Before (warns)
void Save(string path, string text)
{
    File.WriteAllText(path, text);
}

// After
async Task Save(string path, string text)
{
    await File.WriteAllTextAsync(path, text);
}
```

### E128102 &mdash; Use `SearchValues<char>` for `IndexOfAny` scans

Flags `string.IndexOfAny` and `string.LastIndexOfAny`. These scan char by char without SIMD. Use a cached `SearchValues<char>` with the span `IndexOfAny` overload, which scans 16 to 32 bytes per instruction on AVX2, AVX-512, and ARM Neon.

```csharp
// Before (warns)
var i = path.IndexOfAny(new[] { '/', '\\' });

// After
private static readonly SearchValues<char> PathChars = SearchValues.Create("/\\");

var i = path.AsSpan().IndexOfAny(PathChars);
```

The code fix hoists a `SearchValues<char>` field into the containing type and rewrites the call to the span overload. The span overload keeps the int index semantics. It offers no fix when the argument is not a char array with literal elements.

### E128093 &mdash; Sync DbConnection/DbCommand call with an async sibling

Flags a synchronous `DbConnection`/`DbCommand` call (`Open`, `ExecuteReader`, `ExecuteNonQuery`, `ExecuteScalar`, etc.) that has an `Async` sibling, when the containing method is not already `async`. Resolves the sibling through the base-type chain, so it also fires on provider subclasses that override the sync method but inherit the async one unchanged (e.g. `Microsoft.Data.Sqlite`'s `SqliteConnection`/`SqliteCommand`).

```csharp
// Before (warns)
void Insert(SqliteConnection connection, FileInsertData data)
{
    using var command = connection.CreateCommand();
    command.ExecuteNonQuery();
}

// After
async Task Insert(SqliteConnection connection, FileInsertData data)
{
    using var command = connection.CreateCommand();
    await command.ExecuteNonQueryAsync();
}
```

### E128109 &mdash; stackalloc safety

Flags `stackalloc` inside a loop, with a constant size above 1024 bytes, or with a length that has
no guard, or that is assigned to a pointer instead of `Span<T>`.
Stack space is not reclaimed until the method returns. A conditional expression or an `if` statement counts as
a guard only when its condition tests a name used in the length. `Math.Min` also counts as a guard.
The code fix assigns the buffer to `Span<T>` when the pointer is only indexed.

```csharp
// Before (warns)
Span<byte> buffer = stackalloc byte[length];

// After
Span<byte> buffer = length <= 256 ? stackalloc byte[length] : new byte[length];
```

### E128110 &mdash; Zero a pooled buffer before Return

Flags `ArrayPool<T>.Return(buffer)` unless the same `finally` block first calls
`CryptographicOperations.ZeroMemory` on the whole rented array as a direct statement. A call nested in an
`if`, loop, or lambda does not count. A slice such as `buffer.AsSpan(0, used)`
does not satisfy the rule. `Return(buffer, clearArray: true)` does not satisfy it either. The rule
covers `ArrayPool<T>` only. `MemoryPool<T>` is not checked. The fix inserts the
`CryptographicOperations.ZeroMemory` call before the flagged `Return` inside its `finally` block.
It offers no fix when the `Return` has no enclosing `finally`.

```csharp
// Before (warns)
ArrayPool<byte>.Shared.Return(buffer);

// After
try { /* use buffer */ }
finally
{
    CryptographicOperations.ZeroMemory(buffer);
    ArrayPool<byte>.Shared.Return(buffer);
}
```

### E128111 &mdash; Pooled buffer used after Return

Flags a read or write of a rented buffer in a statement that follows `ArrayPool<T>.Return(buffer)`.
After `Return` the pool may give the array to another caller. Return from a `finally` block only when
no other thread or pending async operation still uses the buffer. A reassignment of the buffer stops the
scan. No code fix.

### E128107 and E128108 &mdash; Low-value test conditions

Flags an xUnit `[Fact]` or `[Theory]` method whose shape locks an implementation detail in place.
Such a test fails on every honest refactor. One condition reports E128107 at info. Two or more
report E128108 at warning. Tune the severity with `dotnet_diagnostic.E128107.severity`
or `dotnet_diagnostic.E128108.severity`. This repo keeps both at suggestion, so the code fix
never auto-applies. The code fix deletes the whole test method.

The conditions:

- `BareSize`: three statements, one assertion, and no production call
- `NameMirror`: the test name repeats the called method name and almost nothing else
- `NoAssertion`: the body calls production code and asserts nothing
- `SingleRowTheory`: a `[Theory]` with one `[InlineData]` row
- `LiteralEcho`: the assertion compares a literal with a call whose arguments are all literals
- `ExceptionMessageLock`: the assertion pins an exception message string
- `MockVerifyOnly`: the only assertion is a `Verify` call with a lambda
- `LogAssert`: the only assertion is a `Verify` call on a logger
- `ConstructorPassthrough`: the assertion reads a member back off an object built from the same literal
- `SelfFulfillingExpected`: the expected value and the actual value come from the same receiver
- `MagicConstantEcho`: the expected literal also appears as a returned value in the production body
- `OrderLock`: `Assert.Equal` pins the order of a set or dictionary result
- `DuplicateCoverage`: another test in the same class calls the same method with the same literal arguments

The code fix deletes the test method, so an unattended `dotnet format` run must not remove a test
without review.

### E128112: Use Unsafe.BitCast instead of Unsafe.As

Flags `Unsafe.As<TFrom, TTo>(ref value)` when both types are fixed-size value types of the same size.
`Unsafe.BitCast` checks the size and rejects a reference type.
The rule stays silent when the result is used as a reference (assigned to, passed by `ref`, or bound to a `ref` local).
The rule also stays silent when the target has no `Unsafe.BitCast` (before .NET 8).
The code fix rewrites the call to `Unsafe.BitCast<TFrom, TTo>(value)`.

```csharp
// Before (warns)
int bits = Unsafe.As<float, int>(ref value);

// After
int bits = Unsafe.BitCast<float, int>(value);
```

### E128113: Use the unaligned Unsafe read and write methods

Flags `Unsafe.Read<T>` and `Unsafe.Write<T>`.
The plain overloads assume an aligned address. A misaligned address faults or tears the value.
The code fix renames the call to `Unsafe.ReadUnaligned` or `Unsafe.WriteUnaligned`.

### E128114: Index the array instead of bypassing its bounds check

Flags `Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index)` on a single-dimension array.
The pattern removes the bounds check, and the JIT already elides the check in a simple loop.
The code fix rewrites the call to `array[index]` and adds parentheses where the array expression needs them.

### E128115: Do not reinterpret between bool and int

Flags `Unsafe.As<bool, int>` and `Unsafe.As<int, bool>` when the result is not used as a reference.
A `bool` occupies one byte and an `int` occupies four bytes. The reinterpretation reads adjacent memory.
The code fix rewrites the call to a comparison or a ternary.
The fix normalizes the value, so any nonzero `int` becomes `true`.

### E128116: Do not use MethodImplOptions.AggressiveOptimization

Flags `[MethodImpl(MethodImplOptions.AggressiveOptimization)]`.
The flag disables tiered compilation for the method.
The code fix removes the flag and keeps the other flags.
When `AggressiveOptimization` is the only flag, the fix removes the attribute and keeps the doc comment.

### E128117: Use LibraryImport instead of DllImport

Flags every `[DllImport]` on a method when the target defines `LibraryImportAttribute`.
`LibraryImport` generates the marshalling code at compile time.
The code fix applies only when every parameter and the return type are blittable, `AllowUnsafeBlocks` is on,
and the attribute uses only `EntryPoint` and `SetLastError`.
The fix renames the attribute, drops `extern`, and makes the method and the containing types `partial`.

### E128118: Do not reinterpret bytes as a non-blittable type

Flags `MemoryMarshal.Read<T>` and `MemoryMarshal.Cast<TFrom, TTo>` when the interpreted type is not blittable.
A type is not blittable when it holds a reference, or when it is or contains a `bool` or a `decimal`.
The code fix applies only to `MemoryMarshal.Read<bool>(source)`. It rewrites the call to `(source[0] != 0)`.

### E128119: Use an inline array instead of a fixed-size buffer

Flags a field declared with `fixed`, including a declaration with several buffers or a constant size, when the target defines `InlineArrayAttribute` and the language version is C# 12 or later.
A fixed-size buffer has no bounds checks and no default zeroing.
The code fix applies only to a single-declarator field that nothing references.
It replaces the field with a nested `[InlineArray]` struct named `{Field}Buffer`.
The nested struct takes the accessibility of the field.

### E128120: Do not skip local initialization without a measured reason

Flags `Unsafe.SkipInit(out local)`.
The local keeps whatever the stack holds. The rule accepts the call only after a benchmark shows a win.
The code fix replaces the call with `T name = default;`, or with `name = default;` for an existing local.

### E128121: Use SequenceEqual instead of an element-by-element loop

Flags a `for` loop that compares two arrays element by element and returns `false` on the first mismatch.
The rule applies only when these conditions hold:

- An earlier `if` returns `false` when the two `Length` values differ.
- The loop bound is the `Length` of one of the two arrays.
- The element type is an integral type, `bool`, `char`, or an enum. A floating-point type is excluded because `NaN` compares differently.

The code fix rewrites the loop to `return left.AsSpan().SequenceEqual(right);`.

### E128122: Guard an expensive log argument with IsEnabled

Flags `Log*` calls where an argument is a non-constant interpolated string or a non-constant string concatenation.
The rule stays silent when an `IsEnabled` check for the same logger and level wraps the call or returns early.
The check can sit in a local variable, or in a helper method or property that returns it.
The message is built before the level check.
The code fix wraps the statement in `if (logger.IsEnabled(LogLevel.X))`.
The fix applies only to a statement in a block, and only when the logger expression contains no call.

### E128123: Use GeneratedRegex for a constant pattern

Flags a `static readonly Regex` field built from a string literal when the target defines `GeneratedRegexAttribute`
and the language version is C# 13 or later.
`[GeneratedRegex]` builds the matcher at compile time and keeps the pattern in source.
The code fix applies only when the arguments are constants and the options are a plain list of flags.
It replaces the field with a partial property and makes every containing type `partial`.
The fix adds `matchTimeoutMilliseconds: Timeout.Infinite`, which keeps the original behavior and satisfies E128011. It drops `RegexOptions.Compiled`.

### E128124: Cache SearchValues in a static readonly field

Flags `SearchValues.Create` outside a static readonly field or a static property initializer.
The rule applies only when every argument is a constant or a static readonly value.
The call builds an immutable table on every call.
The code fix hoists the call into a private static readonly field named after the method, for example `_containsDelimiterSearchValues`.

### E128125: Pin the object explicitly with fixed

Flags `Unsafe.AsPointer(ref value)` outside a `fixed` statement.
The rule stays silent for a plain local or a by-value parameter, because the stack does not move.
The call takes no pin, so the GC can move a heap object while the pointer is live.
No code fix. Use a `fixed` block around the pointer.

### E128126: Do not write to a string through a fixed pointer

Flags a `fixed (char* p = text)` block when the body writes through `p`.
A write is an assignment, an increment, or a `ref` or `out` argument on `p[i]`, `*p`, or `*(p + n)`.
A read-only pin is silent. A copy of the pointer in another variable is not followed.
A string is immutable, and the runtime shares interned strings between callers.
No code fix. Build a new string instead.

### E128127: Do not create a null managed reference

Flags `Unsafe.NullRef<T>()` and `Unsafe.AsRef<T>` on a null pointer.
The rule stays silent when a `ref` return gives `NullRef` back as a "not found" sentinel.
A null byref has no valid referent, and any read or write through it corrupts memory.
No code fix. Use a null object, an empty span, or a nullable value instead.

### E128128: Keep the delegate alive while native code holds its function pointer

Flags `Marshal.GetFunctionPointerForDelegate` when the delegate is a temporary (`new`, a lambda, or a method group),
or when it is a local or a parameter that nothing stores in a field, property or element, or passes to another call such as `GC.KeepAlive` or `list.Add`.
The call does not root the delegate, so the GC can collect it while native code holds the pointer.
No code fix. Keep the delegate in a field or a `GCHandle` for the lifetime of the pointer.

### E128129: Do not bulk-copy a type that holds references

Flags `Unsafe.Copy` where the copied type holds a reference.
The copy bypasses the write barrier, so a copied reference can miss a GC update.
No code fix. The right replacement depends on whether the caller needs a deep or a shallow copy.

### E128131: Parse UTF-8 bytes without a decoded string

Flags `Encoding.UTF8.GetString(...)` passed to a `Parse` or `TryParse` call. The decode allocates a string that the parse then reads through a span. The span overload reads the bytes directly.

The fix replaces the decoded argument with the byte argument. When the method needs an `IFormatProvider`, the fix appends `CultureInfo.CurrentCulture`.

```csharp
// Before
int value = int.Parse(Encoding.UTF8.GetString(bytes));

// After
int value = int.Parse(bytes, CultureInfo.CurrentCulture);
```

### E128132: Do not use a culture-aware comparer for a collection key

Flags `StringComparer.CurrentCulture`, `CurrentCultureIgnoreCase`, `InvariantCulture`, and `InvariantCultureIgnoreCase` in the constructor of `Dictionary<TKey, TValue>`, `ConcurrentDictionary<TKey, TValue>`, `SortedDictionary<TKey, TValue>`, `HashSet<T>`, or `SortedSet<T>`. A culture-aware comparer holds a `CompareInfo` reference and compares code points with culture rules.

The fix replaces the comparer with `Ordinal` or `OrdinalIgnoreCase`.

### E128133: Use an alternate lookup instead of a temporary string key

Flags a lookup key of the form `new string(span)` on a string-keyed `Dictionary<TKey, TValue>`, `ConcurrentDictionary<TKey, TValue>`, or `HashSet<T>`. The construction allocates a string on every lookup.

The fix reads the collection through `GetAlternateLookup<ReadOnlySpan<char>>()`, which compares the span in place. The analyzer reports only when the initializer proves the default or the ordinal comparer, because the alternate lookup throws for a culture-aware comparer. The fix provider offers no Fix All, since one file can mix both comparer kinds.

### E128134: Use `BitOperations.PopCount` instead of a shift loop

Flags a `while (value != 0)` loop whose body is exactly `count += value & 1;` and `value >>= 1;`. The loop counts one bit per iteration. `BitOperations.PopCount` compiles to one hardware instruction.

The fix replaces the loop with `count += BitOperations.PopCount(value);` and casts a signed value to its unsigned width.

The analyzer skips a loop whose counter is unsigned (`uint` or `ulong`) by design. The counter must be `int` or `long`. The test `Silent_WhenTheCountIsUnsigned` pins this behavior.

### E128135: Use `TensorPrimitives.Sum` instead of a sum loop

Flags a `foreach` loop whose body is exactly `sum += value;` where both the sum and the element are `float` or `double`, and the source is an array, `Span<T>`, or `ReadOnlySpan<T>`. The loop adds one element per iteration.

The fix replaces the loop with `sum += TensorPrimitives.Sum(source);`. The analyzer reports only when the referenced `System.Numerics.Tensors` assembly declares the matching `Sum` overload.

### E128136: Do not pair a pointer parameter with a separate length parameter

Flags a method with a pointer parameter and another parameter of an integer or pointer-width type. The pair invites a length that does not match the buffer.

No code fix. Replace the pair with a single `Span<T>` or `ReadOnlySpan<T>` parameter.

### E128137: Do not bind to a non-public BCL member through reflection

Flags `Type.GetMethod`, `GetField`, `GetProperty`, `GetEvent`, `GetMember`, `GetConstructor`, or `GetNestedType` when a `BindingFlags` argument names a non-public member and the receiver is `typeof(X)`. A private BCL member can change or disappear between runtime versions.

No code fix. Use the public API, or a package that owns the access.

The analyzer treats a type as a BCL type only when its assembly name starts with `System` or `Microsoft` and its public key token belongs to a known framework key. A user assembly named like `System.Foo` has no framework key and stays silent.

### E128138: Do not build dynamic methods with raw IL

Flags every call on `System.Reflection.Emit.ILGenerator`. The emitted opcodes are unchecked by the compiler and are hard to read.

No code fix. Use `Expression.Lambda` or a source-generated method.

### E128139 &mdash; Do not move a non-blittable type through an unaligned access

Flags `Unsafe.ReadUnaligned<T>` and `Unsafe.WriteUnaligned<T>` when `T` is not blittable. A type is not blittable when it holds a reference, or when it is or contains a `bool` or a `decimal`. The bit pattern of such a type is not portable.

```csharp
// Before (warns)
bool value = Unsafe.ReadUnaligned<bool>(ref source);

// After
bool value = source != 0;
```

The code fix applies to `Unsafe.ReadUnaligned<bool>` only.

### E128140 &mdash; Use a span bulk method instead of an element loop

Flags a `for` loop that writes one element per iteration, in these shapes:

- `target[i] = value` becomes `Fill(value)`.
- `target[i] = 0`, `false`, `null`, or `default` becomes `Clear()`.
- `target[i] = source[i]` becomes `source.CopyTo(target)`.

The loop must count from zero with `i++` and stop at `< target.Length`. The receiver must be a name, not a method call. For the copy shape the loop bound belongs to the source, because `CopyTo` reads the whole source span.

```csharp
// Before (warns)
for (var i = 0; i < values.Length; i++)
{
    values[i] = 1;
}

// After
values.AsSpan().Fill(1);
```

The code fix calls `AsSpan()` on an array and calls the method directly on a `Span<T>`.

### E128141 &mdash; Use a u8 literal instead of Encoding.UTF8.GetBytes

Flags `Encoding.UTF8.GetBytes` with a literal string argument. The call runs the UTF-8 encoder on every call. A `u8` literal stores the bytes in the assembly.

```csharp
// Before (warns)
byte[] bytes = Encoding.UTF8.GetBytes("abc");

// After
byte[] bytes = "abc"u8.ToArray();
```

The rule needs C# 11 or later. The fix emits the bare literal when the target is a `ReadOnlySpan<byte>`. Every other target, including `Span<byte>`, keeps the `byte[]` shape through `ToArray()`.

### E128142: Do not instantiate a Managed or CNG cryptography type

Flags a direct `new` of a type whose name ends in `Managed` or `Cng`. A concrete type fixes one implementation. The abstract factory lets the platform select its cryptographic module. A `Cng` constructor that takes a `CngKey` or a `CngProvider` is not reported, because the factory cannot open a persisted key. When no factory overload matches the constructor arguments, the message names the abstract base factory instead of an overload. `Rijndael` and `RijndaelManaged` are reported because AES does not allow their block sizes. Types that E128071 already blocks are skipped.

```csharp
// Before (warns)
using var sha = new SHA256Managed();

// After
using var sha = SHA256.Create();
```

### E128143: Use an RSA key of at least 3072 bits or an approved curve

Flags an RSA key below 3072 bits and an elliptic curve key below 384 bits. Approved curves are P-384 and P-521. A key size or a curve name that the rule cannot read at compile time is reported, including a key size created by name, imported from parameters, a handle, a blob, PEM text or a certificate, and a curve held by friendly name, object identifier, parameter reference or struct initializer. A factory or constructor without a curve argument uses the default P-256 curve and is reported.

```csharp
// Before (warns)
using var rsa = RSA.Create(2048);

// After
using var rsa = RSA.Create(3072);
```

### E128144: Use an authenticated cipher mode

Flags `CipherMode.ECB`, `CipherMode.OFB` and `CipherMode.CFB`, and the `EncryptEcb`, `DecryptEcb`, `EncryptCfb` and `DecryptCfb` methods. ECB reveals plaintext structure. Use authenticated encryption such as AES-GCM. A comparison reads the mode and is not reported.

```csharp
// Before (warns)
aes.Mode = CipherMode.ECB;

// After
using var gcm = new AesGcm(key, 16);
```

### E128145: Use a strong PBKDF2 configuration

Flags a PBKDF2 iteration count below 600,000 and an overload that defaults to 1,000 iterations. It also flags SHA-1 as the PRF and an iteration count that the rule cannot read at compile time. An `IterationCount` assignment on the same object replaces the default-count report.

```csharp
// Before (warns)
using var kdf = new Rfc2898DeriveBytes("pw", salt, 1000, HashAlgorithmName.SHA256);

// After
using var kdf = new Rfc2898DeriveBytes("pw", salt, 600_000, HashAlgorithmName.SHA256);
```

### E128146: Use TLS 1.2 or later

Flags `SslProtocols.Ssl2`, `Ssl3`, `Tls`, `Tls11` and `Default`, and the matching `SecurityProtocolType` members. It also flags a numeric cast that sets a weak protocol bit and a complement that enables every weak protocol. A comparison and a mask removal are not reported. `AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", true)` is reported.

```csharp
// Before (warns)
var protocols = SslProtocols.Tls11;

// After
var protocols = SslProtocols.Tls12;
```

### E128147: RNGCryptoServiceProvider is deprecated

Flags `new RNGCryptoServiceProvider()` where the target framework marks the type `[Obsolete]`, as on .NET 6 and later. On .NET Framework and netstandard2.0 the type is supported and the rule stays silent.

```csharp
// Before (warns)
using var rng = new RNGCryptoServiceProvider();

// After
var bytes = RandomNumberGenerator.GetBytes(32);
```

### E128148: Do not use a cipher excluded by policy

Flags a type whose name contains a whole word from the excluded list: Blowfish, RC4, Arcfour, CAST5, CAST6, IDEA and Skipjack. Third-party libraries such as BouncyCastle expose these ciphers. `IDEA` is also an ordinary word, so it needs a cipher role word at the end of the type name, a cipher base type, a cipher interface or a cryptography namespace. `IdeaEngine` reports, and `IdeaRankingAlgorithm` does not.

The rule inspects a constructor, a static member in `Create`, `HashData`, `TryHashData` or `HashDataAsync`, and a method group. Types that E128071 already blocks are skipped, so one type produces one diagnostic.

```csharp
// Before (warns)
var cipher = new BlowfishEngine();

// After
using var aes = Aes.Create();
```

### E128149: Use the full 16-byte AES-GCM tag

Flags an `AesGcm` constructor whose `tagSizeInBytes` constant is below 16. It also flags an `Encrypt` or `Decrypt` call whose constant tag buffer is below 16 bytes. A tag size or buffer held in an unreassigned local or a readonly field resolves to its initializer.

```csharp
// Before (warns)
using var aes = new AesGcm(key, 12);

// After
using var aes = new AesGcm(key, 16);
```

### E128150: Do not use SHA-3, SHAKE or KMAC

Flags the `SHA3_*`, `Shake*`, `HMACSHA3_*` and `Kmac*` types, the `OaepSHA3_*` paddings, and a SHA-3 hash name stored in a variable. NIST SP 800-131A Rev. 2 accepts SHA-3. This rule is organizational policy, not a NIST statement. A read of `IsSupported` is not reported.

```csharp
// Before (warns)
var hash = SHA3_256.HashData(data);

// After
var hash = SHA256.HashData(data);
```

### E128151: Use an HMAC key at least as long as the hash output

Flags an HMAC key shorter than the hash output: 32 bytes for SHA-256, 48 for SHA-384 and 64 for SHA-512. The rule reads the constructor, the static `HashData` family, and a `Key` property assignment. A key whose length is not a compile-time constant is not reported.

```csharp
// Before (warns)
using var hmac = new HMACSHA256(new byte[16]);

// After
using var hmac = new HMACSHA256(new byte[32]);
```

### E128156: HttpClient certificate validation accepts every certificate

Flags a certificate validation callback that accepts every certificate. The rule reads three callback targets: `HttpClientHandler.ServerCertificateCustomValidationCallback`, `SslClientAuthenticationOptions.RemoteCertificateValidationCallback` (for example `SocketsHttpHandler.SslOptions`), and the `userCertificateValidationCallback` argument of the `SslStream` constructor. The rule reads three accepting shapes: an expression whose constant value is `true` (including `x || true`), the framework `DangerousAcceptAnyServerCertificateValidator` member, and a function that returns such an expression. The function may be a lambda, an anonymous method, or a method, local, or field that resolves to one. A callback that reads the certificate chain or the policy errors is not reported. This rule is an error, so the build fails.

```csharp
// Before (warns)
var handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
};

// After
var handler = new HttpClientHandler();
```

### E128157: Fixed-time comparison of a secret-derived value

Flags `SequenceEqual` over a byte array or a span (the extension form and the static `Enumerable` and `MemoryExtensions` forms), and a string `==`, `!=`, or `Equals`, when one side holds a secret. The equality operator on a span or a byte array compares address and length, not content, in constant time, so the rule does not report that shape. A value counts as a secret in three cases: the name ends with `Mac`, `Hmac`, `Tag`, or `Signature` in any case, the name holds one of those words as a segment, or an HMAC produced the value. An HMAC type call (including `ComputeHash` on an HMAC instance), the `Hash` property of an HMAC, an `IncrementalHash.CreateHMAC` result, and a hex or base64 encoding of such a value all count. Two string names that each hold `token`, `digest`, `expected`, `computed`, or `hash` also count, unless both hold only `hash`. A string comparison with a constant is not reported. A content hash comparison is not reported, because it is not a forgery check. The code fix handles `SequenceEqual` only and declines when an operand may be null.

```csharp
// Before (warns)
return computedTag == expectedTag;

// After
return CryptographicOperations.FixedTimeEquals(computedTag, expectedTag);
```

### E128164: Hardcoded absolute URL in source

Flags a string literal, or the text part of an interpolated string, that starts with `http://` or `https://` and carries a host. The project rules require a URL to come from an options class bound to configuration. The rule skips attribute arguments, the namespace and schema hosts `w3.org`, `schemas.microsoft.com`, `schemas.openxmlformats.org`, `schemas.xmlsoap.org`, and `json-schema.org`, and the reserved hosts `example.com`, `example.org`, and `example.net`. It also skips a property initializer on a type whose name ends in `Options`, and a `const` or `static readonly` string field: each declares one fixed value in one place, which is the shape the rule asks for. A local `const` inside a method is still reported, because it hides a value in a code path. An interpolated host such as `$"https://{host}/items"` is not reported. The rule is silent in a compilation that references xUnit, NUnit, or MSTest. No code fix.

```csharp
// Before (warns)
var uri = new Uri("https://api.contoso.com/v1");

// After
var uri = options.Value.BaseAddress;
```

### E128165: Hardcoded secret or connection string in source

Flags two shapes. The first is a string literal of 8 or more characters, with no whitespace, that is assigned to a name holding `password`, `passwd`, `pwd`, `secret`, `apikey`, `accesskey`, `privatekey`, `connectionstring`, `token`, `credential`, or `bearer`. The assignment can be a variable, a property initializer, an expression-bodied property, a property getter return, a plain assignment, an indexer key such as `["Password"]`, or a named argument. The rule looks through parentheses, `??`, and `?:`. It also reads `u8` literals. A name that ends in `Name`, `Header`, `Claim`, `Type`, `Prefix`, `Scheme`, `Path`, `Label`, `Format`, `Field`, `Param`, `Parameter`, `Setting`, `Pattern`, `Regex`, `Arn`, `Id`, `Count`, `Length`, `Expiry`, or `Hint` names a lookup key and is not reported. A literal that looks like a header name (`X-Api-Key`, `Some-Header`) or a config path (`Auth:Password`) is not reported, and neither is a value of uppercase letters joined by underscores such as `BRAVE_API_KEY`, which names a setting rather than holding a credential. The second shape is a connection string literal of `key=value` pairs separated by `;` that holds a `Server`, `Data Source`, or `Host` key and a `Password` or `Pwd` value. Space around `=` is allowed. A URL query string is not a connection string. An empty value or a `{0}` placeholder is not reported. The rule is silent in a compilation that references xUnit, NUnit, or MSTest. No code fix.

```csharp
// Before (warns)
const string AdminPassword = "hunter2hunter2";

// After
var password = options.Value.AdminPassword;
```

### E128166: Sensitive value passed to a logger call

Flags an `ILogger` call, through `LoggerExtensions`, `ILogger.Log`, or a `[LoggerMessage]` method, that passes a value named for a credential or personal-data word. The words are `password`, `passwd`, `pwd`, `secret`, `apikey`, `accesskey`, `privatekey`, `connectionstring`, `token`, `ssn`, `creditcard`, and `cardnumber`. The short words `pwd` and `ssn` match only as a whole word, so `processNumber` is not reported. The rule reads an identifier, the last segment of a member access or conditional access, and each hole of an interpolated message. It looks through a cast, parentheses, and a `ToString()` or `Trim()` call with no arguments. It reads the elements of the `params object[]` array. It skips the `EventId`, `Exception`, and message template arguments. A name that holds `cancellation`, or ends in `Name`, `Type`, `Count`, `Length`, `Expiry`, `Header`, `Id`, or `Hint`, is not reported. The rule is silent in a compilation that references xUnit, NUnit, or MSTest. No code fix.

```csharp
// Before (warns)
logger.LogInformation("Login {Password}", password);

// After
logger.LogInformation("Login for {UserId}", userId);
```

### E128167: Deferred-work comment with no work-item reference

Flags a `//` or `/* */` comment that holds the word `TODO`, `FIXME`, or `HACK` in any case and carries no work-item reference. The word must stand alone, so `hackathon` is not reported. A reference counts when it matches the regex in the `e128_tracked_ref_pattern` option. The default pattern is `#\d+`, which accepts `AB#123` and `#123`. An invalid pattern falls back to the default pattern. A match that exceeds the one-second timeout counts the text as tracked, so the rule stays silent. Sonar S1135 and S1134 report every marker, so keep them off when you turn this rule on. No code fix.

```csharp
// Before (warns)
// TODO handle the empty case

// After
// TODO(AB#1234) handle the empty case
```

```ini
# .editorconfig
e128_tracked_ref_pattern = PROJ-\d+
```

### E128168: Skipped test with no work-item reference in its reason

Flags an xUnit `[Fact]` or `[Theory]` whose `Skip` argument is a constant string that does not match `e128_tracked_ref_pattern`. An invalid pattern falls back to the default `#\d+`. A match that exceeds the one-second timeout counts the reason as tracked, so the rule stays silent. An empty reason is reported. A `Skip` value that is not a constant is not reported. No code fix.

```csharp
// Before (warns)
[Fact(Skip = "flaky on CI")]

// After
[Fact(Skip = "AB#4521 flaky on CI")]
```

### E128169: `[Obsolete]` with no message

Flags `System.ObsoleteAttribute` with no message argument, or with a null, empty, or whitespace message. The rule finds the message by position or by the name `message`, so `[Obsolete(error: true, message: "")]` is reported. Named arguments such as `DiagnosticId` do not count as a message. No code fix.

```csharp
// Before (warns)
[Obsolete]
void Old() { }

// After
[Obsolete("Use New instead.")]
void Old() { }
```

### E128170: `Split(...)[n]` index with no length guard

Flags a constant index of 1 or more taken from the result of `string.Split`. The array comes from an inline call or from a local initialized by one. `Split` returns one element when the separator is absent, so the index throws `IndexOutOfRangeException` on unexpected input. Index 0 and a from-end index such as `[^1]` are safe and not reported. A local is not reported when the same member reads `Length` on it before the index access. A `Length` read after the access does not count. The rule does not model control flow, so any earlier read counts as a guard, even one in a different branch. The rule does not track a `Split` result held in a field or a property, or one taken apart by deconstruction. A `Split` method on another type is not reported. No code fix.

```csharp
// Before (warns)
var value = line.Split(':')[1];

// After
var parts = line.Split(':');
var value = parts.Length > 1 ? parts[1] : null;
```

### E128171: Committed `Assert.Fail` red-phase stub

Flags an xUnit `[Fact]` or `[Theory]` whose only statement is a call to `Xunit.Assert.Fail`. The method can use a block body or an expression body. A `Fail` call after other assertions, and a `Fail` call in a method with no test attribute, are not reported. The stub marks unfinished work, so write the real assertions or remove the test before you commit. No code fix.

```csharp
// Before (warns)
[Fact]
public void ParsesEmptyInput() => Assert.Fail("RED");

// After
[Fact]
public void ParsesEmptyInput() => Assert.Empty(Parser.Parse(string.Empty));
```

### E128172: `catch` swallows an assertion failure

Flags a `catch` clause when its `try` block calls `Xunit.Assert` and the clause catches `Exception`, `XunitException`, or nothing in particular, and holds no `throw`. A failed assertion throws, so the catch turns a failing test into a passing one. A narrow exception type, such as `InvalidOperationException`, is not reported. A catch with a `when` filter is not reported. A catch that calls `Assert.Fail` is not reported. A `throw` inside a lambda or a local function does not count as a rethrow. No code fix.

```csharp
// Before (warns)
try { Assert.Equal(1, value); }
catch (Exception) { }

// After
Assert.Equal(1, value);
```

### E128173: `[SuppressMessage]` or `[ExcludeFromCodeCoverage]` with no justification

Flags `System.Diagnostics.CodeAnalysis.SuppressMessageAttribute` and `ExcludeFromCodeCoverageAttribute` when the `Justification` argument is missing, null, empty, or blank. The rule skips `ExcludeFromCodeCoverageAttribute` on a target framework where the attribute has no `Justification` property, which is any framework before .NET 5. A reader needs the reason in the attribute to review or remove the suppression. A suppression still needs approval under the repository rules. No code fix.

```csharp
// Before (warns)
[SuppressMessage("Design", "CA1000")]

// After
[SuppressMessage("Design", "CA1000", Justification = "The factory shape is part of the public API.")]
```

### E128174: Constructor past the `e128_max_ctor_dependencies` ceiling

Flags a constructor, or a class or struct primary constructor, that takes more dependencies than the ceiling. A parameter counts as a dependency when its type is a service-like class or interface. Values, enums, delegates, arrays (including a `params` array), `string`, `object`, `Uri`, exceptions, record types, and collection types such as `IReadOnlyList<T>` and `List<T>` do not count. The default ceiling is 7. Set `e128_max_ctor_dependencies` in `.editorconfig` to change it. A record primary constructor is not checked. No code fix.

```ini
# .editorconfig
e128_max_ctor_dependencies = 5
```

### E128175: `SemaphoreSlim` wait with no `finally` release guarantee

Flags a `SemaphoreSlim.Wait` or `WaitAsync` call that no later `try`/`finally` pairs with a `Release` call on the same semaphore. An exception between the wait and the release leaves the permit held. The rule reads the plain overloads only. A timeout overload supports the try-acquire shape, where no permit is held on the false path, so the rule skips it. The rule stays silent when an enclosing `try` already releases. No code fix.

```csharp
// Before (warns)
await _sem.WaitAsync();
await WorkAsync();

// After
await _sem.WaitAsync();
try
{
    await WorkAsync();
}
finally
{
    _sem.Release();
}
```

### E128176: Event subscription that keeps the subscriber alive

Flags an event subscription in a disposable type where the handler reads the subscriber instance and no dispose method removes the same event. The publisher holds the delegate, and the delegate holds the subscriber, so the subscriber stays reachable after its owner drops it. A handler that reads only a parameter or a local does not keep the subscriber alive, so the rule skips it. A static handler is skipped for the same reason. A subscription to an event on this instance is skipped, because the publisher dies with the subscriber. So is a subscription to an event on an instance field that `Dispose`, `DisposeAsync`, or `DisposeAsyncCore` disposes. Any `-=` on the same event inside `Dispose`, `DisposeAsync`, or `DisposeAsyncCore` clears the report. The rule does not compare the handler expression. No code fix.

```csharp
// Before (warns)
_publisher.Changed += (s, e) => _count++;

// After
_publisher.Changed += OnChanged;

public void Dispose() => _publisher.Changed -= OnChanged;
```

### E128177: Record member with a mutable collection type

Flags a record member whose type is a mutable collection, such as `List<T>`, `Dictionary<K,V>`, `HashSet<T>`, `ICollection<T>`, `IList<T>`, or an array. The generated equality compares that member by reference, so two records with equal contents compare unequal. A record that declares its own `GetHashCode` is skipped, because it owns its equality. The rule reads a positional parameter, an auto-property, and a field, because all three join the generated equality. An immutable collection, an abstract property, a computed property, a static member, and a const field are skipped. Both a record class and a record struct are checked. No code fix.

```csharp
// Before (warns)
public record Order(List<int> Lines);

// After
public record Order(ImmutableArray<int> Lines);
```

### E128178: Duplicate `JsonPropertyName` value

Flags two members of one type that carry the same `[JsonPropertyName]` value, including a collision between a member on the derived type and one it inherits. System.Text.Json throws `InvalidOperationException` at the first serialize or deserialize call, so the failure lands far from the declaration. The rule matches the attribute by symbol, so a lookalike attribute never triggers it. An ignored member, an indexer, a static member, a member that is not public and has no `[JsonInclude]`, and a member that overrides or hides the base member are skipped. A `[JsonIgnore]` with a condition other than `Always` keeps the member in the contract. The whole rule stays off when System.Text.Json is absent from the compilation. No code fix.

```csharp
// Before (warns on the second member)
[JsonPropertyName("id")] public int Id { get; init; }
[JsonPropertyName("id")] public int Key { get; init; }

// After
[JsonPropertyName("id")] public int Id { get; init; }
[JsonPropertyName("key")] public int Key { get; init; }
```

### E128179: Options type bound from configuration with no validation

Flags `services.AddOptions<T>().Bind(...)`, `AddOptions<T>().BindConfiguration(...)`, and `services.Configure<T>(configuration)` when the same chain carries no `Validate`, `ValidateDataAnnotations`, `ValidateOnStart`, or `ValidateAllProperties` call. A missing or malformed configuration value then reaches the consumer and fails at the point of use. A bare `AddOptions<T>()` and the `Configure<T>(Action<T>)` overload are skipped, because neither reads configuration. The rule matches the registration, the bind, and the validation through the option and DI extension symbols, so a user type with the same method name never triggers it. A separate `services.AddSingleton<IValidateOptions<T>, ...>` registration for the same options type clears the report. No code fix.

```csharp
// Before (warns)
services.AddOptions<RetryOptions>().Bind(configuration.GetSection("Retry"));

// After
services.AddOptions<RetryOptions>()
    .Bind(configuration.GetSection("Retry"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

### E128180: Entity Framework Core query executed inside a loop

Flags an Entity Framework Core query that executes inside a `for`, `foreach`, `while`, or `do` body. Each iteration runs one database round trip, which is the N+1 query problem. The rule reads the materializing calls, such as `ToList`, `FirstOrDefaultAsync`, `CountAsync`, `Find`, `Contains`, `Last`, `Average`, and `ExecuteDelete`. It also covers `AsEnumerable` or `AsAsyncEnumerable` that feeds a nested `foreach`, and a query syntax expression (`from ... select`) that a materializing call consumes. A `foreach` with a deconstruction pattern counts as a loop. E128066 defers every `IQueryable` receiver to this rule. The receiver must be a `DbSet<T>`, an EF queryable, or an `IQueryable<T>` chain rooted in an EF type. The `foreach` source expression and the `for` initializer run once, so neither counts as a loop body. The whole rule stays off when EF Core is absent from the compilation. No code fix.

```csharp
// Before (warns)
foreach (var id in ids)
{
    var row = await db.Rows.FirstOrDefaultAsync(r => r.Id == id);
}

// After
var rows = await db.Rows.Where(r => ids.Contains(r.Id)).ToListAsync();
```

### E128181: `HttpClient.Timeout` set to an infinite value

Flags an assignment of `Timeout.InfiniteTimeSpan` or `TimeSpan.FromMilliseconds(-1)` to `HttpClient.Timeout`. The request then never times out, so a stalled server blocks the caller and its thread pool slot. The assignment is reported in an object initializer, in a property assignment, and inside a `ConfigureHttpClient` lambda. A computed value is skipped, because the rule cannot prove it is infinite. No code fix.

```csharp
// Before (warns)
client.Timeout = Timeout.InfiniteTimeSpan;

// After
client.Timeout = TimeSpan.FromSeconds(30);
```

### E128182: `IAsyncEnumerable` value enumerated twice

Flags a second enumeration of one `IAsyncEnumerable<T>` value inside one method body. A second loop can re-run the source, and a single-pass source such as a channel reader yields nothing the second time. Enumerations in the two branches of one `if` are skipped, and an assignment to the value between two loops starts a new count. An inline-created sequence is skipped, because a factory call may return a fresh sequence. An iterator body that carries `yield` is skipped, because it hands items onward. No code fix.

```csharp
// Before (warns)
await foreach (var row in rows) { Count(row); }
await foreach (var row in rows) { Store(row); }

// After
var materialized = await rows.ToListAsync();
foreach (var row in materialized) { Count(row); }
foreach (var row in materialized) { Store(row); }
```

### E128183: `Task.WhenAny` result is never awaited

Flags a `Task.WhenAny` call whose result is discarded or stored in a local that the enclosing block never reads. The returned task never completes faulted, so a fault in every task in the set stays unobserved. An awaited, returned, yielded, stored, or passed result is skipped, because the caller may observe it there. A `WhenAny` declared on another type is skipped. No code fix.

```csharp
// Before (warns)
Task.WhenAny(first, second);

// After
var winner = await Task.WhenAny(first, second);
await winner;
```

### E128184: Recursive delete outside a recognized safe root

Flags a recursive `Directory.Delete` or `DirectoryInfo.Delete` when the path is a parameter, a field read, a property read, or a `Path.Combine` whose first argument is one of those. The severity is Info, because a false positive on a build gate blocks work for no gain.

A path under `Path.GetTempPath()`, a relative string literal, a const field, and a `Path.GetFullPath` over a safe value are recognized as safe. A local that holds one of those is also safe, and a local alias counts as the value it holds. A rooted literal reports, for example `"/etc/nginx"` or `"C:\Data"`, because the rule cannot prove the directory is disposable. A readonly field, a get-only property, and a mutable field report, because the value is computed at run time.

A guard counts only when it proves the test passed. The comparison must use `StringComparison.Ordinal` or `OrdinalIgnoreCase`, and the root must end at a separator boundary, so a prefix test cannot pass for a sibling directory. An enclosing `if` that holds the call alone or joined by `&&` counts, and so does a preceding `if` with no `else` that exits when the call is false. The guard must be the statement before the delete. A write to the guarded symbol between the guard and the delete voids the guard. A negated test around the delete and an `||` condition do not count.

A `Path.Combine` under a temp path stays silent even when a later segment comes from a variable. The rule cannot see a `..` or a rooted value in that segment. An unknown shape stays silent. No code fix.

```csharp
// Before (reports)
Directory.Delete(root, recursive: true);

// After
var temp = Path.GetTempPath();
Directory.Delete(Path.Combine(temp, "build"), recursive: true);
```

### E128185: Entity Framework Core `SaveChanges` inside a loop

Flags a `SaveChanges` or `SaveChangesAsync` call on an Entity Framework Core context inside a `for`, `foreach`, `while`, or `do` body. Each iteration costs one database round trip per row. A call in a `foreach` source expression or a `for` initializer is skipped, because it runs once. A `DbContext` type from another namespace is skipped. The rule stays off when EF Core is absent from the compilation. No code fix.

```csharp
// Before (warns)
foreach (var row in rows)
{
    context.Rows.Add(row);
    context.SaveChanges();
}

// After
foreach (var row in rows)
{
    context.Rows.Add(row);
}

context.SaveChanges();
```

### E128186: `await` inside a `finally` block

Flags an `await` expression in the body of a `finally` clause. The awaited call runs while the original exception propagates, so a fault there replaces the original failure. `await using` and `await foreach` count too, and the report sits on the `await` keyword. An `await` in a `try` block or a `catch` clause is skipped. An `await` in a local function or a lambda declared in the `finally` body is skipped. An `await` inside a nested `try` is skipped, and an `await` inside a nested `finally` reports once, at the inner clause. No code fix.

```csharp
// Before (warns)
finally
{
    await flushing.DisposeAsync();
}

// After
try
{
    await flushing.FlushAsync();
}
finally
{
    flushing.Dispose();
}
```

### E128187: `Parallel.ForEachAsync` without a degree cap

Flags a `Parallel.ForEachAsync` call that passes no `ParallelOptions` argument, or an inline `new ParallelOptions` initializer that sets no `MaxDegreeOfParallelism` member. The call then runs at most `Environment.ProcessorCount` operations at once, which is the default cap. An I/O-bound body can need a different cap, so the rule reports at Info severity. A `MaxDegreeOfParallelism` assignment of any expression silences the rule, including `-1` and a computed value. Options held in a variable, a parameter, a field, or a factory call are not reported. A user type named `Parallel` is not reported. No code fix.

```csharp
// Before (warns)
await Parallel.ForEachAsync(urls, async (url, ct) => await FetchAsync(url, ct));

// After
await Parallel.ForEachAsync(
    urls,
    new ParallelOptions { MaxDegreeOfParallelism = 8 },
    async (url, ct) => await FetchAsync(url, ct));
```

### E128188: Exception filter reads the message text

Flags a `when` filter that reads `Message`, `StackTrace`, or `ToString` on the caught exception. The filter runs before any `catch` clause, so a formatted member costs work on the fault path and can throw inside the filter itself. A filter that reads `HResult` or `InnerException` is skipped. A read on another object, a method call such as `ex.GetType()`, and a catch clause with no variable are skipped. The rule binds the receiver to the catch variable by symbol, so a lambda parameter or a local with the same name is skipped. No code fix.

```csharp
// Before (warns)
catch (HttpRequestException ex) when (ex.Message.Contains("timeout", StringComparison.Ordinal))
{
    retry = true;
}

// After
catch (HttpRequestException ex) when (ex.InnerException is TimeoutException)
{
    retry = true;
}
```

### E128189: `GCHandle` with no `Free` call

Flags a local that stores a `GCHandle.Alloc` result in the same body and never calls `Free` on it. The handle pins the target object, so a missing `Free` keeps the object reachable and pinned. The rule also reads a later assignment to the same local. A local passed to a method, returned, or stored in a field, a property, another local, or a collection is not reported, because it may leave the method. An allocation assigned directly to a field or an element, and a discarded allocation, are not reported. A bare `Alloc(x)` call that binds through `using static` is read as well. The rule stops when `GCHandle` is absent from the compilation or resolves to a user type. No code fix.

```csharp
// Before (warns)
var handle = GCHandle.Alloc(payload, GCHandleType.Pinned);
IntPtr address = handle.AddrOfPinnedObject();

// After
var handle = GCHandle.Alloc(payload, GCHandleType.Pinned);
try
{
    IntPtr address = handle.AddrOfPinnedObject();
}
finally
{
    handle.Free();
}
```

### E128190: `FileStream` created without asynchronous I/O

Flags a `FileStream` construction that cannot run asynchronous I/O. The call passes `useAsync: false` or a `FileOptions` value with no `FileOptions.Asynchronous` bit. A constructor that has neither parameter, such as `new FileStream(path, mode, access, share)`, is not reported. An async read then runs as blocking work on a pool thread. A `useAsync` value of `true`, an options value with `FileOptions.Asynchronous`, and a non-constant argument are skipped. The `SafeFileHandle` overload has no asynchronous switch, so the rule skips it. A user type named `FileStream` is skipped. No code fix.

```csharp
// Before (warns)
using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, false);

// After
using var stream = new FileStream(
    path,
    FileMode.Open,
    FileAccess.Read,
    FileShare.Read,
    bufferSize: 4096,
    useAsync: true);
```

### E128191: PFX import without `EphemeralKeySet`

Flags an `X509Certificate2` constructor that imports a PFX file, and an `X509CertificateLoader.LoadPkcs12`, `LoadPkcs12FromFile`, `LoadPkcs12Collection`, or `LoadPkcs12CollectionFromFile` call, when the flags omit `X509KeyStorageFlags.EphemeralKeySet`. Without the flag, the import persists the private key in the key store, and the key outlives the process. The flag is not supported for PFX loading on macOS. A flags constant that includes `EphemeralKeySet` and a non-constant flags value are skipped. A constructor that takes a byte array, and a constructor with no flags parameter, are skipped. No code fix.

```csharp
// Before (warns)
using var certificate = new X509Certificate2(
    path,
    password,
    X509KeyStorageFlags.MachineKeySet);

// After
using var certificate = new X509Certificate2(
    path,
    password,
    X509KeyStorageFlags.EphemeralKeySet);
```

### E128192: `GetBuffer` on a `MemoryStream` with a hidden buffer

Flags a `MemoryStream.GetBuffer()` call whose receiver was created from a `byte[]` without `publiclyVisible: true`. The call throws `UnauthorizedAccessException` for such a stream. The empty and capacity constructors expose the buffer, so the rule skips them. The rule follows a local to its creation in the same body, and a parameter to an in-body assignment. A stream created with `publiclyVisible: true`, a non-constant flag, and a receiver that is a field, a property, or a method result are skipped. No code fix.

```csharp
// Before (warns)
var buffer = new MemoryStream(new byte[8]);
byte[] raw = buffer.GetBuffer();

// After
var buffer = new MemoryStream(new byte[8]);
byte[] raw = buffer.ToArray();
```

### E128193: Disposable field overwritten in a loop without a dispose

Flags an assignment to a field whose type implements `IDisposable` or `IAsyncDisposable` inside a loop body, when no earlier dispose call on the same field appears inside the same loop. A field whose type is a type parameter with an `IDisposable` constraint counts as disposable. The overwritten instance keeps its unmanaged resources until the finalizer runs. A dispose call on the same field before the assignment, inside the same loop, clears the report, including `field?.Dispose()` and `this.field?.Dispose()`. The suppression requires a dispose inside the same loop. A dispose before the loop, or anywhere outside the loop body, does not clear the report. A local, a parameter, a property, a compound assignment, a value type, and an assignment outside a loop are skipped. No code fix.

```csharp
// Before (warns)
foreach (var item in items)
{
    _current = new Subscription(item);
}

// After
foreach (var item in items)
{
    _current?.Dispose();
    _current = new Subscription(item);
}
```

### E128194: `Lazy<T>` field without thread safety

Flags a `Lazy<T>` field whose initializer passes `LazyThreadSafetyMode.None`. Two threads can then build the value twice, and each caller gets a different instance. The `Lazy<T>(factory, false)` overload means the same thing and is reported too. An omitted mode, `ExecutionAndPublication`, `PublicationOnly`, and a non-constant mode are skipped, because the default is thread safe. The rule reads a field declarator only, so a local, a parameter, and a property initializer are skipped. A user type named `Lazy<T>` is skipped. No code fix.

```csharp
// Before (warns)
private static readonly Lazy<Cache> Shared =
    new(() => new Cache(), LazyThreadSafetyMode.None);

// After
private static readonly Lazy<Cache> Shared = new(() => new Cache());
```

### E128195: Configuration key absent from the appsettings files

Flags a constant configuration key that no `appsettings*.json` additional file defines. The rule reads only those files, and a file that fails to parse is skipped. The rule reads the key from an `IConfiguration` indexer, from `GetSection` or `GetRequiredSection`, and from `GetConnectionString`, which maps to `ConnectionStrings:<name>`. A section read needs the section prefix, not a leaf. A read through an `IConfigurationSection` is relative to that section, and an indexer assignment defines a key, so the rule skips both. The JSON reader accepts comments and trailing commas. A key comparison ignores case, because the configuration provider ignores case. A non-constant key and a receiver that is not an `IConfiguration` are skipped. The rule stays off when the project passes no parsable `appsettings*.json` additional file.

The project must pass the file in. A key from an environment variable, a user secret, or a file outside the project is not visible to the rule.

```xml
<ItemGroup>
  <AdditionalFiles Include="appsettings.json" />
</ItemGroup>
```

A key that a run-time source supplies needs an entry in the `e128_added_configuration_keys` option. The option holds comma-separated keys and adds them on top of the parsed files. The option never activates the rule on its own.

```ini
# .editorconfig
e128_added_configuration_keys = FeatureFlags:Beta, External:Endpoint
```

```csharp
// Before (warns when no appsettings file defines the key)
var level = configuration["Logging:LogLevel:Default"];

// After
var level = configuration.GetRequiredSection("Logging:LogLevel")["Default"];
```

### E128196: JsonSerializerOptions mutated after first use

Flags a write to a `JsonSerializerOptions` member after the same method body passes the instance to `JsonSerializer.Serialize` or `JsonSerializer.Deserialize`. The serializer freezes the instance on first use, so the next write to `WriteIndented`, `PropertyNamingPolicy`, a converter, or any other member throws `InvalidOperationException`. A write before the first call is not reported, and a write to an instance that the body never uses is not reported. The rule reads a local, a parameter, and a field. A serializer call inside a lambda or a local function, and a call in the other branch of the same `if`, do not count as an earlier use. Assigning a new instance to the same variable between the call and the write resets the check. A property receiver is skipped, because the getter can return a new instance on each read. A use in one method and a write in another method stay silent, because the rule reads one body at a time.

```csharp
// Before (warns)
var options = new JsonSerializerOptions();
var json = JsonSerializer.Serialize(value, options);
options.WriteIndented = true;

// After
var options = new JsonSerializerOptions { WriteIndented = true };
var json = JsonSerializer.Serialize(value, options);
```

### E128197: Fixed day count used as a calendar month

Flags a `TimeSpan.FromDays(28)` to `TimeSpan.FromDays(31)` call added to or subtracted from a `DateTime` or a `DateTimeOffset`. The span stands in for a calendar month, and a calendar month holds 28 to 31 days, so the result drifts from the calendar. The rule reads the `Add` and `Subtract` methods, the `+` and `-` operators, and the `+=` and `-=` assignments. The severity is Info, because a 30-day span is often a fixed retention window. It reads an inline call with a constant argument only. A span held in a variable, a constant field, or a computed value is not reported, because the rule cannot tell a month from a 30 day window. `DateTime.AddDays(30)` is not reported either, because the caller asked for days.

```csharp
// Before (warns)
var due = today.Add(TimeSpan.FromDays(30));

// After
var due = today.AddMonths(1);
```

### E128198: Substring count built from a length minus a constant

Flags a two-argument `string.Substring` call whose count is a `Length` value minus a positive constant. A string shorter than the constant makes the count negative, and the call throws `ArgumentOutOfRangeException`. The count shapes are `Length - k`, `Length - start - k`, and the same forms in parentheses. A length test on the same string in an enclosing `if` condition or conditional expression clears the report. So does a preceding `if` with no `else` that returns, throws, continues, or breaks after a length test on the same string. A plain comparison decides the branch: `Length > k` guards the then branch, and `Length < k` guards the else branch. The rule reads one call site, so a guard in a caller method does not clear it. A subtraction whose right side is a variable is not reported, because a `start` index that `IndexOf` returns needs its own check.

```csharp
// Before (warns)
var body = text.Substring(0, text.Length - 1);

// After
var body = text.Length > 0 ? text.Substring(0, text.Length - 1) : text;
```

### E128076 &mdash; Materialize QuerySelectorAll result before iterating

Flags a `foreach` over an AngleSharp `QuerySelectorAll` result that does not pass through `ToList()` or `ToArray()`. The loop also reports when it reads a local variable initialized from an unmaterialized call. A call inside a lambda in the initializer is not reported. The collection is live, so a DOM change inside the loop moves elements that the loop has not visited yet.

```csharp
// Before (warns)
foreach (var row in document.QuerySelectorAll("tr"))
{
    row.Remove();
}

// After
foreach (var row in document.QuerySelectorAll("tr").ToList())
{
    row.Remove();
}
```

### E128077 &mdash; TextContent string match requires a preceding length guard

Flags a `Contains`, `StartsWith`, `EndsWith`, or `Equals` call on an AngleSharp `TextContent` value when the same `&&` chain holds no length guard. A comparison against 100 or fewer characters counts anywhere in the chain. A comparison against more than 100 characters counts only when it comes before the match. `TextContent` covers every descendant node, so an ancestor element can match text that belongs to a child.

```csharp
// Before (warns)
if (block.TextContent.Contains("cookie"))
{
    block.Remove();
}

// After
if (block.TextContent.Length < 200 && block.TextContent.Contains("cookie"))
{
    block.Remove();
}
```

### E128078 &mdash; GetAttribute("href") on element that does not carry href

Flags `GetAttribute("href")` when the receiver is a local variable set from `QuerySelector` or `QuerySelectorAll` with a literal selector. Only `a`, `link`, `area`, and `base` elements carry `href`. The call returns null on any other element.

```csharp
// Before (warns)
var div = document.QuerySelector("div");
var href = div.GetAttribute("href");

// After
var link = document.QuerySelector("a");
var href = link.GetAttribute("href");
```

No code fix. Change the selector, or read a different attribute.

### E128079 &mdash; CompositeDetection with single generic ID selector lacks specificity

Flags a `CompositeDetection` built from a collection expression that holds one `ResourceDetection` branch with a bare CSS ID selector such as `#content` or `#main`. Such a selector matches unrelated sites. Add a domain or meta-tag branch, or a second resource branch.

```csharp
// Before (warns)
var detection = new CompositeDetection([new ResourceDetection("#content")]);

// After
var detection = new CompositeDetection(
[
    new DomainDetection("example.com"),
    new ResourceDetection("#content")
]);
```

No code fix.

### E128080 &mdash; Use ByteSize for data-size values

Flags a numeric property, parameter, field, or local variable whose name ends with a data-size suffix. The suffix list covers `Bytes`, `Kb`, `Kilobytes`, `Mb`, `Megabytes`, `Gb`, `Gigabytes`, `Tb`, `Terabytes`, `FileSize`, `MaxSize`, `CacheSize`, `BufferSize`, and `ChunkSize`. A nullable numeric reports too. Members declared inside the `ByteSize` type are exempt. `ByteSize` comes from `Pug.Core.Classes`. The rule reports at error.

```csharp
// Before (error)
public int MaxBufferSizeMb { get; init; }

// After
public ByteSize MaxBufferSize { get; init; }
```

### E128081 &mdash; Use StringBuilderPool instead of `new StringBuilder()`

Flags every `new StringBuilder()` and every target-typed `new()` that produces a `System.Text.StringBuilder`. The rule runs only when the compilation references `Pug.Core.Text.StringBuilderPool`. Code inside the `StringBuilderPool` class is exempt. The rule reports at info. A code fix rewrites the creation to the pooled form.

```csharp
// Before (info)
var builder = new StringBuilder();

// After
var builder = StringBuilderPool.Shared.Rent();
```

### E128082 &mdash; Do not unwrap ByteSize via cast

Flags a cast to a numeric type of a `Bytes`, `Bits`, `Kilobytes`, `Megabytes`, `Gigabytes`, or `Terabytes` property on a `ByteSize` value. The cast removes the unit from the type system. Compare `ByteSize` values directly, or format the value.

```csharp
// Before (warns)
if ((long)limit.Bytes > 1024) { }

// After
if (limit > threshold) { }
```

### E128088 &mdash; Switch over an enum does not case every member

Flags a `switch` statement or switch expression over a non-nullable enum. The rule reports when at least one member has no case and the switch has no `default`, discard, or `var` arm. An `or` pattern counts for each of its operands. A nullable enum is not checked. A new enum member otherwise falls through silently.

```csharp
// Before (warns)
var label = color switch
{
    Color.Red => "red",
    Color.Green => "green"
};

// After
var label = color switch
{
    Color.Red => "red",
    Color.Green => "green",
    _ => "unknown"
};
```

### E128094 &mdash; Pragma warning disable placed before a file-scoped namespace declaration

Flags a `#pragma warning disable` that sits above `namespace X;`. The directive sits outside the namespace it was meant to scope. IDE0079 treats it as unnecessary and removes it during format, so the suppression stops working. The rule reports at error.

```csharp
// Before (error)
#pragma warning disable CS1591
namespace App;

// After
namespace App;
#pragma warning disable CS1591
```

### E128096 &mdash; Call async local function with await instead of blocking

Flags `.Result` or `.Wait()` on a call to a local function declared `async`. VSTHRD002 covers async methods but not local functions. The rule skips a call site that is already inside an async method or an async local function.

```csharp
// Before (warns)
async Task<int> FetchAsync() => await GetValueAsync();
int value = FetchAsync().Result;

// After
int value = await FetchAsync();
```

### E128097 &mdash; Comment inside test code

Flags every `//`, `/* */`, and `///` comment inside a class that declares an xUnit `[Fact]` or `[Theory]` method. A license header or a doc comment on the type itself stays out of scope. The test name and the assertions already state what the test checks.

```csharp
// Before (warns)
[Fact]
public void ParsesValue()
{
    // Validates the happy path
    Assert.Equal(42, Parse("42"));
}

// After
[Fact]
public void ParsesValue()
{
    Assert.Equal(42, Parse("42"));
}
```

### E128098 &mdash; Collapse chained or looped string Replace calls into a single pass

Flags two or more chained `string.Replace` calls at the outermost call. Also flags a `while` loop whose condition calls `Contains` on a string and whose body assigns `s = s.Replace(...)` to that string. Each call allocates a new string and copies the whole value.

```csharp
// Before (warns)
var cleaned = input.Replace("a", "").Replace("b", "");

// After
var builder = new StringBuilder(input.Length);
foreach (var c in input)
{
    if (c is not ('a' or 'b'))
    {
        builder.Append(c);
    }
}
var cleaned = builder.ToString();
```

### E128099 &mdash; Wait for process exit with a provable timeout

Flags `Process.WaitForExit()` and `Process.WaitForExitAsync()` with no argument. Also flags a single-token `WaitForExitAsync(token)` when the token is an enclosing method parameter or `CancellationToken.None`. A token from a `CancellationTokenSource` local counts as timeout-backed.

```csharp
// Before (warns)
process.WaitForExit();

// After
if (!process.WaitForExit(TimeSpan.FromSeconds(30)))
{
    process.Kill();
}
```

### E128100 &mdash; Catch filter reads negated token state on OperationCanceledException

Flags a `when` filter of the form `!token.IsCancellationRequested` on a `catch` clause for `OperationCanceledException` or `TaskCanceledException`. The token state read races with the throw, so the filter can swallow a real cancellation. Rethrow without a filter instead.

```csharp
// Before (warns)
catch (OperationCanceledException) when (!token.IsCancellationRequested)
{
    return Result.Failed;
}

// After
catch (OperationCanceledException)
{
    throw;
}
```

### E128101 &mdash; Verify process output file exists before use

Flags `File.ReadAllText`, `ReadAllBytes`, `ReadAllLines`, `OpenRead`, `Open`, `OpenText`, and the async forms when the call follows `Process.WaitForExit` or `Process.WaitForExitAsync` in the same method. An earlier `File.Exists` or `FileInfo.Exists` check for the same path suppresses the report. A failed binary or a wrong flag leaves the output missing.

```csharp
// Before (warns)
process.WaitForExit();
var report = File.ReadAllText(outputFile);

// After
process.WaitForExit();
if (!File.Exists(outputFile))
{
    throw new InvalidOperationException("The process did not write the output file.");
}
var report = File.ReadAllText(outputFile);
```

### E128103 &mdash; DI resolve or named HttpClient resolve with no registration

Flags `GetRequiredService<T>()` and constructor parameters of registered types when no `Add*` or `TryAdd*` call in the same compilation registers the type. The rule reports at compilation end. `GetService<T>()` is not flagged, because it returns null on a miss.

Registration in another assembly needs an entry in the `e128_registered_services` option. The option holds comma-separated simple type names. Framework services such as `ILogger`, `IHttpClientFactory`, and `IConfiguration` need no entry. An open-generic registration matches by name.

The rule also flags `IHttpClientFactory.CreateClient(name)` when no `AddHttpClient(name, ...)` call in the compilation registers that name. An empty name and the unnamed overload are skipped, because both select the default client. A non-constant name is skipped. The rule reports one diagnostic per name, at the first use. No code fix.

```csharp
// Before (warns)
services.AddHttpClient("billing", client => client.BaseAddress = billingUri);
var http = factory.CreateClient("bililng");

// After
services.AddHttpClient("billing", client => client.BaseAddress = billingUri);
var http = factory.CreateClient("billing");
```

### E128104 &mdash; Assertion with literal operands

Flags an xUnit `Assert` call whose every argument is a literal, such as `Assert.Equal("alpha", "alpha")`. Such an assertion passes or fails without the code under test. `Skip`, `SkipWhen`, `SkipUnless`, and `Fail` are not checked. A named constant is not a literal, so a comparison of two constants stays allowed. No code fix.

```csharp
// Before (warns)
Assert.Equal("alpha", "alpha");

// After
Assert.Equal("alpha", subject.Name);
```

### E128105 &mdash; Test method calls an out-of-repo resolver without a skip guard

Flags a `[Fact]` or `[Theory]` method that calls a resolver named in the `e128_out_of_repo_resolvers` option and carries no skip guard. The option holds comma-separated `Type` or `Type.Member` entries. A bare `Type` entry matches every member of that type. A guard is an `Assert.Skip`, `Assert.SkipWhen`, or `Assert.SkipUnless` call before the resolver call, or a `Skip*` property on the test attribute. With no option entry the rule stays silent. No code fix.

```csharp
// Before (warns)
[Fact]
public void ReadsSharedFixture()
{
    var fixture = FixtureLocator.Load("shared");
    Assert.NotNull(fixture);
}

// After
[Fact]
public void ReadsSharedFixture()
{
    Assert.SkipUnless(FixtureLocator.Exists("shared"), "The shared fixture is absent.");
    var fixture = FixtureLocator.Load("shared");
    Assert.NotNull(fixture);
}
```

### E128106 &mdash; Index a collection with a bound taken from a different collection

Flags an element access whose index comes from an `Enumerable.Range(0, other.Count)` lambda parameter, or from a `for` loop whose condition reads `other.Count` or `other.Length`, when the indexed collection is a different symbol. A relational comparison against the indexed collection's own `Count` or `Length` in the same scope suppresses the report. A ragged pair throws `IndexOutOfRangeException` on the first short collection. No code fix.

```csharp
// Before (warns)
var cells = Enumerable.Range(0, columns.Count).Select(i => rows[i]).ToList();

// After
var cells = Enumerable.Range(0, columns.Count)
    .Select(i => i < rows.Count ? rows[i] : default)
    .ToList();
```

### E128152: Complexity ceiling: nested loops and non-memoized recursion

Flags two shapes whose cost grows past the configured ceiling. Loop depth counts only loops whose trip count varies. A loop bounded by a constant, an array created with a constant length, or a constant collection expression does not raise the tier. A coordinate step adds no depth either: when an inner loop's trip count reads an element the enclosing loop produced, such as a cell inside a row, the enclosing loop does not count, because the pair costs one visit per element rather than one per pair. Two varying loops infer `quadratic`, three infer `cubic`, and four or more infer `cubic or worse`. A method that calls itself two or more times and passes a parameter made smaller by a constant step infers `exponential`. Recursion that halves its argument, such as binary search, stays silent. Recursion counts as memoized only when the body both reads and writes the same field or `IDictionary`-typed local. Reach is direct same-type recursion only. A single self-call such as tree or tail recursion stays silent, and a single loop stays silent. The diagnostic anchors on the inner loop or on the recursive method declaration.

Two code actions serve this rule. **Suppress with a justification comment** wraps the reported loop, or the reported recursive method, in a `#pragma warning disable` and `#pragma warning restore` pair. The disable line carries `// Justification: state why this site is output-bound`, which E128047 accepts. Replace that sentence with the reason. **Replace the nested scan with a set lookup** rewrites a two-deep equality scan as a `HashSet` lookup. The second action appears only when the outer loop is top level, the inner body is one `==` guard that returns on a match, the inner comparison side is exactly the iteration variable, and the method never mentions the inner collection anywhere else.

Set the allowed ceiling with `e128_max_complexity` in `.globalconfig` or `.editorconfig`. Named tiers: `linear`, `linearithmic`, `quadratic`, `cubic`, `exponential`. Default `linear`, so linearithmic and worse fires. In-loop lookups, sorts, insertions, and string concatenation keep their dedicated rules (E128066, E128067, E128068, E128069, E128153) and stay out of this rule. At ceiling `exponential` neither shape can fire.

```csharp
// Before (warns)
foreach (var a in items)
{
    foreach (var b in items)
    {
        _ = a.Length + b.Length;
    }
}

// After — hoist the outer work, or track the inner iteration on a set
var seen = new HashSet<string>(items);
foreach (var a in items)
{
    Process(a);
}
```

### E128154: Singleton service depends on a scoped service

Flags an `AddSingleton` registration whose implementation type takes a constructor parameter of a type that an `AddScoped` call registers in the same compilation. The rule also reads `TryAddSingleton`, `TryAddScoped`, `AddKeyedSingleton`, `AddKeyedScoped`, `AddHostedService<T>`, and the `typeof(...)` overloads. The container builds the singleton once, so it keeps the first scoped instance for the life of the application. The rule reads a direct constructor dependency only. A parameter typed `IServiceProvider`, `IServiceScopeFactory`, or `IServiceScope` is not reported. A registration with a factory or an instance argument is not reported, because the container then receives a built instance or picks the constructor at run time. A service key argument does not count as such an argument.

```csharp
// Before (warns)
services.AddScoped<Repository>();
services.AddSingleton<Worker>();

// After
services.AddScoped<Repository>();
services.AddScoped<Worker>();
```

The rule also carries a second descriptor on the same id. Flags a `Microsoft.EntityFrameworkCore.DbContext` registered through `AddSingleton`, `TryAddSingleton`, `AddKeyedSingleton`, or a `typeof(...)` overload. The rule also flags `AddDbContext<T>` or `AddDbContextPool<T>` with a constant `ServiceLifetime.Singleton` lifetime. A `DbContext` tracks every entity it materializes, so a singleton context keeps those entities for the life of the application and shares them across requests. The default `AddDbContext<T>()` registration stays silent, because it registers the context as scoped. A lifetime held in a variable or a parameter is not reported. The rule stays off when EF Core is absent from the compilation.

```csharp
// Before (warns)
services.AddSingleton<AppDbContext>();

// After
services.AddDbContext<AppDbContext>();
```

### E128155: Mutating the loop source inside its own foreach

Flags a call in a `foreach` body that mutates the collection the loop enumerates. The rule reads a local, a field, or a parameter as the loop source. A mutating call includes `Add`, `AddRange`, `Clear`, `Insert`, `Remove`, `RemoveAt`, `RemoveAll`, `RemoveRange`, `Reverse`, and `Sort`. An assignment to an index of a non-array collection is reported too, but an array element write is not. A mutation directly followed by `break` or `return` in the same block is not reported. The enumerator throws `InvalidOperationException` on the next step. An immutable, frozen, or concurrent collection is not reported. A mutation inside a lambda or a local function is not reported, because it runs after the loop. The report sits on the loop source, so one loop reports once. The code fix materializes the source with `ToArray()`.

```csharp
// Before (warns)
foreach (var item in items)
{
    items.Add(item);
}

// After
foreach (var item in items.ToArray())
{
    items.Add(item);
}
```

### E128159: TaskCompletionSource without RunContinuationsAsynchronously

Flags a `TaskCompletionSource` creation that omits `TaskCreationOptions.RunContinuationsAsynchronously`. Without the flag, the awaiting continuation runs inside the thread that calls `SetResult`. That thread then runs foreign code, which can deadlock or stall a pool thread. A computed options value is not reported, because the rule cannot decide it. The code fix adds the flag, or combines it with an existing value by `|`. A `TaskContinuationOptions` argument binds to the `state` parameter, so the fix replaces it with the `TaskCreationOptions` flag.

```csharp
// Before (warns)
var source = new TaskCompletionSource<int>();

// After
var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
```

The rule reports a second shape. A `SetResult`, `SetException`, or `SetCanceled` call on a source stored in a field throws `InvalidOperationException` when a second caller completes the same source. A local, a parameter, a property, and a method result are not reported, because the rule cannot prove the source is shared. The safe forms `TrySetResult`, `TrySetException`, and `TrySetCanceled` return false instead of throwing.

```csharp
// Before (warns)
private readonly TaskCompletionSource<int> source = new(TaskCreationOptions.RunContinuationsAsynchronously);

void Complete() => source.SetResult(1);

// After
void Complete() => source.TrySetResult(1);
```

### E128160: Redirected process output never read before WaitForExit

Flags a `Process.WaitForExit` call in a method that redirects `RedirectStandardOutput` or `RedirectStandardError` and never reads the stream. The child blocks once the pipe buffer fills, so the wait never returns. A read method such as `ReadToEnd`, `ReadLineAsync`, or `BeginOutputReadLine` clears the report. A subscription to `OutputDataReceived` or `ErrorDataReceived` clears it too. The read or the subscription clears the report only when it runs before the wait and on the same process. The rule reads one method body or one lambda body.

```csharp
// Before (warns)
process.StartInfo.RedirectStandardOutput = true;
process.Start();
process.WaitForExit();

// After
process.StartInfo.RedirectStandardOutput = true;
process.Start();
var output = process.StandardOutput.ReadToEnd();
process.WaitForExit();
```

### E128161: Path.Combine with a rooted or parent segment

Flags a `System.IO.Path.Combine` call with a constant segment that starts from a root or climbs to a parent directory. A rooted segment drops every earlier segment, so the result escapes the base directory. The message for a rooted segment advises `Path.Join`. The message for a parent segment advises `Path.GetFullPath` on the result, then a check that it starts with the base directory, because `Path.Join` keeps `..` and does not stop traversal. The rule reads both separators and the drive-letter form. A multi-part segment is reported when its `..` parts climb above its start, so `"a/../../b"` is reported and `"a/../b"` is not. A segment that is exactly `".."` is not reported, because a caller uses it on purpose. The code fix replaces the call with `Path.Join` for a rooted segment only. A parent segment still escapes under `Path.Join`, so a report on that shape offers no fix.

```csharp
// Before (warns)
var path = Path.Combine("base", "/etc/passwd");

// After
var path = Path.Join("base", "/etc/passwd");
```

### E128162: Local used after an explicit Dispose

Flags a member call on a local after the same block calls `Dispose()` on it. The `Dispose()` statement must sit directly in the block. A `Dispose()` in a braceless `if`, `else`, loop, or `switch` section is not reported, and neither is a read in a branch that excludes the `Dispose()`. A `nameof(a)`, `a is null`, and `a == null` are not reads. The object throws `ObjectDisposedException` on the next member call that reads its state. A second `Dispose` call is not reported. An assignment to the local ends the disposed state, so the reads that follow are not reported. A dispose at the end of a `using` scope is not reported. A read inside a nested lambda or local function is not reported, because the block order does not give the time that body runs.

```csharp
// Before (warns)
var stream = new MemoryStream();
stream.Dispose();
stream.WriteByte(1);

// After
var stream = new MemoryStream();
stream.WriteByte(1);
stream.Dispose();
```

### E128163: ImmutableArray field left at its default

Flags an `ImmutableArray<T>` field that no initializer and no assignment reaches. The field keeps its default value, and the default array holds a null inner array. Every operation that reads it throws `NullReferenceException`. The rule collects assignments across the whole compilation, including a deconstruction, a `ref` or `out` argument, and an `ImmutableInterlocked` call, so a partial type that assigns the field in another file is not reported. The code fix initializes the field to `ImmutableArray<T>.Empty`.

```csharp
// Before (warns)
private ImmutableArray<int> values;

// After
private ImmutableArray<int> values = ImmutableArray<int>.Empty;
```

### E128199: Method calls itself directly

Flags a method whose body invokes the same method. The call graph becomes
cyclic, so the stack depth grows with the input. The rule matches the callee
symbol, so an overload or a same-name method on another type is not reported.
A local function that calls the outer method is reported, because that call
also re-enters the method. No code fix.

```csharp
// Before (warns)
int Down(int n) => n <= 0 ? 0 : Down(n - 1);

// After
int Down(int n)
{
    var total = 0;
    for (; n > 0; n--)
    {
        total += n;
    }

    return total;
}
```

### E128200: Do not declare a function pointer type

Flags a `delegate*` type in a field, a parameter, a return type, or a local.
This covers the managed and the `unmanaged` forms. A function pointer carries
no lifetime or calling-convention guarantee, and native code can call a
collected target. Use a delegate type. No code fix.

```csharp
// Before (warns)
delegate*<int, int> twice = &Twice;

// After
Func<int, int> twice = Twice;
```

### E128201: Conditional directives past the ceiling

Flags a file that holds more `#if` directives than the configured ceiling. Each
directive multiplies the set of build variants, so the test matrix grows past
what a team can verify. The rule reports the first directive past the ceiling.
The option `e128_max_conditional_directives` sets the ceiling and defaults to
4. A missing or non-numeric value falls back to the default. No code fix.

```csharp
// Before (warns when the file holds more than four #if directives)
#if FEATURE_A
    int A() => 1;
#endif
```

### E128202: Unvalidated JWT Claims Used as an Identity

Reports decoded JWT claims that reach an authenticated identity in `HttpContext.User` or an `AuthenticationTicket`. Token readers (`ReadJwtToken`, `ReadJsonWebToken`, and `ReadToken`) and string token constructors decode without validation. The rule follows every decoded source through local claims, identity collections, conditional access, `foreach`, and lambda inputs. It tracks `AddClaim`, `AddClaims`, `Add`, `AddRange`, `AddIdentity`, and `AddIdentities`. An identity needs an authentication type that is not constant null or empty. Subclasses count too. Claim reads from a token (`Subject`, `Claims`, `Payload`, `GetClaim`, `TryGetClaim`, and other members) count. A pattern variable counts as a local, whether it comes from an `is` expression, a `switch` statement, or a `switch` expression.

A synchronous `ValidateToken` call clears the report only when it precedes the sink and validates the same unchanged token. `ValidateTokenAsync` needs an `IsValid` check, including its `SecurityToken` overload. The rule accepts a true branch, an else branch after a failed check, or a failed-check branch that returns or throws. A bare async validation call does not clear the report. A write to the token between the validation and its check removes the clearance. A field or auto-property token, such as `request.Token`, clears only when the same receiver reads it again. A write to the member or its receiver voids the clearance. The synchronous `JsonWebTokenHandler.ValidateToken` call returns a result object, so it needs an unchanged `IsValid` check like the async form. A `ConfigureAwait` call on the validation task is accepted. A synchronous validation inside a `try` block clears the report when every catch returns or throws. A `goto` or label in the block stops the scan. Lambdas inherit earlier parent validation. Local functions inherit only parent guards that contain their declaration.

A later local definition hides earlier definitions only when its block contains the use. Claims and identity additions count after the sink because the sink retains references. Compound assignment, deconstruction, and `ref` writes stop tracking the affected local after the write position. An `out` argument of a claim reader such as `TryGetClaim` defines the local instead. A `??=` assignment and a lambda write to a parent local count as definitions. A claim collection that a later `Add` or `AddRange` call fills does not reach an identity that already copied it. A captured local that the parent redefines after a lambda or local function counts. A conditional or coalesce principal reports per branch. A definition later in a shared loop reaches the sink on the next iteration, unless a later definition in the same loop replaces it or the loop declares the local. A token read after the sink needs validation before that read. A token rewritten after the sink voids a validation that sits outside the loop. The rule skips a local function that no call or method group reaches from the method body. It does not follow helper-method flows. No code fix applies because validation needs application policy.

```csharp
// Before (warns)
var parsed = handler.ReadJwtToken(token);
context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));

// After
context.User = handler.ValidateToken(token, parameters, out _);
```

## Configuration

Override severity per rule in `.editorconfig`:

```ini
# Promote to error
dotnet_diagnostic.E128008.severity = error

# Disable a rule
dotnet_diagnostic.E128005.severity = none

# Set the complexity ceiling for E128152 (linear, linearithmic, quadratic, cubic, exponential)
e128_max_complexity = linearithmic

# Set the conditional directive ceiling for E128201
e128_max_conditional_directives = 4

# Accept configuration keys that no appsettings file defines (E128195)
e128_added_configuration_keys = FeatureFlags:Beta, External:Endpoint
```

E128195 reads the `appsettings*.json` files that the project passes through `AdditionalFiles`. A file that fails to parse is skipped. The `e128_added_configuration_keys` option adds keys on top of the parsed files, so it never activates the rule alone. Add each file to the project:

```xml
<ItemGroup>
  <AdditionalFiles Include="appsettings.json" />
  <AdditionalFiles Include="appsettings.Development.json" />
</ItemGroup>
```

### E128203: HTTP request inside a loop

Flags an `HttpClient` request call inside a loop body. Each iteration pays a
network round trip, which is the N+1 request shape. The rule does not require an
`await`, because the round trip is paid either way.

The rule reads `SendAsync`, `GetAsync`, `PostAsync`, `PutAsync`, `DeleteAsync`,
and `PatchAsync`. The receiver must derive from `HttpClient`.

Exclusions:
- A `for` loop with a numeric literal bound.
- A request in a loop header, which runs once.
- A loop body that calls `Task.Delay` or `Thread.Sleep`, which marks a retry or
  poll loop.
- A lambda or local function body.

No code fix. The batch contract is application policy.

### E128204: Database command inside a loop

Flags a `DbCommand` execution inside a loop body. Each iteration pays a database
round trip. The rule does not require an `await`, because the round trip is paid
either way.

The rule reads `ExecuteNonQueryAsync`, `ExecuteScalarAsync`, and
`ExecuteReaderAsync`. The receiver must derive from `DbCommand`. E128093 owns the
synchronous method shape, so this rule does not report it.

Exclusions:
- A `for` loop with a numeric literal bound.
- A call in a loop header.
- A loop body that calls `Task.Delay` or `Thread.Sleep`.

No code fix. The batch boundary depends on the transaction scope.

### E128205: Collection filled in a loop with a known count

Flags a `List<T>`, `Dictionary<K,V>`, `HashSet<T>`, `Queue<T>`, or `Stack<T>`
created with no capacity, when the next statement fills it in a loop with a
known count.

```csharp
List<int> result = new();
foreach (int item in source)
{
    result.Add(item);
}
```

The fix passes the count to the constructor:

```csharp
List<int> result = new(source.Count);
```

The loop must be the next statement. The count comes from the `foreach` source
or from a `Count` or `Length` bound in a `for` condition. The source expression
must be a chain of names, not a call, so the fix never runs it twice. The
resolved type must expose a public `(int)` constructor, so `SortedSet<T>` and
`SortedDictionary<K,V>` stay silent.

Exclusions:
- A loop with a `break` that exits this loop. A break inside a nested loop or a
  `switch` section does not count.
- A collection that is not filled by the type's own fill method, which is `Add`,
  `Enqueue`, or `Push`.
- A capacity already set in an object initializer.

### E128206: Empty loop body

Flags a `while` or `do` loop with an empty body. The loop spins a core until
another thread changes the condition.

The rule stays silent when the condition calls a method. A `while (reader.Read())`
loop does its work in the condition, so it is not a spin.

No code fix. The signal type is a caller decision.

### E128207: Reflection lookup inside a loop

Flags a `System.Type` member lookup inside a loop body. The lookup walks metadata
and allocates on every iteration.

The rule reads `GetProperty`, `GetMethod`, `GetField`, `GetMember`,
`GetConstructor`, `GetEvent`, `GetNestedType`, and `GetInterface`.

Exclusions:
- A lookup that references a loop variable, as in `item.GetType()`. The receiver
  changes per iteration, so a hoist is wrong.
- A `for` loop with a literal bound.
- A lookup outside a loop. E128090 owns test methods.

No code fix. The hoisted declaration needs a caller-chosen name.

### E128208: `ContainsKey` then indexer on a `ConcurrentDictionary`

Flags `map.ContainsKey(key)` as the condition of an `if` that then reads
`map[key]`. The pair does two lookups and races with a concurrent writer that
removes the key between them.

Use `TryGetValue`:

```csharp
return map.TryGetValue(key, out int value) ? value : 0;
```

The rule reads `ConcurrentDictionary<TKey,TValue>` only. CA1854 covers
`Dictionary<K,V>`.

No code fix. The replacement depends on the `if` body and the fallback value.

### E128209: `GetBytes` length inside a loop

Flags `Encoding.GetBytes(value).Length` inside a loop body. The call allocates a
byte array only to read its length. `GetByteCount` returns the same number with
no allocation.

The fix rewrites `Encoding.UTF8.GetBytes(value).Length` to
`Encoding.UTF8.GetByteCount(value)`. The receiver must derive from `Encoding`.

No exclusion beyond the loop and the `.Length` use.

### E128210: `new Regex` inside a loop

Flags a `Regex` construction inside a loop body. Each iteration compiles the
pattern.

The rule reads every `Regex` construction inside a loop body, including a
constant pattern. The creation must not reference a value that changes per
iteration, because a hoist is then wrong. A local declared in the loop body and
a call inside the pattern both count as per-iteration values.

No code fix. The hoisted field name and the `GeneratedRegex` shape are caller
choices.

### E128211: `Task.Delay` in a loop without a token

Flags `Task.Delay` inside a loop body when the resolved overload has no
`CancellationToken` parameter. The wait does not observe cancellation.

A `Task.Delay(delay, cancellationToken)` call stays silent. So does a loop whose
condition reads a `CancellationToken`, such as
`while (!token.IsCancellationRequested)`. `Thread.Sleep` is not this rule.

No code fix. The token source is a caller decision.

### E128212: `ToCharArray` used only to index or iterate

Flags `string.ToCharArray()` when the result is the direct `foreach` source or
the receiver of an element access. The copy serves no purpose because indexing
and iteration work on the string directly.

```csharp
foreach (char c in value.ToCharArray())
```

An array assigned to a local or passed to a method stays silent.

No code fix. The span or direct-index shape is a caller choice.

### E128213: `Activator.CreateInstance` inside a loop

Flags `Activator.CreateInstance` inside a loop body. Reflection runs on every
iteration.

A `for` loop with a literal bound stays silent, because the total work is
bounded.

No code fix. The cached factory shape is a caller decision.

### E128214: HTTP handler without automatic decompression

Flags `new HttpClientHandler()` and `new SocketsHttpHandler()` when neither the
object initializer nor a later assignment on the same local sets
`AutomaticDecompression`. The default is `DecompressionMethods.None`, so a
compressed response stays compressed.

```csharp
new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }
```

A derived handler type stays silent because its constructor may set the property.
A field or property initializer stays silent, because a constructor can set the
property later.

Automatic decompression decodes any response the handler reads. Enable it for
trusted origins, and cap the response size or stream with a byte limit when the
origin is untrusted.

No code fix. The decompression method set is a caller decision.

### E128215: `StringBuilder` without capacity for a known append count

Flags `new StringBuilder()` with no arguments when the next statement is a loop
that calls `Append` or `AppendLine` on the builder and the loop count is known.
The builder grows and copies its buffer as it fills.

The count comes from the `foreach` source or a `Count` or `Length` bound in a
`for` condition. The source expression must be a chain of names, not a call.

A loop with a `break` that exits this loop stays silent, because it can stop
early. A break inside a nested loop or a `switch` section does not count. An
append inside a lambda or local function does not run during the loop, so it
does not count. A constructor with a capacity or an initial string stays silent,
and so does a capacity set in an object initializer.

No code fix. The capacity value is an estimate.

### E128216: Unbounded channel

Flags `Channel.CreateUnbounded<T>()` and
`Channel.CreateUnboundedPrioritized<T>()`. An unbounded channel grows without
limit when producers outrun consumers.

Use `Channel.CreateBounded<T>(capacity)` when the producer rate is not bounded.

No code fix. The capacity and full-mode policy are caller decisions.

### E128217: Large struct passed by value

Flags a parameter of a source-declared struct larger than 24 bytes, passed by
value. Each call copies the whole struct.

The size sums instance fields. A reference or pointer field counts as 8 bytes.
The rule handles fixed buffers, inline arrays, nullable structs, and enums.
SIMD and `System.Numerics` types stay silent.

The message reports a lower bound, because the estimate ignores padding. A
self-referential or deeply nested struct stops at a fixed depth instead of
recursing without end.

Exclusions:
- An `in`, `ref`, `out`, `params`, or `this` parameter.
- An override, an interface declaration, or an interface implementation, where
  the signature is fixed.
- A struct declared outside the current compilation, such as a framework type.
- An explicit-layout struct, whose fields overlap.

No code fix. The `in` modifier or a class change is a caller decision.

### E128218: Boolean flags encode one state

Flags a class that uses several mutable `bool` members as one state. The rule
reports when one method writes two or more of those members, or when one
condition reads two or more of them and at least one read is negated.

A member counts only when its transitions are not one-way. A member written only
to `true`, or only to `false`, is ignored. A write in a constructor or a
destructor is not a transition. A write through `Interlocked.Exchange` or
`Volatile.Write` is a transition.

The rule analyzes fields and auto-properties. A property with accessor bodies is
outside its scope.

The default minimum cluster size is three. Set `e128_min_state_flags` in
`.editorconfig` to change it. The minimum accepted value is two. The rule reads
the option per source file, so a project-level `.editorconfig` applies. The
rule stays at `Info` because a pair of independent flags can look like one
state at a single call site.

```csharp
// Before (info)
private bool _isRunning;
private bool _isPaused;
private bool _isStopped;

public void Start()
{
    _isStopped = false;
    _isRunning = true;
}

public void Pause()
{
    _isRunning = false;
    _isPaused = true;
}

public void Stop()
{
    _isPaused = false;
    _isStopped = true;
}

// After
private State _state;

public void Start() => _state = State.Running;
public void Pause() => _state = State.Paused;
public void Stop() => _state = State.Stopped;
```

No code fix. The state names and the legal transitions are domain decisions.

## Requirements

- .NET SDK 10.0+ (targets `netstandard2.0`, runs in any modern Roslyn host)
- Visual Studio 2022 17.8+, Rider 2024.1+, or `dotnet build` from CLI

## License

MIT
