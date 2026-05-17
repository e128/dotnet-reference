#!/usr/bin/env bash
# File structure outline with line ranges for targeted reading.
# Usage: file-outline.sh PATH [--json] [--method NAME] [--section PATTERN]
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

FILE=""
JSON=false
FILTER_METHOD=""
FILTER_SECTION=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json)    JSON=true; shift ;;
        --method)  FILTER_METHOD="$2"; shift 2 ;;
        --section) FILTER_SECTION="$2"; shift 2 ;;
        -h|--help)
            echo "Usage: file-outline.sh PATH [--json] [--method NAME] [--section PATTERN]"
            exit 0 ;;
        -*)  err "Unknown option: $1"; exit 1 ;;
        *)   FILE="$1"; shift ;;
    esac
done

if [[ -z "$FILE" ]]; then
    err "No file path specified"
    exit 1
fi

if [[ ! -f "$FILE" ]]; then
    err "File not found: $FILE"
    exit 1
fi

TOTAL=$(wc -l < "$FILE" | tr -d ' ')
EXT="${FILE##*.}"

# Pattern sets by file type
outline_cs() {
    local entries=""
    # Namespaces
    local ns
    ns="$(rg --line-number '^\s*namespace\s+[\w.]+' "$FILE" 2>/dev/null || true)"
    while IFS= read -r line; do
        [[ -z "$line" ]] && continue
        local lnum="${line%%:*}"
        local text="${line#*:}"
        text="$(echo "$text" | sed 's/^ *//')"
        entries="${entries}${lnum}|namespace|${text}
"
    done <<< "$ns"

    # Types (class, record, struct, interface, enum)
    local types
    types="$(rg --line-number '\b(class|record|struct|interface|enum)\s+\w+' "$FILE" 2>/dev/null || true)"
    while IFS= read -r line; do
        [[ -z "$line" ]] && continue
        local lnum="${line%%:*}"
        local text="${line#*:}"
        text="$(echo "$text" | sed 's/^ *//')"
        entries="${entries}${lnum}|type|${text}
"
    done <<< "$types"

    # Methods
    local methods
    methods="$(rg --line-number '^\s*(public|private|protected|internal)\s+(static\s+|async\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*\w+(\<[^>]+\>)?(\[\])?\s+\w+\s*\(' "$FILE" 2>/dev/null || true)"
    while IFS= read -r line; do
        [[ -z "$line" ]] && continue
        local lnum="${line%%:*}"
        local text="${line#*:}"
        text="$(echo "$text" | sed 's/^ *//')"
        entries="${entries}${lnum}|method|${text}
"
    done <<< "$methods"

    # Properties
    local props
    props="$(rg --line-number '^\s*(public|private|protected|internal)\s+(static\s+|virtual\s+|override\s+|abstract\s+)*\w+(\?|(\<[^>]+\>))?\s+\w+\s*\{\s*(get|set)' "$FILE" 2>/dev/null || true)"
    while IFS= read -r line; do
        [[ -z "$line" ]] && continue
        local lnum="${line%%:*}"
        local text="${line#*:}"
        text="$(echo "$text" | sed 's/^ *//')"
        entries="${entries}${lnum}|property|${text}
"
    done <<< "$props"

    echo "$entries" | sort -t'|' -k1 -n | uniq
}

outline_md() {
    rg --line-number '^#{1,6}\s+.+$' "$FILE" 2>/dev/null | while IFS= read -r line; do
        local lnum="${line%%:*}"
        local text="${line#*:}"
        text="$(echo "$text" | sed 's/^ *//')"
        echo "${lnum}|heading|${text}"
    done
}

# Run the appropriate outliner
case "$EXT" in
    cs|razor|cshtml)  ENTRIES="$(outline_cs)" ;;
    md)               ENTRIES="$(outline_md)" ;;
    *)                ENTRIES="${1}|file|unknown-format" ;;
esac

# Apply filters
if [[ -n "$FILTER_METHOD" ]]; then
    ENTRIES="$(echo "$ENTRIES" | grep -i "$FILTER_METHOD" || true)"
fi
if [[ -n "$FILTER_SECTION" ]]; then
    ENTRIES="$(echo "$ENTRIES" | grep -i "$FILTER_SECTION" || true)"
fi

# Output
if $JSON; then
    ITEMS="[]"
    while IFS='|' read -r lnum kind text; do
        [[ -z "$lnum" ]] && continue
        ITEMS="$(echo "$ITEMS" | jq \
            --argjson line "$lnum" \
            --arg kind "$kind" \
            --arg name "$(echo "$text" | sed 's/"/\\"/g')" \
            --argjson total "$TOTAL" \
            '. + [{"line": $line, "kind": $kind, "name": $name, "end_line": $total}]')"
    done <<< "$ENTRIES"
    jq -n --arg file "$FILE" --arg ext "$EXT" --argjson total "$TOTAL" --argjson entries "$ITEMS" \
        '{file: $file, extension: $ext, total_lines: $total, entries: $entries}'
else
    COUNT=$(echo "$ENTRIES" | grep -c '.' 2>/dev/null || echo 0)
    if [[ "$COUNT" -eq 0 ]]; then
        echo "No matching entries in $FILE ($TOTAL lines)"
        exit 0
    fi
    printf "File: %s  %s lines\n" "$FILE" "$TOTAL"
    echo "────────────────────────────────────────────────────────────────────"
    while IFS='|' read -r lnum kind text; do
        [[ -z "$lnum" ]] && continue
        printf "  %-10s  %-50s  L%s\n" "$kind" "$text" "$lnum"
    done <<< "$ENTRIES"
    echo "────────────────────────────────────────────────────────────────────"
fi
