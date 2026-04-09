# .NET Anti-Patterns

**Never generate these** — they compile cleanly but produce incorrect or fragile code:

- `DateTime.Now` / `DateTime.UtcNow` directly → inject `TimeProvider` via DI
- `new HttpClient()` → use `IHttpClientFactory`
- `async void` methods (non-event-handler) → use `async Task`
- `.Result` / `.GetAwaiter().GetResult()` (sync-over-async) → `await` throughout
