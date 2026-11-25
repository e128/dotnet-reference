# ASP.NET Core Performance
*Updated: 2026-11-25T17:21:02Z*

Rules and measurements for the `E128.Reference.Web` entry point. The general
method lives in [performance.md](performance.md).

## Request Path

- `Program.cs` uses `WebApplication.CreateSlimBuilder`. It omits HTTPS, HTTP/3,
  IIS integration, and hosting startup assemblies. Do not switch back to
  `CreateBuilder` without a measured need.
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

`Program.cs` uses `CreateSlimBuilder`. Two interleaved batches of eight cold
starts each tested both builds. With `CreateBuilder`, the median start was
98.3 ms and 75.1 ms. With `CreateSlimBuilder`, it was 86.8 ms and 64.9 ms. The
change saves about 10 to 12 ms per start.

The request path stayed inside the noise. `GET /` measured 4,696 and 4,803
requests per second. `GET /greetings` measured 4,791 and 4,819. The runs used
macOS arm64 on 2026-11-25. A Release build measured the time to the listening
socket, and the runs interleaved the two builds.

`CreateSlimBuilder` omits HTTPS, HTTP/3, IIS integration, and hosting startup
assemblies. This app uses none of them. Revisit the choice before the app adds
one. The source-generated JSON metadata stays for AOT readiness.

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
