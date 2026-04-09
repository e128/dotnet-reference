#!/usr/bin/env bash
# Composed verify: format → build → test.
# Usage: check.sh [--all] [--no-format] [--json]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

SCRIPTS="$(dirname "${BASH_SOURCE[0]}")"
ALL=false; NO_FORMAT=false; JSON=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --all)       ALL=true ;;
        --no-format) NO_FORMAT=true ;;
        --json)      JSON=true ;;
        *)           err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

STEPS=()
RESULTS=()
EXIT=0

# Step 1: Format check
if [[ "$NO_FORMAT" == false ]]; then
    STEPS+=("format")
    if "$SCRIPTS/format.sh" --check > /dev/null 2>&1; then
        RESULTS+=("ok")
    else
        RESULTS+=("fail")
        EXIT=1
    fi
fi

# Step 2: Build
STEPS+=("build")
if "$SCRIPTS/build.sh" > /dev/null 2>&1; then
    RESULTS+=("ok")
else
    RESULTS+=("fail")
    [[ $EXIT -eq 0 ]] && EXIT=1
fi

# Step 3: Test
STEPS+=("test")
TEST_ARGS=()
[[ "$ALL" == true ]] && TEST_ARGS+=(--all)
if "$SCRIPTS/test.sh" "${TEST_ARGS[@]}" > /dev/null 2>&1; then
    RESULTS+=("ok")
else
    RESULTS+=("fail")
    [[ $EXIT -eq 0 ]] && EXIT=1
fi

if [[ "$JSON" == true ]]; then
    # Build JSON manually
    printf '{"status":"%s","steps":{' "$([ $EXIT -eq 0 ] && echo ok || echo fail)"
    for i in "${!STEPS[@]}"; do
        [[ $i -gt 0 ]] && printf ','
        printf '"%s":"%s"' "${STEPS[$i]}" "${RESULTS[$i]}"
    done
    printf '}}\n'
else
    for i in "${!STEPS[@]}"; do
        if [[ "${RESULTS[$i]}" == "ok" ]]; then
            ok "${STEPS[$i]}"
        else
            err "${STEPS[$i]}"
        fi
    done
    echo
    if [[ $EXIT -eq 0 ]]; then
        ok "All checks passed"
    else
        err "Some checks failed"
    fi
fi

exit $EXIT
