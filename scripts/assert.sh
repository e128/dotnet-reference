#!/usr/bin/env bash
# Fail-fast multi-condition gate before commits.
# Usage: assert.sh [--clean-working-tree] [--build-pass] [--test-pass] [--json]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

SCRIPTS="$(dirname "${BASH_SOURCE[0]}")"
JSON=false
CHECKS=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --clean-working-tree) CHECKS+=("clean") ;;
        --build-pass)         CHECKS+=("build") ;;
        --test-pass)          CHECKS+=("test") ;;
        --plan-complete)      CHECKS+=("plan") ;;
        --json)               JSON=true ;;
        *)                    err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

if [[ ${#CHECKS[@]} -eq 0 ]]; then
    err "No checks specified"
    exit 1
fi

EXIT=0
declare -A RESULTS

for check in "${CHECKS[@]}"; do
    case "$check" in
        clean)
            if [[ -z "$(git status --porcelain 2>/dev/null)" ]]; then
                RESULTS[$check]="ok"
            else
                RESULTS[$check]="fail"
                EXIT=1
            fi
            ;;
        build)
            if "$SCRIPTS/build.sh" > /dev/null 2>&1; then
                RESULTS[$check]="ok"
            else
                RESULTS[$check]="fail"
                EXIT=1
            fi
            ;;
        test)
            if "$SCRIPTS/test.sh" --all > /dev/null 2>&1; then
                RESULTS[$check]="ok"
            else
                RESULTS[$check]="fail"
                EXIT=1
            fi
            ;;
        plan)
            # Stub — requires plan-path resolution
            RESULTS[$check]="skip"
            ;;
    esac
done

if [[ "$JSON" == true ]]; then
    printf '{"status":"%s","checks":{' "$([ $EXIT -eq 0 ] && echo ok || echo fail)"
    first=true
    for check in "${CHECKS[@]}"; do
        [[ "$first" == true ]] && first=false || printf ','
        printf '"%s":"%s"' "$check" "${RESULTS[$check]}"
    done
    printf '}}\n'
else
    for check in "${CHECKS[@]}"; do
        if [[ "${RESULTS[$check]}" == "ok" ]]; then
            ok "$check"
        elif [[ "${RESULTS[$check]}" == "skip" ]]; then
            dim "skip: $check"
        else
            err "$check"
        fi
    done
fi

exit $EXIT
