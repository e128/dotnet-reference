# Tooling

**Required CLI tools.** These must be installed:

- `rg` (ripgrep) — fast regex search
- `fd` — fast file finder that respects `.gitignore`
- `jq` — JSON processor for script output
- `bash` 5 or later — required for associative arrays

**Optional:**

- `shellcheck` — bash script linter
- `gitleaks` — secret scan in `scripts/internal/precommit.sh` (`brew install gitleaks`)
- `dotnet-outdated-tool` — NuGet update checker
  (`dotnet tool install -g dotnet-outdated-tool`)
