#!/usr/bin/env bash
# File size and churn metrics for tech-debt-audit Phase 1.
# Outputs largest files, highest-churn files, and their intersection.
set -euo pipefail

MONTHS="${1:-6}"
TOP_N="${2:-20}"
SCOPE="${3:-.}"

echo "=== Top ${TOP_N} largest .cs files (by line count) ==="
# Strip the "./" that `fd` adds for scope ".". `git log` emits bare paths, so
# the two lists only intersect once the prefixes agree.
LARGEST=$(fd -e cs . "${SCOPE}" --type f 2>/dev/null |
  sed 's#^\./##' |
  xargs wc -l 2>/dev/null |
  grep -v ' total$' |
  sort -rn)
head -"${TOP_N}" <<< "${LARGEST}"

echo ""
echo "=== Top ${TOP_N} most-changed .cs files (last ${MONTHS} months) ==="
# `|| true` absorbs the SIGPIPE that `head` sends to `sort` under pipefail.
CHURN=$(git log --since="${MONTHS} months ago" --name-only --pretty=format: -- "${SCOPE}" '*.cs' |
  grep -v '^$' |
  sort | uniq -c | sort -rn | head -"${TOP_N}" || true)
echo "${CHURN}"

echo ""
echo "=== Intersection: large AND high-churn (debt hotspot candidates) ==="
LARGE_FILES=$(head -"${TOP_N}" <<< "${LARGEST}" | awk '{print $2}')

# Change count per file, read once instead of grepping CHURN per file.
declare -A CHURN_COUNT
while read -r changes file; do
  if [[ -n "${file:-}" ]]; then CHURN_COUNT["${file}"]="${changes}"; fi
done <<< "${CHURN}"

OVERLAP=""
for f in ${LARGE_FILES}; do
  if [[ -n "${CHURN_COUNT[${f}]:-}" ]]; then
    LINES=$(wc -l < "${f}" 2>/dev/null || echo "?")
    CHANGES="${CHURN_COUNT[${f}]}"
    OVERLAP="${OVERLAP}  ${LINES} LOC, ${CHANGES} changes: ${f}\n"
  fi
done

if [[ -n "${OVERLAP}" ]]; then
  echo -e "${OVERLAP}"
else
  echo "  (no overlap — large files are stable, churning files are small)"
fi
