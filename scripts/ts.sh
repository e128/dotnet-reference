#!/usr/bin/env bash
# Print ISO 8601 UTC timestamp, or update a file's *Updated:* line.
# Usage: ts.sh [FILE]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

TS="$(iso_timestamp)"

if [[ $# -eq 0 ]]; then
    echo "$TS"
    exit 0
fi

FILE="$1"
if [[ ! -f "$FILE" ]]; then
    err "File not found: $FILE"
    exit 1
fi

# Update *Updated:* or *Created:* timestamp in-place
if grep -q '\*Updated:' "$FILE"; then
    sed -i '' "s|\*Updated: .*\*|\*Updated: ${TS}\*|" "$FILE"
    ok "Updated timestamp in $FILE"
elif grep -q '\*Created:' "$FILE"; then
    sed -i '' "s|\*Created: .*\*|\*Created: ${TS}\*|" "$FILE"
    ok "Updated created timestamp in $FILE"
else
    warn "No timestamp line found in $FILE"
    exit 1
fi
