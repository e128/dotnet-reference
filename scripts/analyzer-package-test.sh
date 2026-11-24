#!/usr/bin/env bash
# Test the packaged transitive-pinning guidance with MSBuild fixtures.
# Usage: analyzer-package-test.sh [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

if [[ "${1:-}" == "--help" ]]; then
    printf '%s\n' 'Usage: analyzer-package-test.sh' \
        'Test guidance with MSBuild fixtures and local package consumers.'
    exit 0
fi
[[ $# -eq 0 ]] || { err "Unknown argument: $1"; exit 1; }

ROOT="$(find_repo_root)"

# The base analyzer and fixer builds pack into the lowest Roslyn folder. A pin
# that drifts above that band ships an assembly into a folder its host loads,
# and the host rejects it with CS9057. The pin, the folder label, and the
# compiler-host guard must name the same band.
package_version() {
    local project="$1" package="$2"
    dotnet msbuild "$ROOT/src/$project/$project.csproj" -nologo "${DOTNET_SAFEGUARDS[@]}" \
        -getItem:PackageVersion | jq -er --arg package "$package" \
        '.Items.PackageVersion[] | select(.Identity == $package) | .Version'
}

LOWEST_BAND=$(rg -o 'analyzers/dotnet/roslyn([0-9]+\.[0-9]+)/cs' -r '$1' \
    "$ROOT/src/E128.Analyzers/E128.Analyzers.csproj" | sort -V | head -1)
GUARD_BAND=$(rg 'VersionLessThan' "$ROOT/src/E128.Analyzers/build/E128.Analyzers.targets" |
    rg -o "'[0-9]+\.[0-9]+'" | tr -d "'")
[[ -n "$LOWEST_BAND" && "$GUARD_BAND" == "$LOWEST_BAND" ]] || {
    err "The package folder band is '$LOWEST_BAND' and the host guard band is '$GUARD_BAND'. They must match."
    exit 1
}

verify_roslyn_band() {
    local project="$1" package="$2" pin
    pin=$(package_version "$project" "$package") || {
        err "No $package version resolved for $project"
        exit 1
    }
    if [[ "${pin%.*}" != "$LOWEST_BAND" ]]; then
        err "$project packs $package $pin into roslyn$LOWEST_BAND. A host on that band fails with CS9057."
        exit 1
    fi
    ok "$project pins $package $pin in roslyn$LOWEST_BAND"
}

verify_roslyn_band E128.Analyzers Microsoft.CodeAnalysis.CSharp
verify_roslyn_band E128.Analyzers.CodeFixers Microsoft.CodeAnalysis.CSharp.Workspaces

# No repository analyzer configuration may reach the package. A packed
# .globalconfig or .editorconfig would impose this repository's severities on
# every consumer, including the CA1707 test exemption.
PACKED_CONFIG=$(dotnet msbuild "$ROOT/src/E128.Analyzers/E128.Analyzers.csproj" -nologo \
    "${DOTNET_SAFEGUARDS[@]}" -getItem:None 2>/dev/null |
    jq -r '.Items.None[]? | select(.Pack == "true") | .PackagePath // ""' |
    rg -i 'globalconfig|editorconfig|Directory\.Build\.' || true)
[[ -z "$PACKED_CONFIG" ]] || {
    err "E128.Analyzers packs analyzer configuration: $PACKED_CONFIG"
    exit 1
}
ok "E128.Analyzers packs no analyzer configuration"

mkdir -p "$ROOT/.claude/tmp"
FIXTURE="$(mktemp -d "$ROOT/.claude/tmp/analyzer-package-test.XXXXXX")"
trap 'rm -rf "${FIXTURE:?}"' EXIT
mkdir -p "$FIXTURE/feed"
printf '%s\n' '<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="E128.Analyzers" Version="1.0.0" />
  </ItemGroup>
</Project>' > "$FIXTURE/Directory.Packages.props"
printf '%s\n' '<configuration><packageSources><clear /></packageSources></configuration>' > "$FIXTURE/NuGet.Config"

PROJECT="$FIXTURE/Consumer.csproj"
MESSAGE='E128.Analyzers: Transitive pinning can add dependencies to your published package.'
GUIDANCE='https://github.com/e128/dotnet-reference/blob/main/src/E128.Analyzers/README.md#transitive-pinning-guidance'
PASSED=0

printf '%s\n' '<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
    <IsPackable>true</IsPackable>
  </PropertyGroup>
  <Import Project="$(E128PackageTargets)" />
  <Target Name="Pack" />
  <Target Name="Build" />
</Project>' > "$PROJECT"

run_case() {
    local name="$1" expected="$2" target="$3" output remainder
    shift 3
    if ! output=$(dotnet msbuild "$PROJECT" -nologo -verbosity:normal -warnAsError \
        "${DOTNET_SAFEGUARDS[@]}" "-target:$target" \
        "-p:E128PackageTargets=$ROOT/src/E128.Analyzers/build/E128.Analyzers.targets" \
        -p:ImportDirectoryBuildProps=false -p:ImportDirectoryBuildTargets=false \
        -p:NuGetAudit=false \
        "-p:RestoreSources=$FIXTURE/feed" "-p:RestorePackagesPath=$FIXTURE/packages" \
        "-p:RestoreConfigFile=$FIXTURE/NuGet.Config" "$@" 2>&1); then
        err "$name: MSBuild failed"
        printf '%s\n' "$output"
        exit 1
    fi
    if [[ "$expected" == 1 ]]; then
        remainder="${output#*"$MESSAGE"}"
        if [[ "$output" != *"$MESSAGE"* || "$remainder" == *"$MESSAGE"* || "$output" != *"$GUIDANCE"* ]]; then
            err "$name: expected one guidance message and its documentation link"
            printf '%s\n' "$output"
            exit 1
        fi
    elif [[ "$output" == *"$MESSAGE"* ]]; then
        err "$name: unexpected guidance message"
        printf '%s\n' "$output"
        exit 1
    fi
    PASSED=$((PASSED + 1))
    ok "$name"
}

run_case 'Enabled pinning' 1 Pack
run_case 'Case-insensitive properties' 1 Pack -p:CentralPackageTransitivePinningEnabled=TRUE -p:IsPackable=TRUE
run_case 'Pinning disabled' 0 Pack -p:CentralPackageTransitivePinningEnabled=false
run_case 'Pinning unset' 0 Pack -p:CentralPackageTransitivePinningEnabled=
run_case 'Central management disabled' 0 Pack -p:ManagePackageVersionsCentrally=false
run_case 'Central management unset' 0 Pack -p:ManagePackageVersionsCentrally=
run_case 'Non-packable project' 0 Pack -p:IsPackable=false
run_case 'Dependencies suppressed' 0 Pack -p:SuppressDependenciesWhenPacking=true
run_case 'Design-time build' 0 Pack -p:DesignTimeBuild=true
run_case 'Ordinary build' 0 Build
run_case 'Pack without build' 1 Pack -p:NoBuild=true
cp "$PROJECT" "$FIXTURE/Consumer.vbproj"
PROJECT="$FIXTURE/Consumer.vbproj"
run_case 'Non-C# project' 0 Pack

# Use the production packaging metadata so a missing outer-build asset fails the test.
PACKAGE_PATH=$(dotnet msbuild "$ROOT/src/E128.Analyzers/E128.Analyzers.csproj" \
    -nologo "${DOTNET_SAFEGUARDS[@]}" -getItem:None | \
    jq -er '.Items.None[] | select(.Identity == "build/E128.Analyzers.targets" and .Pack == "true") | .PackagePath')
[[ -n "$PACKAGE_PATH" ]] || { err 'The package target has no packaging path'; exit 1; }
PROJECT="$FIXTURE/Package.csproj"
printf '%s\n' '<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <PackageId>E128.Analyzers</PackageId>
    <Configuration>Release</Configuration>
    <NoBuild>true</NoBuild>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
    <NoPackageAnalysis>true</NoPackageAnalysis>
  </PropertyGroup>
  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="/" />' \
    "    <None Include=\"\$(E128PackageTargets)\" Pack=\"true\" PackagePath=\"$PACKAGE_PATH\" />" \
    '  </ItemGroup>
</Project>' > "$PROJECT"
printf '%s\n' 'Package fixture.' > "$FIXTURE/README.md"
run_case 'Package build assets' 0 Pack -restore "-p:PackageOutputPath=$FIXTURE/feed"
[[ -f "$FIXTURE/feed/E128.Analyzers.1.0.0.nupkg" ]] || \
    { err 'SDK pack did not produce the expected package'; exit 1; }

mkdir -p "$FIXTURE/consumer"
printf '%s\n' '<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
    <IsPackable>true</IsPackable>
    <Configuration>Release</Configuration>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <NoPackageAnalysis>true</NoPackageAnalysis>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="E128.Analyzers" PrivateAssets="all" />
    <None Include="README.md" Pack="true" PackagePath="/" />
  </ItemGroup>
</Project>' > "$FIXTURE/consumer/Shared.props"
printf '%s\n' 'Consumer fixture.' > "$FIXTURE/consumer/README.md"

# Write a consumer project that imports the shared settings after the SDK props.
write_consumer() {
    PROJECT="$FIXTURE/consumer/$1.csproj"
    printf '%s\n' '<Project Sdk="Microsoft.NET.Sdk">' '  <PropertyGroup>' \
        "    <$2>$3</$2>" '  </PropertyGroup>' \
        '  <Import Project="Shared.props" />' '</Project>' > "$PROJECT"
}

write_consumer Single TargetFramework net11.0
run_case 'Single-target package consumer without build' 1 Pack -restore -p:NoBuild=true
run_case 'Single-target package consumer with build' 1 Pack -p:NoBuild=false
write_consumer Multi TargetFrameworks 'net11.0;net11.0-browser'
run_case 'Multi-target package consumer without build' 1 Pack -restore -p:NoBuild=true
run_case 'Multi-target package consumer with build' 1 Pack -p:NoBuild=false
run_case 'Multi-target dependencies suppressed' 0 Pack -p:SuppressDependenciesWhenPacking=true
run_case 'Multi-target ordinary build' 0 Build

ok "$PASSED package guidance tests passed"
