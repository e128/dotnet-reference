#!/usr/bin/env bash
# Run dotnet format on the solution or specific files.
# Usage: format.sh [--check] [--changed] [FILE...]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

CHECK=false; CHANGED=false
INCLUDES=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --check)   CHECK=true ;;
        --changed) CHANGED=true ;;
        -*)        err "Unknown flag: $1"; exit 1 ;;
        *)         INCLUDES+=("$1") ;;
    esac
    shift
done

SOLUTION="$(find_solution)"
ARGS=(dotnet format "$SOLUTION")

if [[ "$CHECK" == true ]]; then
    ARGS+=(--verify-no-changes)
fi

# Collect files to format
FILES=()
if [[ "$CHANGED" == true ]]; then
    while IFS= read -r f; do FILES+=("$f"); done < <(changed_cs_files)
    if [[ ${#FILES[@]} -eq 0 ]]; then
        ok "No changed .cs files to format"
        exit 0
    fi
elif [[ ${#INCLUDES[@]} -gt 0 ]]; then
    FILES=("${INCLUDES[@]}")
fi

if [[ ${#FILES[@]} -gt 0 ]]; then
    for f in "${FILES[@]}"; do
        ARGS+=(--include "$f")
    done
fi

if ! output=$("${ARGS[@]}" 2>&1); then
    if [[ "$CHECK" == true ]]; then
        err "Format check failed — run format.sh to fix"
        echo "$output"
        exit 1
    else
        err "Format failed"
        echo "$output"
        exit 1
    fi
fi

if [[ "$CHECK" == true ]]; then
    ok "Format check passed"
else
    ok "Format applied"
fi
