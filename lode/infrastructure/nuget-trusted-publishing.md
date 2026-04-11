# NuGet Trusted Publishing

*Updated: 2026-04-11T14:12:11Z*

OIDC-based package publishing from GitHub Actions to nuget.org. Eliminates long-lived API keys.

## How It Works

1. GitHub Actions job requests OIDC token (signed, includes repo/workflow metadata)
2. `NuGet/login@v1` sends token to nuget.org token exchange endpoint
3. nuget.org validates token against your trusted publishing policy
4. Returns a **1-hour, single-use** API key
5. Workflow uses that key with `dotnet nuget push`

## nuget.org Policy Setup

Profile menu -> Trusted Publishing -> Create.

| Field              | Value                          | Notes                                    |
| ------------------ | ------------------------------ | ---------------------------------------- |
| Policy Name        | e.g. `E128.Analyzers`         | Free-text, for your reference            |
| Package Owner      | username or org                | Controls scope -- ALL packages for owner |
| Repository Owner   | `e128`                         | GitHub org/user, case-sensitive          |
| Repository         | `dotnet-reference`             | Repo name, case-sensitive                |
| Workflow File      | `publish.yml`                  | Filename only, NOT `.github/workflows/`  |
| Environment        | `release` (optional)           | Must match `environment:` in workflow    |

### Private Repo Bootstrap

New policies for private repos are **active 7 days**. First successful `NuGet/login` call permanently activates via immutable GitHub IDs. Miss the window? Re-activate manually from the Trusted Publishing page.

## GitHub Actions Workflow

```yaml
name: Publish

on:
  push:
    branches: [main]
    paths: ['src/E128.Analyzers/**']

permissions:
  contents: read

jobs:
  publish:
    name: Pack + Push E128.Analyzers
    runs-on: ubuntu-24.04
    environment: release          # must match nuget.org policy
    permissions:
      contents: read
      id-token: write             # REQUIRED for OIDC

    steps:
      - uses: actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd # v6.0.2

      - name: Check if version already published
        id: check
        run: |
          VERSION=$(sed -n 's/.*<Version>\([^<]*\)<.*/\1/p' src/E128.Analyzers/E128.Analyzers.csproj)
          STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
            "https://api.nuget.org/v3-flatcontainer/e128.analyzers/${VERSION}/...")
          # Sets outputs: version, exists (true/false)

      - uses: actions/setup-dotnet@c2fa09f4bde5ebb9d1777cf28262a3eb3db3ced7 # v5.2.0
        if: steps.check.outputs.exists == 'false'
        with:
          dotnet-version: '10.0.x'

      - name: Pack
        if: steps.check.outputs.exists == 'false'
        run: dotnet pack src/E128.Analyzers/E128.Analyzers.csproj -c Release -o ./nupkgs

      - name: NuGet login (OIDC)
        if: steps.check.outputs.exists == 'false'
        uses: NuGet/login@d22cc5f58ff5b88bf9bd452535b4335137e24544 # v1.1.0
        id: login
        with:
          user: ${{ secrets.NUGET_USER }}

      - name: Push
        if: steps.check.outputs.exists == 'false'
        run: >
          dotnet nuget push ./nupkgs/*.nupkg
          --api-key ${{ steps.login.outputs.NUGET_API_KEY }}
          --source https://api.nuget.org/v3/index.json
```

### `NuGet/login@v1` Action Inputs

| Input               | Required | Default                              |
| ------------------- | -------- | ------------------------------------ |
| `user`              | yes      | --                                   |
| `token-service-url` | no       | `https://www.nuget.org/api/v2/token` |
| `audience`          | no       | `https://www.nuget.org`              |

Output: `NUGET_API_KEY` (short-lived, single-use).

## Roslyn Analyzer csproj for Packaging

Key properties that differ from a normal library package:

