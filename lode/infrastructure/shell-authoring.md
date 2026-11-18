# Shell Authoring
*Updated: 2026-11-18T09:21:59Z*

Tool prerequisites and the shell lint gate for this repository. Performance
rules and the Bash version floor live in
[script-performance.md](script-performance.md).

## Required Tools

Install these CLI tools:

- `rg` (ripgrep): fast regex search
- `fd`: fast file finder that respects `.gitignore`
- `jq`: JSON processor for script output
- `bash` 4.0 or later: required for `mapfile` and associative arrays. macOS
  ships 3.2 at `/bin/bash`, so `scripts/lib.sh` re-execs under Homebrew Bash.
  Minimum versions: [scripts/README.md](../../scripts/README.md).
- `shellcheck`: bash script linter. `scripts/lint-shell.sh` runs it at warning
  severity. CI pins the version. Install it with `brew install shellcheck`.

## Optional Tools

- `gitleaks`: secret scan in `scripts/internal/precommit.sh`
  (`brew install gitleaks`). See [secret-scanning.md](secret-scanning.md).
- `dotnet-outdated-tool`: NuGet update checker
  (`dotnet tool install -g dotnet-outdated-tool`)

## Repository-Local Tools

`dotnet-tools.json` pins the tools this repository owns. Run
`dotnet tool restore` after a clone or a manifest change. The Roslyn Navigator
MCP server depends on it. See
[roslyn-navigator-mcp.md](roslyn-navigator-mcp.md).

## Shell Lint Gate

Run `scripts/lint-shell.sh` after you write or edit any `.sh` file. Do this
before you call the task complete.

```bash
scripts/lint-shell.sh              # every tracked .sh file
scripts/lint-shell.sh path/to.sh   # one file
```

The script runs `shellcheck -S warning`. A warning fails the run. The gate
ignores the note-level findings, so `SC1091` needs no suppression. Pass
`--json` for `{"status","files","findings","details"}`.

shellcheck parses every file it reads, so a syntax error fails the same run. A
separate `bash -n` call is not needed.

### Where the Gate Runs

The same script is a step of these entries. Never add a second shellcheck
invocation to them. Never call raw `shellcheck` in a gate.

- `scripts/check.sh`
- `scripts/ci.sh`
- `scripts/internal/precommit.sh`, over the staged shell files only
- the CI job named `lint`

### New Scripts

After you create a new `scripts/*.sh` file, confirm it appears in the help
output:

```bash
scripts/help.sh | rg <script-name>
```

## MSBuild Safeguards

Every script that runs a build, test, or restore command must stop MSBuild worker nodes and the Roslyn compiler server from outliving the command. `scripts/lib.sh` owns the settings.

- `MSBUILDDISABLENODEREUSE=1` is exported on source. It covers every MSBuild process, including nested ones.
- `DOTNET_SAFEGUARDS` holds `-nodeReuse:false -p:UseSharedCompilation=false`. Pass it to `dotnet build` and `dotnet restore`.
- `DOTNET_TEST_SAFEGUARDS` holds `-p:UseSharedCompilation=false`. Pass it to `dotnet test`. Under Microsoft.Testing.Platform, `dotnet test` forwards `-nodeReuse:false` to the test app, which rejects it and runs zero tests.
- `Directory.Build.props` sets `UseSharedCompilation=true`. A command-line property overrides it, and an environment variable does not.

A forced rebuild takes longer without the shared compiler server. A new script that calls `dotnet build`, `dotnet test`, or `dotnet restore` passes the matching array.

## Fixing a Finding

Resolve the finding in the code. Use `# shellcheck disable=SC<N>` only for a
false positive, on one line, with a comment that states why.

```bash
# BOLD is used only by the sourcing scripts, never inside this file.
# shellcheck disable=SC2034
RED='' GREEN='' YELLOW='' BLUE='' DIM='' BOLD='' RESET=''
```

To compare findings before and after a refactor, match on file and rule code.
Never match on raw text. The line number moves with the edit, so a text compare
reports an unchanged finding as new. See the verification steps in
[script-performance.md](script-performance.md).

## Related

- [script-performance.md](script-performance.md): fork avoidance, the Bash
  version floor, and byte-diff verification
- [claude-code-maintenance.md](claude-code-maintenance.md): script conventions
  and the harness layout
- [scripts/README.md](../../scripts/README.md): version floors and the script
  catalog
- [deterministic-scripts.md](../../.claude/rules/deterministic-scripts.md): the
  command routing table
- [C# Conventions](../dotnet/csharp-conventions.md): C# idioms and the format gate
