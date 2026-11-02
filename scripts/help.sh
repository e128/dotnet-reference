#!/usr/bin/env bash
# List available scripts with descriptions.
# Usage: help.sh [FILTER]
set -euo pipefail

SCRIPTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FILTER="${1:-}"

# Colors
if [[ -t 1 ]]; then
    BOLD='\033[1m'; DIM='\033[2m'; RESET='\033[0m'
else
    BOLD=''; DIM=''; RESET=''
fi

# Read the description from line 2 of a script. Builtins avoid one sed per file.
script_desc() {
    local line2=""
    { IFS= read -r _ && IFS= read -r line2; } < "$1" 2>/dev/null || true
    [[ "$line2" == \#* ]] || return 0
    line2="${line2#\#}"
    printf '%s' "${line2#"${line2%%[! ]*}"}"
}

printf "${BOLD}%-24s %s${RESET}\n" "Script" "Description"
printf "${DIM}%-24s %s${RESET}\n" "------------------------" "-------------------------------------------"

for script in "$SCRIPTS_DIR"/*.sh; do
    name="${script##*/}"

    # Skip self and the sourced libraries
    case "$name" in
        help.sh|lib.sh|lode-lib.sh|lode-ollama-lib.sh) continue ;;
    esac

    # Apply filter
    if [[ -n "$FILTER" && "$name" != *"$FILTER"* ]]; then
        continue
    fi

    # Extract description from second line (after shebang)
    desc="$(script_desc "$script")"
    printf "%-24s %s\n" "$name" "$desc"
done

# Internal scripts
HAS_INTERNAL=false
shopt -s nullglob
for script in "$SCRIPTS_DIR"/internal/*.sh; do
    [[ ! -f "$script" ]] && continue
    name="internal/${script##*/}"

    if [[ -n "$FILTER" && "$name" != *"$FILTER"* ]]; then
        continue
    fi

    if [[ "$HAS_INTERNAL" == false ]]; then
        echo
        printf "${BOLD}Internal scripts:${RESET}\n"
        HAS_INTERNAL=true
    fi

    desc="$(script_desc "$script")"
    printf "%-24s %s\n" "$name" "$desc"
done
