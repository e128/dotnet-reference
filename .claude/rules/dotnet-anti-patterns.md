# .NET Anti-Patterns

**Never generate these.** Each one compiles cleanly and produces incorrect or
fragile code.

| Never write                                  | Write instead                     | Why                                            |
| -------------------------------------------- | ---------------------------------- | ----------------------------------------------- |
| `DateTime.Now` or `DateTime.UtcNow` directly | inject `TimeProvider` through DI  | untestable, hidden ambient dependency           |
| `new HttpClient()`                           | `IHttpClientFactory`               | socket exhaustion, DNS staleness, no pipeline   |
| `async void` outside an event handler        | `async Task`                       | unobservable exceptions                         |
| `.Result` or `.GetAwaiter().GetResult()`     | `await` throughout                 | sync-over-async deadlocks                       |

Never hardcode a URL or a secret in source. See [security.md](security.md).