```xml
<PropertyGroup>
  <Version>1.0.0</Version>
  <IsPackable>true</IsPackable>
  <IncludeBuildOutput>false</IncludeBuildOutput>
  <DevelopmentDependency>true</DevelopmentDependency>
  <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
  <NoPackageAnalysis>true</NoPackageAnalysis>

  <!-- Package metadata -->
  <PackageId>E128.Analyzers</PackageId>
  <Authors>Brent Miller</Authors>
  <Description>Roslyn analyzers enforcing E128 conventions: file system path types,
    string.Empty, TimeProvider, IHttpClientFactory, and sealed-by-default.</Description>
  <PackageTags>roslyn;analyzer;csharp;code-analysis</PackageTags>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <RepositoryUrl>https://github.com/e128/dotnet-reference</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
</PropertyGroup>

<ItemGroup>
  <!-- Place DLL in NuGet analyzer convention path -->
  <None Include="$(OutputPath)\$(AssemblyName).dll"
        Pack="true"
        PackagePath="analyzers/dotnet/cs"
        Visible="false" />
</ItemGroup>
```

| Property                          | Why                                                        |
| --------------------------------- | ---------------------------------------------------------- |
| `Version`                           | Pinned in csproj (`1.0.0`); workflow reads via `sed`       |
| `IncludeBuildOutput=false`          | Prevents DLL in `lib/` (would be treated as library ref)   |
| `DevelopmentDependency=true`        | Marks as dev-only; consumers get analyzer, not dependency   |
| `SuppressDependenciesWhenPacking`   | Keeps analyzer deps out of the nupkg dependency list       |
| `PackagePath="analyzers/dotnet/cs"` | NuGet convention path the compiler scans for analyzers     |
| `PrivateAssets="all"` on refs       | Analyzer's own PackageReferences stay private              |

## Gotchas

### Critical

- **Token scope is ALL packages for the owner.** A single compromised repo can publish any package under that owner. No per-package scoping exists yet.
- **Reusable workflows break it.** OIDC token validation uses the repo containing the `NuGet/login` step, not the calling repo. Shared workflow repos get 401s. Keep `NuGet/login` in the originating repo.
- **`user` is your nuget.org profile name, NOT email.** Common mistake; causes silent auth failures.

### Operational

- **Workflow is idempotent.** Version check queries nuget.org before packing; skips everything if the version already exists. Bump `<Version>` in csproj to publish a new release.
- **API key expires in 1 hour.** Request immediately before `dotnet nuget push`, not at workflow start.
- **One token = one API key.** Each OIDC token exchange yields exactly one key.
- **Remove old API keys after migration.** Stored secrets can interfere with trusted publishing.
- **`id-token: write` is mandatory.** Without it, GitHub won't issue the OIDC token. Set at job level.
- **Environment name is case-sensitive.** Must match exactly between GitHub Settings and nuget.org policy.
- **GitHub Actions only (for now).** GitLab, Azure DevOps support is on the roadmap but not shipped.
- **Gradual rollout.** Some accounts may not see the Trusted Publishing option yet.

### Analyzer Packaging

- **`NU5128` warning is expected.** Suppressing it is standard for analyzer packages (no `lib/` content).
- **Dependencies with analyzers** require bundling dependency DLLs into `analyzers/dotnet/cs` via `GeneratePathProperty=true` + `<None Include="$(PkgDep_Name)\lib\netstandard2.0\*.dll" .../>`.
- **Must target `netstandard2.0`.** Roslyn loads analyzers in a netstandard2.0 context regardless of the consuming project's TFM.

## Sources

- [Microsoft Learn: Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) (2026-02-02)
- [.NET Blog announcement](https://devblogs.microsoft.com/dotnet/enhanced-security-is-here-with-the-new-trust-publishing-on-nuget-org/) (2025)
- [Andrew Lock walkthrough](https://andrewlock.net/easily-publishing-nuget-packages-from-github-actions-with-trusted-publishing/) (2025)
- [Barry Dorrans (idunno.org) guide](https://idunno.org/publishing-nuget-packages-from-a-github-action-without-secrets/) (2025)
- [Damir Arh migration](https://www.damirscorner.com/blog/posts/20251003-SwitchingToNuGetTrustedPublishing.html) (2025-10-03)
- [Reusable workflow discussion](https://github.com/orgs/community/discussions/179952) (2025)
- [Aaron Stannard: Roslyn analyzer NuGet packaging](https://aaronstannard.com/roslyn-nuget/) (2024)
- [Meziantou: analyzer with NuGet deps](https://www.meziantou.net/packaging-a-roslyn-analyzer-with-nuget-dependencies.htm)
- [NuGet/login action.yml](https://github.com/NuGet/login) (v1.1.0, SHA `d22cc5f5`)
