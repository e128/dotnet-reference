#!/usr/bin/env bash
# Run tests via the xUnit v3 MTP runner.
# Usage: test.sh [--all] [--json] [CLASS_NAME...]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

JSON=false; ALL=false; DRY_RUN=false
CLASSES=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json)    JSON=true ;;
        --all)     ALL=true ;;
        --dry-run) DRY_RUN=true ;;
        -*)        err "Unknown flag: $1"; exit 1 ;;
        *)         CLASSES+=("$1") ;;
    esac
    shift
done

ROOT="$(find_repo_root)"
SOLUTION="$(find_solution)"

# Build first
if ! dotnet build "$SOLUTION" --nologo -clp:ErrorsOnly > /dev/null 2>&1; then
    err "Build failed — fix build errors before running tests"
    exit 1
fi

# Find test assemblies
TEST_DLLS=()
while IFS= read -r f; do TEST_DLLS+=("$f"); done < <(fd -e dll -p 'Tests\.dll$' "$ROOT/artifacts/bin" 2>/dev/null | grep -v '/ref/')

if [[ ${#TEST_DLLS[@]} -eq 0 ]]; then
    err "No test assemblies found in artifacts/bin/"
    exit 1
fi

TOTAL_PASS=0; TOTAL_FAIL=0; TOTAL_SKIP=0; TOTAL_COUNT=0; EXIT_CODE=0

for dll in "${TEST_DLLS[@]}"; do
    ARGS=(dotnet exec "$dll")

    if [[ "$ALL" == false && ${#CLASSES[@]} -gt 0 ]]; then
        ARGS+=(--filter-class "${CLASSES[0]}")
    fi

    if [[ "$DRY_RUN" == true ]]; then
        dim "Would run: ${ARGS[*]}"
        continue
    fi

    output=$("${ARGS[@]}" 2>&1) || EXIT_CODE=$?

    # Parse xUnit summary line: Total: N, Errors: N, Failed: N, Skipped: N
    if summary=$(echo "$output" | grep -oP 'Total: \K\d+.*' | head -1); then
        total=$(echo "$summary" | grep -oP 'Total: \K\d+' || echo 0)
        failed=$(echo "$summary" | grep -oP 'Failed: \K\d+' || echo 0)
        skipped=$(echo "$summary" | grep -oP 'Skipped: \K\d+' || echo 0)
        passed=$((total - failed - skipped))
        TOTAL_COUNT=$((TOTAL_COUNT + total))
        TOTAL_PASS=$((TOTAL_PASS + passed))
        TOTAL_FAIL=$((TOTAL_FAIL + failed))
        TOTAL_SKIP=$((TOTAL_SKIP + skipped))
    fi

    if [[ $EXIT_CODE -ne 0 && "$JSON" == false ]]; then
        echo "$output"
    fi
done

if [[ "$JSON" == true ]]; then
    json_object status="$([ $TOTAL_FAIL -eq 0 ] && echo ok || echo fail)" \
        total="$TOTAL_COUNT" passed="$TOTAL_PASS" failed="$TOTAL_FAIL" skipped="$TOTAL_SKIP"
else
    if [[ $TOTAL_FAIL -eq 0 ]]; then
        ok "Tests passed: $TOTAL_PASS passed, $TOTAL_SKIP skipped"
    else
        err "Tests failed: $TOTAL_FAIL failed, $TOTAL_PASS passed, $TOTAL_SKIP skipped"
    fi
fi

exit $EXIT_CODE
