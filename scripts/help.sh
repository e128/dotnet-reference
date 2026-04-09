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

printf "${BOLD}%-24s %s${RESET}\n" "Script" "Description"
printf "${DIM}%-24s %s${RESET}\n" "------------------------" "-------------------------------------------"

for script in "$SCRIPTS_DIR"/*.sh; do
    name="$(basename "$script")"

    # Skip self and lib
    [[ "$name" == "help.sh" || "$name" == "lib.sh" ]] && continue

    # Apply filter
    if [[ -n "$FILTER" && "$name" != *"$FILTER"* ]]; then
        continue
    fi

    # Extract description from second line (after shebang)
    desc=$(sed -n '2s/^# *//p' "$script" 2>/dev/null || echo "")
    printf "%-24s %s\n" "$name" "$desc"
done

# Internal scripts
HAS_INTERNAL=false
shopt -s nullglob
for script in "$SCRIPTS_DIR"/internal/*.sh; do
    [[ ! -f "$script" ]] && continue
    name="internal/$(basename "$script")"

    if [[ -n "$FILTER" && "$name" != *"$FILTER"* ]]; then
        continue
    fi

    if [[ "$HAS_INTERNAL" == false ]]; then
        echo
        printf "${BOLD}Internal scripts:${RESET}\n"
        HAS_INTERNAL=true
    fi

    desc=$(sed -n '2s/^# *//p' "$script" 2>/dev/null || echo "")
    printf "%-24s %s\n" "$name" "$desc"
done
