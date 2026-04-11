# Dependency Policy
*Updated: 2026-04-11T14:11:53Z*

## Selection Criteria

Before adding a new NuGet dependency, evaluate:

1. **License** — MIT, Apache 2.0, or BSD only. No GPL, no AGPL, no commercial-only.
2. **Maintenance** — actively maintained (commits in last 6 months). Check GitHub pulse.
3. **Size** — prefer small, focused packages over large frameworks.
4. **Transitive impact** — check what the package pulls in. Avoid packages with heavy transitive chains.
5. **Alternatives** — is this in the BCL already? Can we write 20 lines instead of adding a dependency?

## Package Categories

| Category             | Policy                                                          |
| -------------------- | --------------------------------------------------------------- |
| Analyzers            | Always allowed — zero runtime impact                            |
| Roslyn authoring     | Allowed for custom analyzer/code-fix projects (Microsoft.CodeAnalysis.*) |
| Architecture testing | Allowed in test projects only (ArchUnitNET)                     |
| Testing              | Allowed in test projects only                                   |
| CLI                  | System.CommandLine for all console entry points                 |
| Microsoft            | Preferred for BCL extensions (Microsoft.Extensions.*)           |
| Third-party          | Requires justification — evaluate against the 5 criteria        |

## Version Management

All versions are managed centrally in `Directory.Packages.props` (CPM). Individual projects use `<PackageReference Include="..." />` without version attributes. Transitive pinning is enabled via `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`. Explicit transitive pins exist for `System.Collections.Immutable` and `System.Diagnostics.DiagnosticSource` to prevent diamond-dependency drift.

## Automated Updates

Renovate manages dependency PRs via `renovate.json`. Key rules:

- Patch and minor updates auto-merge.
- Major version bumps require manual review.
- Security updates bypass schedule/grouping and auto-merge.
- PRs limited to 1 concurrent to keep the pipeline green.

## Supply Chain Security

`nuget.config` enforces a locked-down supply chain:

- **Cleared sources** — `<clear />` removes inherited feeds; only `nuget.org` is configured.
- **Trusted signers** — three NuGet.org certificate fingerprints (SHA-256) are pinned.
- **Package source mapping** — all packages (`*`) are mapped to the `nuget.org` source, preventing feed substitution.
