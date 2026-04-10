#!/usr/bin/env bash
# Resolve a plan's canonical path by partial name.
# Usage: plan-path.sh NAME
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

if [[ $# -eq 0 ]]; then
    err "Usage: plan-path.sh <plan-name>"
    exit 1
fi

NAME="$1"
ROOT="$(find_repo_root)"
PLANS_DIR="$ROOT/plans"

# Exact match first
if [[ -d "$PLANS_DIR/$NAME" ]]; then
    echo "$PLANS_DIR/$NAME"
    exit 0
fi

# Fuzzy match
MATCHES=()
if [[ -d "$PLANS_DIR" ]]; then
    while IFS= read -r dir; do
        [[ -z "$dir" ]] && continue
        MATCHES+=("$dir")
    done < <(fd -t d "$NAME" "$PLANS_DIR" --max-depth 1 2>/dev/null)
fi

case ${#MATCHES[@]} in
    0) err "No plan found matching: $NAME"; exit 1 ;;
    1) echo "${MATCHES[0]}" ;;
    *) err "Ambiguous match for '$NAME':"; printf "  %s\n" "${MATCHES[@]}"; exit 1 ;;
esac
