# NuGet Trusted Publishing
*Updated: 2026-11-24T12:57:23Z*

OIDC-based package publishing from GitHub Actions to nuget.org. Eliminates long-lived API keys.

## How It Works

1. GitHub Actions job requests OIDC token (signed, includes repo/workflow metadata)
2. Workflow calls nuget.org token exchange endpoint via `curl` (manual, no third-party action)
3. nuget.org validates token against your trusted publishing policy
4. Returns a **1-hour, single-use** API key
5. Workflow uses that key with `dotnet nuget push`

## nuget.org Policy Setup

Profile menu -> Trusted Publishing -> Create.

| Field              | Value                          | Notes                                    |
| ------------------ | ------------------------------ | ---------------------------------------- |
| Policy Name        | Example: `E128.Analyzers`     | Free-text, for your reference            |
| Package Owner      | username or org                | Controls scope: ALL packages for owner |
| Repository Owner   | `e128`                         | GitHub org/user, case-sensitive          |
| Repository         | `dotnet-reference`             | Repo name, case-sensitive                |
| Workflow File      | `publish.yml`                  | Filename only, NOT `.github/workflows/`  |
| Environment        | `release` (optional)           | Must match `environment:` in workflow    |

### Private Repo Bootstrap

New policies for private repos are **active 7 days**. First successful OIDC token exchange permanently activates via immutable GitHub IDs. Miss the window? Re-activate manually from the Trusted Publishing page.

## GitHub Actions Workflow

The sample simplifies `.github/workflows/publish.yml`. The real workflow also sets `env` values, restores a NuGet cache, and writes a job summary.

```yaml
name: Publish

on:
  push:
    branches: [main]
    paths:
      - 'src/E128.Analyzers/**'
      - 'src/E128.Analyzers.CodeFixers/**'
      - 'src/E128.Analyzers.Union/**'

permissions:
  contents: read

jobs:
  publish:
    name: Pack + Push E128.Analyzers
    runs-on: ubuntu-26.04
    environment: release          # must match nuget.org policy
    permissions:
      contents: read
      id-token: write             # REQUIRED for OIDC

    steps:
      - uses: actions/checkout@<sha>          # pin to a full-length commit SHA

      - name: Check if version already published
        id: check
        run: |
          VERSION=$(sed -n 's/.*<Version>\([^<]*\)<.*/\1/p' src/E128.Analyzers/E128.Analyzers.csproj)
          STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
            "https://api.nuget.org/v3-flatcontainer/e128.analyzers/${VERSION}/...")
          # Sets outputs: version, exists (true/false)

      - uses: actions/setup-dotnet@<sha>      # pin to a full-length commit SHA
        if: steps.check.outputs.exists == 'false'
        with:
          dotnet-version: '11.0.x'

      - name: Pack
        if: steps.check.outputs.exists == 'false'
        run: dotnet pack src/E128.Analyzers/E128.Analyzers.csproj -c Release -o ./nupkgs

      - name: NuGet login (OIDC)
        if: steps.check.outputs.exists == 'false'
        id: login
        env:
          NUGET_USER: ${{ secrets.NUGET_USER }}
        run: |
          OIDC_TOKEN=$(curl -sL \
            -H "Authorization: Bearer $ACTIONS_ID_TOKEN_REQUEST_TOKEN" \
            "${ACTIONS_ID_TOKEN_REQUEST_URL}&audience=https%3A%2F%2Fwww.nuget.org" \
            | jq -r '.value')
          echo "::add-mask::$OIDC_TOKEN"
          NUGET_API_KEY=$(curl -sL -X POST "https://www.nuget.org/api/v2/token" \
            -H "Content-Type: application/json" \
            -H "Authorization: Bearer $OIDC_TOKEN" \
            -d "{\"username\":\"${NUGET_USER}\",\"tokenType\":\"ApiKey\"}" \
            | jq -r '.apiKey')
          echo "::add-mask::$NUGET_API_KEY"
          echo "NUGET_API_KEY=$NUGET_API_KEY" >> "$GITHUB_OUTPUT"

      - name: Push
        if: steps.check.outputs.exists == 'false'
        run: >
          dotnet nuget push ./nupkgs/*.nupkg
          --api-key ${{ steps.login.outputs.NUGET_API_KEY }}
          --source https://api.nuget.org/v3/index.json
```

