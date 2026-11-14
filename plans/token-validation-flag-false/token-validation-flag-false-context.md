# Token Validation Flag Pinned False: Context
*Updated: 2026-11-14T00:08:09Z*

## Problem

`TokenValidationParameters` carries six boolean flags that default to `true`. A
literal `false` in an object initializer or an assignment turns the check off
with no build warning and no runtime warning. The token handler then accepts a
token that the caller believes it rejects.

No installed rule covers the shape. E128202 reports unvalidated JWT claims used
as an identity, which is a later step in the same failure.

## Evidence

| Check | Result |
| ----- | ------ |
| `TokenValidationParameters` sites in `~/repos/e128` | Zero (as of 2026-11-14) |
| Covered by an installed rule | No |
| Runtime guard | None |

Zero sites means the frequency score is low. The register bar is a real defect,
so Phase 0 records the decision to ship for prevention or to close.

## Register Rows Considered And Declined

| Row | Reason |
| --- | ------ |
| `AsReadOnly()` in a property getter | E128058 and E128060 require `.AsReadOnly()` in that exact getter shape. A second rule would report the code the shipped rules demand. Recorded in `skipped-decisions.md`. |
| CORS wildcard with credentials | ASP.NET Core throws at startup when a policy holds `AllowAnyOrigin` and `AllowCredentials`. The runtime already reports the defect. Zero sites in the corpus. |

## Shape

```csharp
var parameters = new TokenValidationParameters
{
    ValidateIssuer = false,
    ValidateAudience = false,
};
```

The rewrite is a caller decision, because the safe value depends on the issuer
and the audience. The rule reports and carries no code fix.

## Related

- [index.md](../../lode/dotnet/analyzer-candidates/index.md): open register
- [catalog.md](../../lode/dotnet/analyzer-candidates/catalog.md): settled rules
- [rule-details-security.md](../../lode/dotnet/analyzers/rule-details-security.md): security detection shapes
