# Security Rule Detail Notes
*Updated: 2026-11-17T22:44:32Z*

Detection shape and exclusions for the E128 security rules that the main rule
table cannot show. The severity and the code-fix status live in
`src/E128.Analyzers/README.md`. The shared rule notes live in
[rule-details.md](rule-details.md). The analyzer strategy lives in
[analyzers.md](../analyzers.md).

### E128227: HTTP body read without a size bound

Flags `HttpContent.ReadAsStringAsync` and `HttpContent.ReadAsByteArrayAsync`.
Both buffer the whole response body in memory, so an untrusted server can answer
with a body that exhausts the heap.

The rule matches the method symbol, so a user type with the same method name
stays silent. A body bound counts when it sits in the enclosing method body or
the enclosing lambda body, and the rule resolves the bound by symbol, not by
name text:

- A resolved `HttpContent.LoadIntoBufferAsync` call whose first parameter is
  `long`, with or without a trailing `CancellationToken`. The parameterless
  overload and the `CancellationToken`-only overload do not bound anything.
- A null-safe `ContentLength` comparison on the same receiver chain. The check
  must precede and dominate the read. A read inside the `then` branch needs a
  condition that is false when `ContentLength` is `null`. A read outside it
  needs a condition that is true when `null` and a `then` branch that throws
  or exits, so a chunked response fails closed. A plain `>` guard is false
  when `null`, so it never bounds the read.
- A local whose initializer is a `ContentLength` access on the same chain
  counts as the bound variable.

A `MaxResponseContentBufferSize` setting does not bound a direct content read.
The setting caps the client buffer only, so a read through
`HttpContent.ReadAsStringAsync` stays unbounded. A bound in a different method or
type is invisible, so a client configured in a composition root needs a bound in
the read scope or an analyzer suppression at the read site.

An in-memory content type needs no bound: `ByteArrayContent`, its derived
`StringContent` and `FormUrlEncodedContent`, `ReadOnlyMemoryContent`, and
`JsonContent`. `StreamContent` and `MultipartContent` stay in scope, because
their source size is unknown at the read site.

`TextReader.ReadToEndAsync` stays out of scope. A file or console reader has no
local cap to detect, and every file read would report. The rule carries no code
fix, because the cap value is a caller decision.

`tests/.globalconfig` and `benchmarks/.globalconfig` set E128227 to `none`. A
test reads a fixed response from its own app, and a benchmark must not add a
check to the measured path.

The rule needs `System.Net.Http.HttpContent`. Without that type it registers no
action, so a consumer with no HTTP reference gets no report.

### E128228: Token validation flag set to constant false

Flags an assignment of a literal `false` to a validation flag on
`Microsoft.IdentityModel.Tokens.TokenValidationParameters`. The analyzer resolves
the property symbol and the containing type, so an unrelated type with the same
property name stays silent. One `SimpleAssignmentExpression` action covers an
object initializer assignment and an assignment after construction.

Five flags default to `true`: `ValidateIssuer`, `ValidateAudience`,
`ValidateLifetime`, `RequireExpirationTime`, and `RequireSignedTokens`.

`ValidateIssuerSigningKey` defaults to `false`. The rule reports that flag with
its own message, because the assignment matches the default and changes no
runtime behavior.

The rule reports only a literal `false`, including a parenthesized literal. A
value read from configuration, a local, or a `const` stays silent, because the
rule cannot judge it. The safe value depends on the deployment, so the rule
carries no code fix.

The rule needs `Microsoft.IdentityModel.Tokens.TokenValidationParameters`.
Without that type it registers no action, so a consumer with no JWT reference
gets no report.

The analyzer declares two descriptors with one ID. The test harness cannot infer
a named markup diagnostic when descriptors share an ID, so the tests supply an
explicit `DiagnosticResult` with a keyed location.
