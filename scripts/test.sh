#!/usr/bin/env bash
# Run tests via dotnet test + Microsoft Testing Platform.
# Usage: test.sh [--all] [--verbose] [--trait KEY=VALUE] [CLASS_NAME...]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

VERBOSE=false; ALL=false; DRY_RUN=false
TRAIT="Category=CI"
CLASSES=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json)    : ;;  # legacy alias — terse is now the default
        --verbose) VERBOSE=true ;;
        --all)     ALL=true ;;
        --dry-run) DRY_RUN=true ;;
        --trait)   TRAIT="$2"; shift ;;
        -*)        err "Unknown flag: $1"; exit 1 ;;
        *)         CLASSES+=("$1") ;;
    esac
    shift
done

ROOT="$(find_repo_root)"
SOLUTION="$(find_solution)"

# Build first (debug config for fast iteration)
if ! dotnet build "$SOLUTION" --nologo -clp:ErrorsOnly > /dev/null 2>&1; then
    err "Build failed — fix build errors before running tests"
    exit 1
fi

# Construct dotnet test command
CMD=(dotnet test --solution "$SOLUTION" --no-build)

# MTP args go after the -- separator
MTP_ARGS=()

if [[ "$ALL" == true ]]; then
    # No filter — run everything including Docker and Manual
    :
elif [[ ${#CLASSES[@]} -gt 0 ]]; then
    # Filter to specific test class
    MTP_ARGS+=(--filter-class "${CLASSES[0]}")
else
    # Default: CI category only
    MTP_ARGS+=(--filter-trait "$TRAIT")
fi

if [[ ${#MTP_ARGS[@]} -gt 0 ]]; then
    CMD+=(-- "${MTP_ARGS[@]}")
fi

if [[ "$DRY_RUN" == true ]]; then
    dim "Would run: ${CMD[*]}"
    exit 0
fi

# Run and capture output (suppress set -e: MTP returns non-zero for zero-match projects)
output=$("${CMD[@]}" 2>&1) && EXIT_CODE=0 || EXIT_CODE=$?

# MTP exit code 8 means "zero tests matched filter" — not a real failure.
# Only treat exit codes 1 (test failure) and 2 (test error) as actual failures.
# Exit code 8 from individual projects when using --filter-class is expected.
REAL_FAILED=0
if [[ $EXIT_CODE -eq 1 ]]; then
    REAL_FAILED=1
elif [[ $EXIT_CODE -eq 2 ]]; then
    # Exit code 2 can mean "no tests matched" in some MTP versions, or a real error
    # Check if the output contains actual test failures
    if echo "$output" | grep -q "failed with"; then
        REAL_FAILED=1
    fi
fi

# Parse MTP summary: total: N, failed: N, succeeded: N, skipped: N
total=$(echo "$output" | sed -n 's/.*total: *\([0-9]*\).*/\1/p' | tail -1)
failed=$(echo "$output" | sed -n 's/.*failed: *\([0-9]*\).*/\1/p' | tail -1)
succeeded=$(echo "$output" | sed -n 's/.*succeeded: *\([0-9]*\).*/\1/p' | tail -1)
skipped=$(echo "$output" | sed -n 's/.*skipped: *\([0-9]*\).*/\1/p' | tail -1)

total=${total:-0}; failed=${failed:-0}; succeeded=${succeeded:-0}; skipped=${skipped:-0}

if [[ "$VERBOSE" == true ]]; then
    if [[ $REAL_FAILED -eq 1 || "$failed" -gt 0 ]]; then
        err "Tests failed: $failed failed, $succeeded passed, $skipped skipped"
        # Show full failure details from MTP output
        echo "$output" | grep -A 20 "failed" | head -50
    else
        ok "Tests passed: $succeeded passed, $skipped skipped"
    fi
else
    json_object status="$([ "$REAL_FAILED" -eq 0 ] && echo ok || echo fail)" \
        total="$total" passed="$succeeded" failed="$failed" skipped="$skipped"
fi

# Return 0 only if no real failures; exit code 8 (zero matches) is not an error
if [[ $REAL_FAILED -eq 0 && $EXIT_CODE -eq 8 ]]; then
    exit 0
fi

exit $EXIT_CODE