# Security

Defect-prevention rules for security-relevant defaults. Each rule is
negative-form because it blocks a real bug class.

## Never Hardcode a Security-Relevant Default

Inject the value through an options class with a safe default. Never hardcode
any of these:

- Cryptographic keys, secrets, or connection strings
- Allowed origins and CORS allowlists
- Authentication or authorization policy values
- File paths outside the application root

Expose every configurable value through a strongly-typed options class bound
to configuration.

## See Also

- [dotnet-anti-patterns.md](dotnet-anti-patterns.md) — .NET defects with
  security impact
- [quality-gates.md](quality-gates.md) — analyzer suppressions, null-forgiving
