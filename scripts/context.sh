#!/usr/bin/env bash
# Combines status + diff + plans context in one call.
# Usage: context.sh [--minimal] [--json]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

SCRIPTS="$(dirname "${BASH_SOURCE[0]}")"
MINIMAL=false; JSON=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --minimal) MINIMAL=true ;;
        --json)    JSON=true ;;
        *)         err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

if [[ "$JSON" == true ]]; then
    STATUS=$("$SCRIPTS/status.sh" --json)
    DIFF=$("$SCRIPTS/diff.sh" --json)
    PLANS=$("$SCRIPTS/internal/plan-context.sh" --active-only 2>/dev/null || echo '[]')
    printf '{"status":%s,"diff":%s,"plans":%s}\n' "$STATUS" "$DIFF" "$PLANS"
else
    printf "${BOLD}=== Status ===${RESET}\n"
    "$SCRIPTS/status.sh"
    echo

    if [[ "$MINIMAL" == false ]]; then
        printf "${BOLD}=== Diff ===${RESET}\n"
        "$SCRIPTS/diff.sh"
        echo

        printf "${BOLD}=== Plans ===${RESET}\n"
        "$SCRIPTS/internal/plan-context.sh" 2>/dev/null || dim "No active plans"
    fi
fi
