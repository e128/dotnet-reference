# E128.Reference — Summary
*Updated: 2026-04-11T14:10:12Z*

E128.Reference is a .NET 10 reference repository demonstrating modern conventions: minimal API web app (Kestrel), System.CommandLine CLI, custom Roslyn analyzers with code fixes (E128.Analyzers, NuGet-packable), hardened Alpine Docker images, strict deny-by-default code analysis with third-party Roslyn analyzers (see `Directory.Packages.props`), xUnit v3 + Microsoft Testing Platform, ArchUnitNET architecture tests enforcing structural invariants via IL analysis, Central Package Management with transitive pinning, Renovate for automated dependency updates, OIDC trusted publishing to nuget.org via GitHub Actions, and a Claude Code harness (CLAUDE.md, rules, hooks, skills, agents) powered by bash scripts.
