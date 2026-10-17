# Tooling

**Required CLI tools.** These must be installed:

- `rg` (ripgrep) — fast regex search
- `fd` — fast file finder that respects `.gitignore`
- `jq` — JSON processor for script output
- `bash` 4.0 or later — required for `mapfile` and associative arrays.
  macOS ships 3.2 at `/bin/bash`, so `scripts/lib.sh` re-execs under Homebrew
  Bash. bash 5.3 is current. Minimum versions: [scripts/README.md](../../scripts/README.md).
- `shellcheck` — bash script linter. `scripts/lint-shell.sh` runs it at warning
  severity. That script is a step of `scripts/check.sh`, `scripts/ci.sh`, and
  `scripts/internal/precommit.sh`. CI pins the version. Install it with
  `brew install shellcheck`.

**Optional:**

- `gitleaks` — secret scan in `scripts/internal/precommit.sh` (`brew install gitleaks`)
- `dotnet-outdated-tool` — NuGet update checker
  (`dotnet tool install -g dotnet-outdated-tool`)
