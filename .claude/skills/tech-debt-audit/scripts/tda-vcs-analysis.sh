#!/usr/bin/env bash
# VCS-based analysis for tech-debt-audit Phase 1.
# Outputs hotspots, author concentration, SATD age, and co-change pairs.
set -euo pipefail

MONTHS="${1:-6}"
TOP_N="${2:-30}"
SCOPE="${3:-.}"

echo "=== Hotspot candidates (change frequency, last ${MONTHS} months) ==="
git log --since="${MONTHS} months ago" --name-only --pretty=format: -- "${SCOPE}" |
  grep -v '^$' |
  sort | uniq -c | sort -rn | head -"${TOP_N}"

echo ""
echo "=== Co-change pairs (temporal coupling, last ${MONTHS} months) ==="
git log --since="${MONTHS} months ago" --name-only --pretty=format:"---" -- "${SCOPE}" '*.cs' |
  awk '/^---$/{if(NR>1) for(i in files) for(j in files) if(i<j) print files[i], files[j]; delete files; next} NF{files[$0]=$0}' |
  sort | uniq -c | sort -rn | head -20

echo ""
echo "=== SATD age attribution (oldest unresolved TODOs in .cs files) ==="
git log -S "TODO" --format="%H %ad %ae" --date=short -- "${SCOPE}" '*.cs' |
  head -30

echo ""
echo "=== Author concentration for top hotspot files ==="
TOP_FILES=$(git log --since="${MONTHS} months ago" --name-only --pretty=format: -- "${SCOPE}" |
  grep -v '^$' | sort | uniq -c | sort -rn | head -10 | awk '{print $2}')

for f in ${TOP_FILES}; do
  if [[ -f "${f}" ]]; then
    echo "--- ${f} ---"
    git log --since="12 months ago" --format="%ae" -- "${f}" | sort | uniq -c | sort -rn
  fi
done
