# ASP.NET Core Performance
*Updated: 2026-11-11T03:14:54Z*

Rules and measurements for the `E128.Reference.Web` entry point. The general
method lives in [performance.md](performance.md).

## Request Path

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

## Response Compression

.NET 11 adds Zstandard to the response compression middleware. The default
provider order is Zstandard, then Brotli, then Gzip. A client that sends
`Accept-Encoding: zstd` gets Zstandard with no code change. Set the level with
`ZstandardCompressionProviderOptions` and `ZstandardCompressionOptions.Quality`,
which ranges from 1 to 22. Adding a provider by hand disables every default
provider, so a custom list must name all three.

This app does not call `AddResponseCompression`, so it compresses nothing. The
evaluation keeps it that way for three reasons:

- Every response is a small JSON document. A compression header and a frame
  cost more than the body.
- `EnableForHttps` defaults to false, because compression over TLS exposes the
  app to CRIME and BREACH. A production path needs a security decision before a
  flip.
- The app already took its latency win from the Warning log level.

Revisit the decision when a response body grows past several kilobytes.

## Request Logging

`Program.cs` sets the minimum log level to Warning. A per-request Information
log costs a console write and a monitor lock on the request thread.

On a Release publish over loopback, the change moved the median service time
on `GET /` from 0.072 ms to 0.062 ms at `-c 1`. It removed two log lines per
request. Throughput at `-c 20` stayed inside the noise.

## See Also

- [performance.md](performance.md): the profiling ladder and the change protocol
- [../dotnet-reference/dep-map.md](../dotnet-reference/dep-map.md): the web
  project dependencies
