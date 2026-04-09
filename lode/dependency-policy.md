# Dependency Policy
*Updated: 2026-04-09T00:52:00Z*

## Selection Criteria

Before adding a new NuGet dependency, evaluate:

1. **License** — MIT, Apache 2.0, or BSD only. No GPL, no AGPL, no commercial-only.
2. **Maintenance** — actively maintained (commits in last 6 months). Check GitHub pulse.
3. **Size** — prefer small, focused packages over large frameworks.
4. **Transitive impact** — check what the package pulls in. Avoid packages with heavy transitive chains.
5. **Alternatives** — is this in the BCL already? Can we write 20 lines instead of adding a dependency?

## Package Categories

| Category   | Policy                                                     |
| ---------- | ---------------------------------------------------------- |
| Analyzers  | Always allowed — zero runtime impact                       |
| Testing    | Allowed in test projects only                              |
| Microsoft  | Preferred for BCL extensions (Microsoft.Extensions.*)      |
| Third-party | Requires justification — evaluate against the 5 criteria  |

## Version Management

All versions are managed centrally in `Directory.Packages.props`. Individual projects use `<PackageReference Include="..." />` without version attributes. Transitive pinning is enabled to prevent version drift.
