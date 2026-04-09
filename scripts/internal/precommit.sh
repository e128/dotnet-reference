#!/usr/bin/env bash
# PII scan: check staged files for home paths and email addresses.
# Usage: precommit.sh [--json]
source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

JSON=false
[[ "${1:-}" == "--json" ]] && JSON=true

ROOT="$(find_repo_root)"
STAGED=$(git -C "$ROOT" diff --cached --name-only 2>/dev/null)

if [[ -z "$STAGED" ]]; then
    if [[ "$JSON" == true ]]; then
        json_object status=ok findings=0
    else
        ok "No staged files to scan"
    fi
    exit 0
fi

FINDINGS=0
DETAILS=()

# Check for home directory paths
HOME_PATTERN="/Users/[a-zA-Z]|/home/[a-zA-Z]|C:\\\\Users\\\\"
while IFS= read -r file; do
    [[ ! -f "$ROOT/$file" ]] && continue

    # Skip the co-author line
    matches=$(grep -nE "$HOME_PATTERN" "$ROOT/$file" 2>/dev/null | grep -v "Co-Authored-By" || true)
    if [[ -n "$matches" ]]; then
        FINDINGS=$((FINDINGS + 1))
        DETAILS+=("$file: home path detected")
    fi
done <<< "$STAGED"

if [[ $FINDINGS -gt 0 ]]; then
    if [[ "$JSON" == true ]]; then
        json_object status=fail findings="$FINDINGS"
    else
        err "PII scan found $FINDINGS issue(s):"
        for d in "${DETAILS[@]}"; do
            printf "  %s\n" "$d"
        done
    fi
    exit 1
fi

if [[ "$JSON" == true ]]; then
    json_object status=ok findings=0
else
    ok "PII scan clean"
fi
