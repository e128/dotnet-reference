#!/usr/bin/env bash
# List .cs files modified by the most recent format run.
# Usage: format-invalidate.sh [--json] [--staged]
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

JSON=false
STAGED=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json)   JSON=true; shift ;;
        --staged) STAGED=true; shift ;;
        -h|--help)
            echo "Usage: format-invalidate.sh [--json] [--staged]"
            echo "Lists .cs files with unstaged modifications (touched by format)."
            exit 0 ;;
        *) err "Unknown option: $1"; exit 1 ;;
    esac
done

ROOT="$(find_repo_root)"

# Unstaged .cs modifications
UNSTAGED="$(git -C "$ROOT" diff --name-only -- '*.cs' 2>/dev/null || true)"

# Optionally include staged
STAGED_FILES=""
if $STAGED; then
    STAGED_FILES="$(git -C "$ROOT" diff --cached --name-only -- '*.cs' 2>/dev/null || true)"
fi

# Combine and deduplicate
ALL="$(printf "%s\n%s" "$UNSTAGED" "$STAGED_FILES" | sort -u | sed '/^$/d')"
COUNT=0
[[ -n "$ALL" ]] && COUNT=$(echo "$ALL" | wc -l | tr -d ' ')

if $JSON; then
    FILES_JSON="$(echo "$ALL" | jq -R . 2>/dev/null | jq -s '.' 2>/dev/null || echo '[]')"
    jq -n --argjson count "$COUNT" --argjson files "$FILES_JSON" \
        '{status: "ok", invalidated_count: $count, files: $files}'
else
    if [[ $COUNT -eq 0 ]]; then
        echo "No .cs files modified — nothing to re-read."
    else
        echo "$COUNT file(s) invalidated by format — re-read before editing:"
        while IFS= read -r f; do
            [[ -z "$f" ]] && continue
            echo "  $f"
        done <<< "$ALL"
    fi
fi
