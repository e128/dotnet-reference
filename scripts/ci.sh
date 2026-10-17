#!/usr/bin/env bash
# Combined CI pipeline: shell lint + format + build + test.
# Usage: ci.sh [--skip-format] [--skip-build] [--skip-test] [--json]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

SCRIPTS="$(dirname "${BASH_SOURCE[0]}")"
JSON=true; SKIP_FORMAT=false; SKIP_BUILD=false; SKIP_TEST=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --human)       JSON=false ;;
        --json)        JSON=true ;;
        --skip-format) SKIP_FORMAT=true ;;
        --skip-build)  SKIP_BUILD=true ;;
        --skip-test)   SKIP_TEST=true ;;
        *)             err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

EXIT=0
declare -A RESULTS

# Step 1: Shell lint
if "$SCRIPTS/lint-shell.sh" > /dev/null 2>&1; then
    RESULTS[shell]="ok"
else
    RESULTS[shell]="fail"
    EXIT=1
fi

# Step 2: Format
if [[ "$SKIP_FORMAT" == false ]]; then
    if "$SCRIPTS/format.sh" --check > /dev/null 2>&1; then
        RESULTS[format]="ok"
    else
        RESULTS[format]="fail"
        EXIT=1
    fi
fi

# Step 3: Build
if [[ "$SKIP_BUILD" == false ]]; then
    if "$SCRIPTS/build.sh" > /dev/null 2>&1; then
        RESULTS[build]="ok"
    else
        RESULTS[build]="fail"
        [[ $EXIT -eq 0 ]] && EXIT=1
    fi
fi

# Step 4: Test
if [[ "$SKIP_TEST" == false ]]; then
    TEST_ARGS=(--all)
    if "$SCRIPTS/test.sh" "${TEST_ARGS[@]}" > /dev/null 2>&1; then
        RESULTS[test]="ok"
    else
        RESULTS[test]="fail"
        [[ $EXIT -eq 0 ]] && EXIT=1
    fi
fi

if [[ "$JSON" == true ]]; then
    printf '{"status":"%s"' "$([ $EXIT -eq 0 ] && echo ok || echo fail)"
    for step in shell format build test; do
        if [[ -n "${RESULTS[$step]+x}" ]]; then
            printf ',"%s":"%s"' "$step" "${RESULTS[$step]}"
        fi
    done
    printf '}\n'
else
    for step in shell format build test; do
        if [[ -n "${RESULTS[$step]+x}" ]]; then
            if [[ "${RESULTS[$step]}" == "ok" ]]; then
                ok "$step"
            else
                err "$step"
            fi
        fi
    done
fi

exit $EXIT