The workflow does NOT use the `NuGet/login` GitHub Action. Instead it performs the OIDC exchange manually via `curl` + `jq`, calling the GitHub OIDC endpoint and then the nuget.org token endpoint directly. This avoids the third-party action dependency.

Output: `NUGET_API_KEY` (short-lived, single-use) written to `$GITHUB_OUTPUT`.

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
  <Authors>Maintainer name</Authors>
  <Description>Roslyn analyzers enforcing opinionated .NET conventions at compile time.
    <!-- full description maintained in E128.Analyzers.csproj --></Description>
  <PackageTags>roslyn;analyzer;csharp;code-analysis</PackageTags>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <RepositoryUrl>https://github.com/e128/dotnet-reference</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
</PropertyGroup>

<ItemGroup>
  <!-- Place each DLL in its Roslyn-band folder under analyzers/dotnet -->
  <None Include="$(OutputPath)\$(AssemblyName).dll"
        Pack="true"
        PackagePath="analyzers/dotnet/roslyn5.9/cs"
        Visible="false" />
  <None Include="$(OutputPath)\$(AssemblyName).dll"
        Pack="true"
        PackagePath="analyzers/dotnet/roslyn5.12/cs"
        Visible="false" />
</ItemGroup>
```

The sample shows the base assembly only. The real csproj also packs the `E128.Analyzers.CodeFixers` assembly in both band folders and the `E128.Analyzers.Union` assembly in the 5.12 folder. The `BuildComponentsBeforePack` target builds both components before `dotnet pack` runs.

| Property                          | Why                                                        |
| --------------------------------- | ---------------------------------------------------------- |
| `Version`                           | Pinned in csproj. The workflow reads it with `sed`.     |
| `IncludeBuildOutput=false`          | Prevents DLL in `lib/` (would be treated as library ref)   |
| `DevelopmentDependency=true`        | Marks as dev-only. Consumers get the analyzer, not a dependency. |
| `SuppressDependenciesWhenPacking`   | Keeps analyzer deps out of the nupkg dependency list       |
| `PackagePath="analyzers/dotnet/roslyn{band}/cs"` | The SDK picks one Roslyn-band folder per package, so the base build sits in two bands |
| `PrivateAssets="all"` on refs       | PackageReferences of the analyzer stay private              |

## Gotchas

### Critical

- **Token scope is ALL packages for the owner.** A single compromised repo can publish any package under that owner. No per-package scoping exists yet.
- **Reusable workflows break it.** OIDC token validation uses the repo containing the token exchange step, not the calling repo. Shared workflow repos get 401s. Keep the OIDC login step in the originating repo.
- **`user` is your nuget.org profile name, NOT email.** This mistake causes silent auth failures.

### Operational

- **Workflow is idempotent.** The version check queries nuget.org before packing. The workflow skips every later step if the version already exists. Bump `<Version>` in csproj to publish a new release.
- **API key expires in 1 hour.** Request immediately before `dotnet nuget push`, not at workflow start.
- **One token = one API key.** Each OIDC token exchange yields exactly one key.
- **Remove old API keys after migration.** Stored secrets can interfere with trusted publishing.
- **`id-token: write` is mandatory.** Without it, GitHub does not issue the OIDC token. Set at job level.
- **Environment name is case-sensitive.** Must match exactly between GitHub Settings and nuget.org policy.
- **GitHub Actions only.** GitLab, Azure DevOps support is on the roadmap and not available yet.
- **Gradual rollout.** Some accounts may not see the Trusted Publishing option yet.

### Analyzer Packaging

- **`NU5128` is suppressed in the csproj (`NoWarn`).** The package declares no dependencies, and `lib/netstandard2.0` holds only a `_._` placeholder file.
- **Dependencies with analyzers** require bundling dependency DLLs into the analyzer folder via `GeneratePathProperty=true` + `<None Include="$(PkgDep_Name)\lib\netstandard2.0\*.dll" .../>`. This package declares no dependencies, so it bundles none.
- **Must target `netstandard2.0`.** Roslyn loads analyzers in a netstandard2.0 context for any consuming target framework. All three analyzer projects target it.

## Related Files

- [secret-scanning.md](secret-scanning.md): the CI workflow that scans the full history
- [shipping.md](shipping.md): the version bump that triggers a publish

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
