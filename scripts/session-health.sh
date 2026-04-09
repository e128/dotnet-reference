#!/usr/bin/env bash
# Error trend check against a saved baseline.
# Usage: session-health.sh [--baseline] [--json]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
BASELINE_FILE="$ROOT/.claude/tmp/error-baseline.json"
BASELINE=false; JSON=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --baseline) BASELINE=true ;;
        --json)     JSON=true ;;
        *)          err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

# Count current errors by category
BUILD_ERRORS=0
if ! dotnet build "$(find_solution)" --nologo -clp:ErrorsOnly > /dev/null 2>&1; then
    BUILD_ERRORS=$(dotnet build "$(find_solution)" --nologo 2>&1 | grep -c ' error ' || true)
fi

FORMAT_ERRORS=0
if ! dotnet format "$(find_solution)" --verify-no-changes --no-restore > /dev/null 2>&1; then
    FORMAT_ERRORS=1
fi

CURRENT="{\"build\":$BUILD_ERRORS,\"format\":$FORMAT_ERRORS}"

if [[ "$BASELINE" == true ]]; then
    mkdir -p "$(dirname "$BASELINE_FILE")"
    echo "$CURRENT" > "$BASELINE_FILE"
    ok "Baseline saved: build=$BUILD_ERRORS, format=$FORMAT_ERRORS"
    exit 0
fi

if [[ "$JSON" == true ]]; then
    if [[ -f "$BASELINE_FILE" ]]; then
        PREV=$(cat "$BASELINE_FILE")
        printf '{"current":%s,"baseline":%s}\n' "$CURRENT" "$PREV"
    else
        printf '{"current":%s,"baseline":null}\n' "$CURRENT"
    fi
else
    printf "${BOLD}Current:${RESET} build=%d format=%d\n" "$BUILD_ERRORS" "$FORMAT_ERRORS"
    if [[ -f "$BASELINE_FILE" ]]; then
        PREV_BUILD=$(echo "$(cat "$BASELINE_FILE")" | grep -oP '"build":\K\d+' || echo "?")
        PREV_FORMAT=$(echo "$(cat "$BASELINE_FILE")" | grep -oP '"format":\K\d+' || echo "?")
        printf "${DIM}Baseline: build=%s format=%s${RESET}\n" "$PREV_BUILD" "$PREV_FORMAT"
    else
        dim "No baseline saved — run with --baseline to set one"
    fi
fi
