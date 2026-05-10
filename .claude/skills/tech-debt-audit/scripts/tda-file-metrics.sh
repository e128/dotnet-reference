#!/usr/bin/env bash
# File size and churn metrics for tech-debt-audit Phase 1.
# Outputs largest files, highest-churn files, and their intersection.
set -euo pipefail

MONTHS="${1:-6}"
TOP_N="${2:-20}"
SCOPE="${3:-.}"

echo "=== Top ${TOP_N} largest .cs files (by line count) ==="
fd -e cs . "${SCOPE}" --type f 2>/dev/null |
  xargs wc -l 2>/dev/null |
  grep -v ' total$' |
  sort -rn |
  head -"${TOP_N}"

echo ""
echo "=== Top ${TOP_N} most-changed .cs files (last ${MONTHS} months) ==="
CHURN=$(git log --since="${MONTHS} months ago" --name-only --pretty=format: -- "${SCOPE}" '*.cs' |
  grep -v '^$' |
  sort | uniq -c | sort -rn | head -"${TOP_N}")
echo "${CHURN}"

echo ""
echo "=== Intersection: large AND high-churn (debt hotspot candidates) ==="
LARGE_FILES=$(fd -e cs . "${SCOPE}" --type f 2>/dev/null |
  xargs wc -l 2>/dev/null |
  grep -v ' total$' |
  sort -rn |
  head -"${TOP_N}" |
  awk '{print $2}')

CHURN_FILES=$(echo "${CHURN}" | awk '{print $2}')

OVERLAP=""
for f in ${LARGE_FILES}; do
  if echo "${CHURN_FILES}" | grep -qF "${f}"; then
    LINES=$(wc -l < "${f}" 2>/dev/null || echo "?")
    CHANGES=$(echo "${CHURN}" | grep -F "${f}" | awk '{print $1}')
    OVERLAP="${OVERLAP}  ${LINES} LOC, ${CHANGES} changes: ${f}\n"
  fi
done

if [[ -n "${OVERLAP}" ]]; then
  echo -e "${OVERLAP}"
else
  echo "  (no overlap — large files are stable, churning files are small)"
fi
