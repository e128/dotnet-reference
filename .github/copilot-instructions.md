# Copilot Instructions

## .NET Conventions

- Central Package Management — versions in `Directory.Packages.props`, not per-project
- Latest C# language features (`LangVersion=latest`)
- `StringComparer.OrdinalIgnoreCase` or `CultureInfo.InvariantCulture` by default
- `string.Create(CultureInfo.InvariantCulture, ...)` for interpolated strings
- `DateTimeOffset` over `DateTime` for time zone-aware code
- Code must pass all analyzer rules (deny-by-default in `.globalconfig`)
- No copyleft dependencies
- Follow `.editorconfig` formatting

## Performance

- `Span<T>`/`Memory<T>` over arrays; `ArrayPool<T>.Shared` for transient buffers
- `ReadOnlySpan<char>` for string manipulation
- `ValueTask` for async methods with frequent sync completion
- `Interlocked` for atomic operations

## Prohibited Patterns

| Pattern                                  | Reason                    |
| ---------------------------------------- | ------------------------- |
| `async void` (non-event-handler)         | Unobservable exceptions   |
| `.Wait()` / `.Result` / `.GetAwaiter().GetResult()` | Deadlock risk   |
| `Thread.Sleep()` in async code           | Thread blocking           |
| `new HttpClient()`                       | Use `IHttpClientFactory`  |
| `DateTime.Now` / `DateTime.UtcNow`      | Inject `TimeProvider`     |
| `dynamic` in security contexts           | Type safety bypass        |
| `String.Format` with untrusted input     | Injection risk            |
| `GC.Collect()` in production             | Performance degradation   |
