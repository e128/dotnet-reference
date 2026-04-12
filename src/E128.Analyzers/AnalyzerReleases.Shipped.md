## Release 1.6.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|-------
E128031 | Reliability | Warning  | AddSingleton factory returns IDisposable
E128032 | Design      | Warning  | Concrete-only DI registration with available interface
E128033 | Reliability | Warning  | Options class bound via .Bind() has init-only property
E128034 | Reliability | Warning  | Constructor news a DI-registered type
E128035 | Reliability | Warning  | Concrete-type DI dependency without direct registration
E128036 | Design      | Warning  | Task.Run wrapping async lambda — unnecessary thread pool hop
E128037 | Reliability | Warning  | Unbounded Task.WhenAll over async Select
E128038 | Reliability | Warning  | Task.WhenAll async lambda missing CancellationToken propagation
E128039 | Reliability | Warning  | Catch filter must exclude OperationCanceledException
E128040 | Reliability | Warning  | Concurrency limit must be positive
E128041 | Reliability | Warning  | JsonDocument.RootElement must not escape the document's using scope
E128042 | Design      | Warning  | Convert.ToInt32/ToInt64 wrapping ExecuteScalar without null guard
E128043 | Style       | Warning  | Do not use the null-forgiving operator
E128044 | Design      | Warning  | Type implements IAsyncDisposable but not IDisposable
E128045 | Design      | Warning  | Avoid direct System.Console usage
E128046 | Design      | Warning  | Class has excessive user-defined inheritance depth
E128047 | Style       | Warning  | #pragma warning disable without justification comment
E128048 | Design      | Warning  | Use switch instead of if/else-if chain on enum values
E128049 | Design      | Warning  | Avoid [DynamicallyAccessedMembers] attribute
E128050 | Design      | Error    | Use TimeSpan for duration values
E128051 | Reliability | Warning  | HttpClient missing OperationCanceledException catch
E128052 | Design      | Info     | Mutable collection on public API
E128053 | Design      | Warning  | Collection-of-string path parameter

## Release 1.5.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|-------
E128027 | Performance | Warning  | Use FrozenSet/FrozenDictionary for static readonly collections
E128028 | Reliability | Warning  | Task.FromResult wraps sync I/O that has an async alternative
E128029 | Performance | Warning  | Replace multi-string OR-chain with HashSet.Contains
E128030 | Design      | Info     | Do not compare FileSystemInfo types by reference

## Release 1.4.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|-------
E128019 | Design      | Warning  | Do not pass CancellationToken by 'in' reference
E128020 | Reliability | Warning  | Do not use 'in' modifier with mutable structs
E128021 | Design      | Error    | Do not use 'in' modifier with ref struct parameters
E128022 | Design      | Warning  | Remove ConfigureAwait(false) in app hosts
E128023 | Reliability | Warning  | Avoid hardcoded /tmp path — use Path.GetTempPath()
E128024 | Style       | Warning  | Non-XML-doc comment above method declaration
E128025 | Style       | Warning  | Guid.NewGuid() used in temp file path — use GetRandomFileName
E128026 | Performance | Warning  | Redundant HashSet allocation in FrozenSet creation

## Release 1.3.5

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|-------
E128015 | Performance | Warning  | Use string interpolation instead of string.Format
E128016 | Reliability | Warning  | DateTime.Parse/ParseExact missing DateTimeStyles parameter
E128017 | Design      | Warning  | Use primary constructor parameter directly
E128018 | Performance | Warning  | Use ToArray() instead of ToList() for read-only foreach iteration

## Release 1.3.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|-------
E128011 | Reliability | Warning  | [GeneratedRegex] attribute is missing matchTimeoutMilliseconds
E128012 | Reliability | Warning  | RegexOptions.Compiled is redundant in [GeneratedRegex]
E128013 | Reliability | Warning  | [GeneratedRegex] pattern has overlapping quantifiers
E128014 | Reliability | Warning  | [GeneratedRegex] pattern has nested quantifiers

## Release 1.2.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|-------
E128009 | Performance | Warning  | Use MinBy/MaxBy instead of OrderBy().First()
E128010 | Performance | Warning  | HttpClient call missing HttpCompletionOption.ResponseHeadersRead

## Release 1.1.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|-------
E128006 | Style       | Warning  | Use Encoding.UTF8 instead of Encoding.Default
E128007 | Design      | Warning  | Avoid async void methods (non-event-handler)
E128008 | Design      | Warning  | Avoid sync-over-async (.Result / .GetAwaiter().GetResult())

## Release 1.0.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|-------
E128001 | Design      | Warning  | Use FileInfo or DirectoryInfo instead of string for file system paths
E128002 | Style       | Warning  | Use string.Empty instead of ""
E128003 | Reliability | Warning  | Use TimeProvider instead of DateTime/DateTimeOffset direct access
E128004 | Reliability | Warning  | Use IHttpClientFactory instead of new HttpClient()
E128005 | Design      | Warning  | Seal classes that have no subclasses
