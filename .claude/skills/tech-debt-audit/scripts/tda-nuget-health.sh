#!/usr/bin/env bash
# NuGet package health analysis for tech-debt-audit.
# Runs vulnerable, outdated, and deprecated checks in parallel.
set -euo pipefail

TMPDIR="${TMPDIR:-/tmp}"
WORK=$(mktemp -d "${TMPDIR}/tda-nuget.XXXXXX")
trap 'rm -rf "${WORK}"' EXIT

echo "=== NuGet Package Health ==="
echo "(Running 3 checks in parallel...)"
echo ""

dotnet list package --vulnerable --include-transitive > "${WORK}/vulnerable.txt" 2>&1 &
PID_V=$!

dotnet list package --outdated --include-transitive > "${WORK}/outdated.txt" 2>&1 &
PID_O=$!

dotnet list package --deprecated --include-transitive > "${WORK}/deprecated.txt" 2>&1 &
PID_D=$!

wait ${PID_V} || true
wait ${PID_O} || true
wait ${PID_D} || true

echo "## Vulnerable Packages"
cat "${WORK}/vulnerable.txt"
echo ""

echo "## Outdated Packages"
cat "${WORK}/outdated.txt"
echo ""

echo "## Deprecated Packages"
cat "${WORK}/deprecated.txt"
