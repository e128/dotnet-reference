#!/usr/bin/env bash
# Run tests via dotnet test + Microsoft Testing Platform.
# Usage: test.sh [--all] [--verbose] [--trait KEY=VALUE] [--framework TFM] [CLASS_NAME...]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

VERBOSE=false; ALL=false; DRY_RUN=false
TRAIT="Category=CI"
CLASSES=()
FRAMEWORK=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json)    : ;;  # legacy alias — terse is now the default
        --verbose) VERBOSE=true ;;
        --all)     ALL=true ;;
        --dry-run) DRY_RUN=true ;;
        --trait)   TRAIT="$2"; shift ;;
        --framework) FRAMEWORK="$2"; shift ;;
        -*)        err "Unknown flag: $1"; exit 1 ;;
        *)         CLASSES+=("$1") ;;
    esac
    shift
done

SOLUTION="$(find_solution)"

if [[ -n "$FRAMEWORK" ]]; then
    # A framework leg runs one project. A solution build with -f would push the
    # single TFM into every project, including the netstandard2.0 analyzers.
    TARGET_PROJECT="$(find_repo_root)/tests/E128.Analyzers.Tests/E128.Analyzers.Tests.csproj"
    BUILD_CMD=(dotnet build "$TARGET_PROJECT" --nologo -clp:ErrorsOnly -f "$FRAMEWORK")
    TEST_HEAD=(dotnet test "$TARGET_PROJECT" --no-build -f "$FRAMEWORK")
else
    BUILD_CMD=(dotnet build "$SOLUTION" --nologo -clp:ErrorsOnly)
    TEST_HEAD=(dotnet test --solution "$SOLUTION" --no-build)
fi

# Build first (debug config for fast iteration)
if ! "${BUILD_CMD[@]}" > /dev/null 2>&1; then
    err "Build failed — fix build errors before running tests"
    exit 1
fi

# Construct dotnet test command
CMD=("${TEST_HEAD[@]}")

# MTP args go after the -- separator
MTP_ARGS=()

if [[ "$ALL" == true ]]; then
    # No filter — run everything including Podman and Manual
    :
elif [[ ${#CLASSES[@]} -gt 0 ]]; then
    # Filter to specific test class(es) — MTP supports repeated --filter-class.
    # MTP matches the fully qualified name, so a bare class name matches nothing.
    # Expand a bare name to a suffix wildcard so `test.sh MyTests` works.
    for cls in "${CLASSES[@]}"; do
        if [[ "$cls" != *.* && "$cls" != *'*'* ]]; then
            cls="*$cls"
        fi
        MTP_ARGS+=(--filter-class "$cls")
    done
elif [[ -n "$TRAIT" ]]; then
    # Default: CI category only. An empty --trait supplies no filter.
    MTP_ARGS+=(--filter-trait "$TRAIT")
fi

# A filter exists only when a class or a non-empty trait narrows the run.
FILTERED=false
if [[ "$ALL" != true && ( ${#CLASSES[@]} -gt 0 || -n "$TRAIT" ) ]]; then
    FILTERED=true
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

# A filter that matches nothing is a typo, not a pass. MTP needs the fully
# qualified class name, so a mistyped class would otherwise exit 0. With --all
# no filter exists, so a zero total points at the summary parse, not a typo.
# The same holds for an empty --trait, so the message keys on FILTERED.
if [[ $total -eq 0 && $REAL_FAILED -eq 0 ]]; then
    if [[ "$VERBOSE" == true ]]; then
        if [[ "$FILTERED" == true ]]; then
            err "No test matched the filter: ${CLASSES[*]:-trait $TRAIT}"
        else
            err "No test summary parsed. The MTP output format may have changed."
        fi
    else
        json_object status="$([[ "$FILTERED" == true ]] && echo no-match || echo no-summary)" total=0 passed=0 failed=0 skipped=0
    fi
    exit 3
fi

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

exit "$EXIT_CODE"