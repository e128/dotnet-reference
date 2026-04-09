# Step 2: Solution Infrastructure

**Mandatory — executes immediately. No findings table, no approval gate.**

After all sub-steps: report what was done (or that everything was already in place), confirm build
and tests pass, and proceed to Step 3.

---

## 2a. Convert to .slnx

Glob for `.sln` files. If found:
1. Convert: `dotnet sln migrate`
2. Verify `.slnx` was created and contains all projects
3. Delete the old `.sln` file
4. `${CLAUDE_SKILL_DIR}/scripts/build.sh <solution> --json` — confirm solution loads and compiles
5. `${CLAUDE_SKILL_DIR}/scripts/test.sh <solution> --json` — confirm no regressions

If `.slnx` already exists: report and skip to 2b.

---

## 2b. Adopt Central Package Management

Glob for `Directory.Packages.props` at the solution root.

**If NOT found:**
1. Scan all `.csproj` files for `<PackageReference>` with `Version` attributes
2. Create `Directory.Packages.props` with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`
3. Move all versions from `.csproj` files into `Directory.Packages.props` as `<PackageVersion>` entries
4. Remove `Version` attributes from all `<PackageReference>` elements in `.csproj` files
5. Convert any `<PackageReference Update="...">` to central management pattern
6. If the project uses lock files (`packages.lock.json`): `dotnet restore --force-evaluate`

**If already exists:** Verify these hardening properties are set (add if missing):
- `CentralPackageTransitivePinningEnabled=true` — prevents transitive version drift
- `CentralPackageVersionOverrideEnabled=false` — prevents per-project version overrides

Then skip to 2c.

---

## 2c. Verify MTP Test Infrastructure

Check that the Microsoft Testing Platform is correctly configured for .NET 10:

**global.json** — must contain:
```json
{ "test": { "runner": "Microsoft.Testing.Platform" } }
```
Without this, `dotnet test` fails on .NET 10 SDK with a VSTest compatibility error.

**Directory.Build.targets** — must contain (conditioned on `IsTestProject`):
```xml
<PropertyGroup Condition="'$(IsTestProject)' == 'true'">
  <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
  <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
  <OutputType>Exe</OutputType>
</PropertyGroup>
```
This must be in `.targets` (not `.props`) because `IsTestProject` is set in the `.csproj` and isn't available during `.props` evaluation.

**Test .csproj files** — should only need `<IsTestProject>true</IsTestProject>`. If they also have `<OutputType>Exe</OutputType>` or MTP properties, remove the redundancy (inherited from targets).

If `Directory.Build.targets` does not exist, create it with the above content.

---

## 2d. Audit NuGet Security

Check `nuget.config` at the solution root:

**Required** (add if missing):
- `<clear />` in `<packageSources>` — removes implicit default sources
- Explicit `nuget.org` source with `protocolVersion="3"`
- `<packageSourceMapping>` — restricts which packages come from which source

**Recommended** (flag if missing):
- `<trustedSigners>` with certificate fingerprints for nuget.org
- If private feeds exist, they should also have source mapping entries

If `nuget.config` does not exist, create one with `<clear />` + explicit nuget.org + source mapping.

---

## 2e. Enforce Strict Code Analysis

Check `Directory.Build.props` for these properties (add any missing):

```xml
<PropertyGroup>
  <!-- All warnings are errors — prevents regressions -->
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>

  <!-- Enable ALL built-in analyzer rules -->
  <AnalysisLevel>latest-all</AnalysisLevel>

  <!-- Enforce style rules during build, not just in IDE -->
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>

  <!-- Per-category: guarantee full coverage for critical categories.
       Explicit AnalysisMode overrides the compound AnalysisLevel format. -->
  <AnalysisModeSecurity>All</AnalysisModeSecurity>
  <AnalysisModeReliability>All</AnalysisModeReliability>
  <AnalysisModePerformance>All</AnalysisModePerformance>
</PropertyGroup>
```

**Key interactions:**
- `AnalysisLevel=latest-all` sets *which* rules are enabled; explicit `AnalysisMode<Category>` overrides its category without affecting others
- `.editorconfig` `dotnet_analyzer_diagnostic.category-XXX.severity` only changes severity of *already-enabled* rules — it does NOT enable disabled rules; the MSBuild properties above are required for that
- `TreatWarningsAsErrors=true` promotes all enabled-and-warned rules to errors

**Root `.editorconfig`:**
Glob for `.editorconfig`.

- **If NOT found:** Create with `root = true` and `dotnet_analyzer_diagnostic.severity = warning`
- **If found:**
  1. Verify `root = true` is present
  2. Check it does NOT contain `dotnet_analyzer_diagnostic.severity = none` or `suggestion` as a blanket override
  3. Flag any `dotnet_diagnostic.CA*.severity = none` that suppress security-critical rules (CA2300-CA2362, CA3001-CA3012, CA5350-CA5405) for review

**After changes:**
1. `dotnet build` — expect new errors from previously unenforced rules; these are pre-existing issues, not regressions
2. If new errors: **fix inline first** — these are real pre-existing issues now surfaced; if <= 10 errors and each is fixable in under 2 minutes, fix them now; only fall back to temporary suppressions if >10 errors or if a fix requires architectural decisions; track each suppression as a finding to resolve in Steps 3-8

**Child `.editorconfig` files** (e.g., in test projects) that suppress CA1707, CA1515, etc. for test
conventions are expected — do not remove them.

---

## 2f. Ensure .gitignore Coverage

Check that a `.gitignore` exists at the repository root with entries for:

**Required** (add if missing):
- `.DS_Store` — macOS Finder metadata
- `.env` — environment files often contain secrets

**Also verify these .NET-standard entries** (add if missing):
- `bin/`, `obj/` — build output directories
- `*.user`, `*.suo` — Visual Studio user settings

If `.gitignore` does not exist: `dotnet new gitignore`, then verify the required entries above.

If `.DS_Store` or `.env` files are already tracked in git: report and recommend `git rm --cached`.
Do not execute without user approval.

---

## 2g. Verify and Continue

Run the single consolidated verification pass for all changes made in 2b-2f:
```bash
${CLAUDE_SKILL_DIR}/scripts/check.sh <solution> --json
```

- Report what was converted/enforced (or that everything was already in place)
- Proceed to Step 3
