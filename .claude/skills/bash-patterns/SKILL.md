---
name: bash-patterns
description: >
  Write, review, and fix Bash (.sh) scripts using project conventions and best practices.
  Always use this skill when writing any .sh file, creating a new shell script, fixing a
  bash error or runtime error, reviewing shell code, adding a new script to scripts/, or
  when the user asks about bash style, naming, formatting, or patterns. This skill combines
  official style rules with project-specific patterns to produce correct, portable scripts
  on the first try. Trigger on: write bash, create .sh, bash script, shell script, bash error,
  fix .sh, bash style, bash patterns, script error in bash.
---

# Bash Scripting Patterns

Bash 5+ is the project's shell scripting language. Scripts live in `scripts/` and use `.sh` extension.
All scripts must pass `shellcheck` before commit.

Detailed implementation examples: see `references/bash-reference.md`

## Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Script files | kebab-case | `build-project.sh` |
| Functions | snake_case | `function fetch_user()` |
| Local variables | snake_case | `local user_id` |
| Constants | SCREAMING_SNAKE_CASE | `readonly HARVEST_PATH` |
| Environment variables | SCREAMING_SNAKE_CASE | `$APP_VERSION` |

## Script Header Template

```bash
#!/usr/bin/env bash
set -euo pipefail

# One-line summary of what this script does.
#
# Usage:
#   scripts/my-script.sh [--flag] <arg>
```

**Always start with `set -euo pipefail`:**
- `set -e` — exit on error
- `set -u` — error on undefined variables
- `set -o pipefail` — pipe failures propagate

## Formatting Rules

- No trailing whitespace.
- Indent with 2 spaces (not tabs).
- Lines <= 100 chars preferred; wrap long lines with `\`.
- Quote all variable expansions: `"$var"` not `$var`.
- Use `[[ ]]` for conditionals (not `[ ]`).
- Use `$(command)` for subshells (not backticks).

## Argument Parsing Template

```bash
SOLUTION=""
FLAG=false
JSON=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --flag)    FLAG=true; shift ;;
    --json)    JSON=true; shift ;;
    -*)        echo "Unknown flag: $1" >&2; exit 1 ;;
    *)         SOLUTION="$1"; shift ;;
  esac
done

[[ -z "$SOLUTION" ]] && { echo "Usage: script.sh <arg> [--flag]" >&2; exit 1; }
```

## Core Patterns

### JSON Output Mode

Use `jq` for JSON construction and parsing:
```bash
if $JSON; then
  jq -nc \
    --arg status "$status" \
    --argjson count "$count" \
    '{status: $status, count: $count}'
fi
```

### Error Handling

```bash
# Trap for cleanup
cleanup() { rm -f "$TMP_FILE"; }
trap cleanup EXIT

# Check command exists
command -v dotnet &>/dev/null || { echo "dotnet not found" >&2; exit 1; }

# Capture exit code without exiting
output=$(some_command 2>&1) || true
exit_code=$?
```

### Temporary Files

```bash
TMP_FILE="$(mktemp)"
trap 'rm -f "$TMP_FILE"' EXIT
```

### String Operations

```bash
# String contains
if [[ "$text" == *"pattern"* ]]; then echo "found"; fi

# String starts with
if [[ "$text" == "prefix"* ]]; then echo "yes"; fi

# Default value
result="${value:-default}"

# String replacement
new_text="${text//old/new}"
```

### Array Operations

```bash
# Declare array
local -a items=()

# Append
items+=("new_item")

# Length
echo "${#items[@]}"

# Iterate
for item in "${items[@]}"; do
  echo "$item"
done

# Check if empty
if [[ ${#items[@]} -eq 0 ]]; then echo "empty"; fi
```

### Function Pattern

```bash
# Document every function with a comment
# Fetches user data from the API
fetch_user() {
  local user_id="$1"
  local -r endpoint="https://api.example.com/users/${user_id}"

  curl -sf "$endpoint" || return 1
}
```

### Prerequisites Check

```bash
check_prerequisites() {
  local -a missing=()
  for tool in git dotnet jq; do
    command -v "$tool" &>/dev/null || missing+=("$tool")
  done
  if [[ ${#missing[@]} -gt 0 ]]; then
    echo "Missing required tools: ${missing[*]}" >&2
    exit 1
  fi
}
```

### Shared Library Pattern

```bash
# In scripts/lib.sh
#!/usr/bin/env bash

# Shared utility functions sourced by other scripts.

get_repo_root() {
  git rev-parse --show-toplevel
}

log_info() {
  echo "[INFO] $*" >&2
}

log_error() {
  echo "[ERROR] $*" >&2
}

# In consumer script
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${SCRIPT_DIR}/lib.sh"
```

## Command Design Rules

- Max 2 positional parameters; use `--flags` for anything beyond that.
- Document every exported function with a comment line above it.
- Provide both long and short flag forms for commonly-used flags (`-j` / `--json`).
- Validate external tool availability at script start (see Prerequisites pattern).
- Always use `local` for function-scoped variables.
- Use `readonly` for constants that should not change.

## Pitfalls Quick Reference

| Symptom | Root cause | Fix |
|---------|-----------|-----|
| `unbound variable` | `set -u` + unset var | Use `${var:-}` for optional vars |
| Script exits silently | `set -e` + failed command | Add `|| true` for expected failures |
| `command not found` | Missing PATH or wrong name | Check `command -v` first |
| Glob expansion in variable | Unquoted `$var` with `*` | Always quote: `"$var"` |
| Pipe swallows exit code | Default pipe behavior | `set -o pipefail` (in header) |
| Array empty check fails | Wrong syntax | Use `${#arr[@]}` not `$arr` |
| Heredoc indentation breaks | Spaces in heredoc | Use `<<-` with tabs, or `<<'EOF'` |
| Word splitting in loop | Unquoted expansion | Quote arrays: `"${arr[@]}"` |
| Subshell variable lost | `( )` creates subshell | Use `{ }` or process substitution |

## Test Before Commit

Run shellcheck before committing — catches common errors without running the script:

```bash
shellcheck scripts/my-script.sh
```

Then verify it runs without errors:

```bash
bash -n scripts/my-script.sh  # syntax check only
```

`shellcheck` is the authoritative linter. Fix all warnings before committing.

## Self-Improvement (Mandatory)

This skill must get better with every use. After writing or fixing any Bash script:

1. **Add new pitfalls to the quick reference** — If a new error was hit that isn't in the Pitfalls Quick Reference table, add a row immediately (Symptom | Root cause | Fix).
2. **Update version-specific guidance** — If a Bash feature requires a specific version (e.g., associative arrays require Bash 4+), note the version requirement.
3. **Record project-specific patterns** — If a pattern emerged specific to this project's scripts, add it here.
4. **Fix stale instructions immediately** — If any instruction in this skill contradicted what actually worked, correct it before the session ends.
