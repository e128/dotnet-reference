#!/usr/bin/env bash
set -uo pipefail

# Portable build runner for dotnet-code-overhauler.
# Dependencies: dotnet, jq (for --json mode)
#
# Usage:
#   ./build.sh <solution>              Build — errors only
#   ./build.sh <solution> --warnings   Include warnings
#   ./build.sh <solution> --json       Machine-readable JSON

SOLUTION=""
WARNINGS=false
JSON=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --warnings) WARNINGS=true; shift ;;
    --json)     JSON=true; shift ;;
    -*)         echo "Unknown flag: $1" >&2; exit 1 ;;
    *)          SOLUTION="$1"; shift ;;
  esac
done

[[ -z "$SOLUTION" ]] && { echo "Usage: build.sh <solution> [--warnings] [--json]" >&2; exit 1; }

mkdir -p .claude/tmp/overhauler
TMP=".claude/tmp/overhauler/build-output.txt"

$JSON || echo "Building $SOLUTION..."

if dotnet build "$SOLUTION" > "$TMP" 2>&1; then
  BUILD_OK=true
else
  BUILD_OK=false
fi

error_count=$(grep -oE '[0-9]+ Error\(s\)' "$TMP" | head -1 | grep -oE '^[0-9]+' || echo 0)
warning_count=$(grep -oE '[0-9]+ Warning\(s\)' "$TMP" | head -1 | grep -oE '^[0-9]+' || echo 0)

if $JSON; then
  if $WARNINGS; then
    diagnostics=$(grep -E ': (error|warning) ' "$TMP" | sort -u | jq -R -s 'split("\n") | map(select(length > 0))')
  else
    diagnostics=$(grep -E ': error ' "$TMP" | sort -u | jq -R -s 'split("\n") | map(select(length > 0))')
  fi
  [[ -z "$diagnostics" ]] && diagnostics="[]"

  $BUILD_OK && status="pass" || status="fail"

  jq -nc \
    --arg status "$status" \
    --argjson errors "$error_count" \
    --argjson warnings "$warning_count" \
    --argjson diagnostics "$diagnostics" \
    '{status: $status, errors: $errors, warnings: $warnings, diagnostics: $diagnostics}'

  $BUILD_OK || exit 1
  exit 0
fi

# Human output
echo ""
if $WARNINGS; then
  grep -E ': (error|warning) |Build succeeded|Build FAILED| Error\(s\)| Warning\(s\)' "$TMP" | sort -u || true
else
  grep -E ': error |Build succeeded|Build FAILED| Error\(s\)| Warning\(s\)' "$TMP" | sort -u || true
fi
echo ""

if $BUILD_OK; then
  echo "Result: PASS ($error_count errors, $warning_count warnings)"
else
  echo "Result: FAIL ($error_count errors, $warning_count warnings)"
  exit 1
fi
