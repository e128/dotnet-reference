#!/usr/bin/env bash
set -uo pipefail

# Portable composed verify for dotnet-code-overhauler.
# Format-gate -> build -> test in one pass. Solution-agnostic.
# Dependencies: dotnet, jq (for --json mode)
#
# Usage:
#   ./check.sh <solution>              Format + build + test
#   ./check.sh <solution> --no-format  Skip format gate
#   ./check.sh <solution> --json       Machine-readable JSON

SOLUTION=""
NO_FORMAT=false
JSON=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --no-format) NO_FORMAT=true; shift ;;
    --json)      JSON=true; shift ;;
    -*)          echo "Unknown flag: $1" >&2; exit 1 ;;
    *)           SOLUTION="$1"; shift ;;
  esac
done

[[ -z "$SOLUTION" ]] && { echo "Usage: check.sh <solution> [--no-format] [--json]" >&2; exit 1; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FAILURES=()

# ── 1. Format gate ──
FORMAT_STATUS="skipped"
if ! $NO_FORMAT; then
  $JSON || echo "[ 1/3 ] Format gate..."
  if dotnet format "$SOLUTION" --verify-no-changes > /dev/null 2>&1; then
    FORMAT_STATUS="clean"
  else
    FORMAT_STATUS="fail"
    FAILURES+=("format")
  fi
fi

# ── 2. Build ──
$JSON || echo "[ 2/3 ] Build $SOLUTION..."
BUILD_JSON=$("$SCRIPT_DIR/build.sh" "$SOLUTION" --json 2>/dev/null) || true
BUILD_EXIT=$?
BUILD_ERRORS=$(echo "$BUILD_JSON" | jq -r '.errors // 0')
BUILD_WARNINGS=$(echo "$BUILD_JSON" | jq -r '.warnings // 0')

if [[ $BUILD_EXIT -ne 0 ]]; then
  FAILURES+=("build")
  if $JSON; then
    jq -nc \
      --arg status "fail" \
      --arg format "$FORMAT_STATUS" \
      --argjson build_errors "${BUILD_ERRORS:-0}" \
      --argjson build_warnings "${BUILD_WARNINGS:-0}" \
      '{status: $status, format: $format, build: {errors: $build_errors, warnings: $build_warnings}, tests: {passed: 0, failed: 0}}'
    exit 1
  fi
  echo "Build FAILED — skipping tests"
  exit 1
fi

# ── 3. Test ──
$JSON || echo "[ 3/3 ] Tests..."
TEST_JSON=$("$SCRIPT_DIR/test.sh" "$SOLUTION" --json 2>/dev/null) || true
TEST_EXIT=$?
TEST_PASSED=$(echo "$TEST_JSON" | jq -r '.passed // 0')
TEST_FAILED=$(echo "$TEST_JSON" | jq -r '.failed // 0')

[[ $TEST_EXIT -ne 0 ]] && FAILURES+=("tests")

if $JSON; then
  [[ ${#FAILURES[@]} -eq 0 ]] && status="pass" || status="fail"
  jq -nc \
    --arg status "$status" \
    --arg format "$FORMAT_STATUS" \
    --argjson build_errors "${BUILD_ERRORS:-0}" \
    --argjson build_warnings "${BUILD_WARNINGS:-0}" \
    --argjson test_passed "${TEST_PASSED:-0}" \
    --argjson test_failed "${TEST_FAILED:-0}" \
    '{status: $status, format: $format, build: {errors: $build_errors, warnings: $build_warnings}, tests: {passed: $test_passed, failed: $test_failed}}'
  [[ ${#FAILURES[@]} -eq 0 ]] || exit 1
  exit 0
fi

echo ""
if [[ ${#FAILURES[@]} -eq 0 ]]; then
  echo "Result: PASS"
else
  for f in "${FAILURES[@]}"; do echo "  FAIL: $f"; done
  echo ""
  echo "Result: FAIL"
  exit 1
fi
