#!/usr/bin/env bash
set -uo pipefail

# Portable test runner for dotnet-overhaul.
# Reads the detected test convention from state, or accepts an explicit filter.
# Dependencies: dotnet, jq (for --json mode)
#
# Usage:
#   ./test.sh <solution>                          Full suite using detected convention
#   ./test.sh <solution> --json                   Machine-readable JSON
#   ./test.sh <solution> --filter "Category=CI"   Explicit filter override

SOLUTION=""
FILTER=""
JSON=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --json)     JSON=true; shift ;;
    --filter)   FILTER="$2"; shift 2 ;;
    -*)         echo "Unknown flag: $1" >&2; exit 1 ;;
    *)          SOLUTION="$1"; shift ;;
  esac
done

[[ -z "$SOLUTION" ]] && { echo "Usage: test.sh <solution> [--filter <expr>] [--json]" >&2; exit 1; }

# Load convention if no explicit filter
if [[ -z "$FILTER" ]]; then
  CONVENTION=".claude/tmp/overhauler/test-convention.md"
  if [[ -f "$CONVENTION" ]]; then
    # Extract filter expression from "Filter command:" line
    FILTER=$(grep '^Filter command:' "$CONVENTION" | sed -E 's/.*--filter(-trait)?\s+"?([^"]+)"?.*/\2/' || true)
  fi
fi

$JSON || echo "Running tests: $SOLUTION | filter: ${FILTER:-<none>}"

# Build dotnet test command
if [[ -n "$FILTER" ]]; then
  dotnet test --solution "$SOLUTION" --filter "$FILTER" > .claude/tmp/overhauler/test-output.txt 2>&1 || true
else
  dotnet test --solution "$SOLUTION" > .claude/tmp/overhauler/test-output.txt 2>&1 || true
fi
TEST_OK=$?
# Re-check via file — dotnet test exit code in pipefail
TMP=".claude/tmp/overhauler/test-output.txt"
if [[ $TEST_OK -eq 0 ]]; then TEST_OK=true; else TEST_OK=false; fi

total=$(grep -oE 'total:\s+[0-9]+' "$TMP" | head -1 | grep -oE '[0-9]+' || echo 0)
failed=$(grep -E 'failed:' "$TMP" | grep -v 'succeeded' | head -1 | grep -oE '[0-9]+' || echo 0)
passed=$((total - failed))

# Collect failure details
failure_lines=$(grep -E '^\s+Failed |Error Message:|^\s+Expected:|^\s+But was:' "$TMP" || true)

if $JSON; then
  failures_json=$(echo "$failure_lines" | jq -R -s 'split("\n") | map(select(length > 0))')
  [[ -z "$failures_json" || "$failures_json" == '[""]' ]] && failures_json="[]"

  $TEST_OK && status="pass" || status="fail"

  jq -nc \
    --arg status "$status" \
    --argjson passed "$passed" \
    --argjson failed "$failed" \
    --argjson failures "$failures_json" \
    '{status: $status, passed: $passed, failed: $failed, skipped: 0, failures: $failures}'

  $TEST_OK || exit 1
  exit 0
fi

# Human output — show summary lines
grep -E 'Test run summary|total:|succeeded:|failed:|duration:' "$TMP" || true
echo ""
if $TEST_OK; then
  echo "Result: PASS"
else
  echo "Result: FAIL"
  exit 1
fi
