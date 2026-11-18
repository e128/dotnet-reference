# Performance Rule Details: ASP.NET Core
*Updated: 2026-11-17T23:58:14Z*

Detection shape for the ASP.NET Core allocation rules E128229 to E128231. The
rest of the performance rules live in
[rule-details-performance.md](rule-details-performance.md). Each rule gates on
the API that supplies its remedy, so a compilation without ASP.NET Core gets no
report.

### E128229: Middleware next delegate closure

Flags an inline `Use` middleware whose second parameter is `Func<Task>`. The
`Func<HttpContext, Func<Task>, Task>` overload allocates a `Func<Task>` closure
per request. The `RequestDelegate` overload passes the same delegate with no
allocation.

The rule resolves `IApplicationBuilder` and `UseMiddlewareExtensions`, then
checks the delegate type by symbol. It reports the second parameter only when
every use of `next` is a call with no argument, at least one call exists, and
the context parameter carries no assignment.

The code fix writes `RequestDelegate` in the parameter type position, passes the
context to every call, and adds `Microsoft.AspNetCore.Http` when the file lacks
it.

### E128230: Static files through the runtime pipeline

Flags a zero-argument `UseStaticFiles` call. `MapStaticAssets` serves a
build-time manifest with precompressed variants and ETags.

The rule registers only when the compilation declares `MapStaticAssets`, which
gates the rule to a target framework with the endpoint mapping API. The symbol
must belong to `StaticFileExtensions`. A call that passes `StaticFileOptions`
stays silent. No code fix, because the call moves into the endpoint mapping
section and a Razor app also changes how it references the assets.

### E128231: Split result used only to enumerate

Flags a `foreach` over `string.Split` with a `char` or constant-string separator
and the default split options. `MemoryExtensions.Split` yields `Range` values
over the source with no array and no part strings.

The rule registers only when `MemoryExtensions` declares the span `Split`
extension. It reports the loop. The fix applies when the receiver is a name
chain and every read of the part is a length, an element access, or a
constant-string comparison. The fix enumerates `source.AsSpan().Split(separator)`
and reads each part as `source.AsSpan()[range]`.

A numeric parse stays reported with no fix. The narrowest `Parse` overload for
`ReadOnlySpan<char>` also takes an `IFormatProvider`, so the rewrite would add a
culture decision that the loop does not state.

## Related

- [rule-details-performance.md](rule-details-performance.md): the earlier
  performance rules and the E128135 and E128084 extensions
- [rule-details-performance-tier2.md](rule-details-performance-tier2.md): the
  waiting Tier 2 performance rules
- [rule-details.md](rule-details.md): the remaining rule details
