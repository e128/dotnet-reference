#!/usr/bin/env bash
# Check NuGet dependencies for outdated and vulnerable packages.
# Usage: dep-check.sh [--outdated] [--vulnerable] [--json]
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

SLN="$(find_solution)"
JSON=false
CHECK_OUTDATED=false
CHECK_VULNERABLE=false

for arg in "$@"; do
    case "$arg" in
        --json) JSON=true ;;
        --outdated) CHECK_OUTDATED=true ;;
        --vulnerable) CHECK_VULNERABLE=true ;;
        *) err "Unknown flag: $arg"; exit 1 ;;
    esac
done

# Default: check both if neither specified
if [[ "$CHECK_OUTDATED" == false && "$CHECK_VULNERABLE" == false ]]; then
    CHECK_OUTDATED=true
    CHECK_VULNERABLE=true
fi

OUTDATED_OUT=""
VULNERABLE_OUT=""

if [[ "$CHECK_OUTDATED" == true ]]; then
    info "Checking outdated packages..."
    OUTDATED_OUT="$(dotnet list "$SLN" package --outdated 2>&1)" || true
fi

if [[ "$CHECK_VULNERABLE" == true ]]; then
    info "Checking vulnerable packages..."
    VULNERABLE_OUT="$(dotnet list "$SLN" package --vulnerable 2>&1)" || true
fi

if [[ "$JSON" == true ]]; then
    # Build JSON output
    printf '{"outdated":'
    if [[ -n "$OUTDATED_OUT" ]]; then
        printf '%s' "$OUTDATED_OUT" | jq -Rs '.'
    else
        printf 'null'
    fi
    printf ',"vulnerable":'
    if [[ -n "$VULNERABLE_OUT" ]]; then
        printf '%s' "$VULNERABLE_OUT" | jq -Rs '.'
    else
        printf 'null'
    fi
    printf '}\n'
else
    if [[ -n "$OUTDATED_OUT" ]]; then
        printf "\n${BOLD}Outdated Packages${RESET}\n"
        echo "$OUTDATED_OUT"
    fi
    if [[ -n "$VULNERABLE_OUT" ]]; then
        printf "\n${BOLD}Vulnerable Packages${RESET}\n"
        echo "$VULNERABLE_OUT"
    fi
fi
