# Security

Defect-prevention rules for security-relevant defaults. Kept
negative-form because they block real bug classes.

## Never Hardcode Security-Relevant Defaults

Use options classes with safe defaults injected via DI. Examples that
must not hardcode:

- Cryptographic keys, secrets, connection strings
- Allowed origins, CORS allowlists
- Authentication/authorization policy values
- File paths outside the application root

If a value must be configurable, expose it through a strongly-typed
options class bound to configuration.

## Cross-Reference: .NET Anti-Patterns with Security Impact

See [dotnet-anti-patterns.md](dotnet-anti-patterns.md):

- `new HttpClient()` — socket exhaustion, DNS staleness, no handler pipeline
- Hardcoded URLs or secrets in source
- `async void` outside event handlers — unobservable exceptions

## See Also

- [quality-gates.md](quality-gates.md) — analyzer suppressions, null-forgiving
- [dotnet-anti-patterns.md](dotnet-anti-patterns.md) — broader .NET defect list
