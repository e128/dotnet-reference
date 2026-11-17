#!/usr/bin/env bash
# Extract source code for a method, class, or section by name, or line ranges from several files.
# Usage: code-read.sh --method|--class|--section|--line|--range NAME PATH... [--json]
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

MODE=""
QUERY=""
JSON=false
FILES=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --method)  MODE="method";  QUERY="$2"; shift 2 ;;
        --class)   MODE="class";   QUERY="$2"; shift 2 ;;
        --section) MODE="section"; QUERY="$2"; shift 2 ;;
        --line)    MODE="line";    QUERY="$2"; shift 2 ;;
        --range)   MODE="line";    QUERY="$2"; shift 2 ;;
        --json)    JSON=true; shift ;;
        -h|--help)
            echo "Usage: code-read.sh --method|--class|--section|--line|--range NAME PATH... [--json]"
            echo "  --line and --range accept A-B and one or more PATH arguments."
            exit 0 ;;
        -*)  err "Unknown option: $1"; exit 1 ;;
        *)   FILES+=("$1"); shift ;;
    esac
done

if [[ -z "$MODE" || -z "$QUERY" ]]; then
    err "Specify one of: --method, --class, --section, --line, --range"
    exit 1
fi
if [[ ${#FILES[@]} -eq 0 ]]; then
    err "File not found: <none>"
    exit 1
fi
if [[ "$MODE" != "line" && ${#FILES[@]} -gt 1 ]]; then
    err "Only --line and --range accept more than one file"
    exit 1
fi
for f in "${FILES[@]}"; do
    if [[ ! -f "$f" ]]; then
        err "File not found: $f"
        exit 1
    fi
done

# Find the start line of a symbol
find_start_line() {
    local query="$1" mode="$2" file="$3"
    local pattern

    if [[ "$mode" == "method" ]]; then
        # Try definition pattern first (with access modifier)
        pattern="(public|private|protected|internal)\s+(static\s+|async\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*\S+\s+${query}\s*\("
        local def_match
        def_match="$(rg --line-number --max-count 1 "$pattern" "$file" 2>/dev/null || true)"
        if [[ -n "$def_match" ]]; then
            echo "${def_match%%:*}"
            return
        fi
        # Fall back to any usage
        pattern="\b${query}\s*\("
    elif [[ "$mode" == "class" ]]; then
        pattern="\b(class|record|struct|interface|enum)\s+${query}\b"
    elif [[ "$mode" == "section" ]]; then
        pattern="@$(echo "$query" | tr '[:upper:]' '[:lower:]')\b"
    else
        pattern="\b${query}\b"
    fi

    local match
    match="$(rg --line-number --max-count 1 "$pattern" "$file" 2>/dev/null || true)"
    if [[ -n "$match" ]]; then
        echo "${match%%:*}"
    else
        echo "0"
    fi
}

# Walk backwards from match line to include attributes and full signature
walk_back_sig() {
    local file="$1" match_line="$2"
    local idx=$((match_line))
    local -a _l
    mapfile -t _l < "$file" || true

    # Walk backwards over blank lines and attribute lines
    while [[ $idx -gt 1 ]]; do
        local prev
        prev="${_l[idx-2]:-}"
        prev="${prev#"${prev%%[![:space:]]*}"}"
        if [[ -z "$prev" || "$prev" == \[* ]]; then
            idx=$((idx - 1))
        else
            break
        fi
    done
    echo "$idx"
}

# Find end of brace-delimited block by scanning forward
find_brace_end() {
    local file="$1" start_line="$2"
    awk -v start="$start_line" '
    NR >= start {
        # Skip full-line comments
        line = $0
        sub(/\/\/.*$/, "", line)
        n = split(line, chars, "")
        for (i = 1; i <= n; i++) {
            if (chars[i] == "{") { depth++; found = 1 }
            if (chars[i] == "}") depth--
        }
        if (found && depth <= 0) { print NR; exit }
    }
    END { if (!found || depth > 0) print NR }
    ' "$file"
}

# Resolve the range for one file. Sets START_LINE and END_LINE. Returns 1 when the symbol is absent.
extract_lines() {
    local file="$1"

    if [[ "$MODE" == "line" ]]; then
        if [[ "$QUERY" == *-* ]]; then
            START_LINE="${QUERY%-*}"
            END_LINE="${QUERY#*-}"
        else
            START_LINE="$QUERY"
            END_LINE="$QUERY"
        fi
        return 0
    fi

    START_LINE="$(find_start_line "$QUERY" "$MODE" "$file")"
    if [[ "$START_LINE" -eq 0 ]]; then
        return 1
    fi

    # Walk back for methods to include attributes
    if [[ "$MODE" == "method" ]]; then
        START_LINE="$(walk_back_sig "$file" "$START_LINE")"
    fi

    END_LINE="$(find_brace_end "$file" "$START_LINE")"
}

TARGETS=("${FILES[0]}")
if [[ "$MODE" == "line" ]]; then
    TARGETS=("${FILES[@]}")
fi

RECORDS=()
RC=0
for file in "${TARGETS[@]}"; do
    if ! extract_lines "$file"; then
        if $JSON; then
            RECORDS+=("$(jq -n --arg query "$QUERY" --arg type "$MODE" --arg file "$file" \
                '{found: false, query: $query, type: $type, file: $file}')")
        else
            err "No $MODE match for '$QUERY' in $file"
        fi
        RC=1
        continue
    fi

    CONTENT="$(sed -n "${START_LINE},${END_LINE}p" "$file")"

    if $JSON; then
        RECORDS+=("$(jq -n \
            --arg query "$QUERY" \
            --arg type "$MODE" \
            --arg file "$file" \
            --argjson start_line "$START_LINE" \
            --argjson end_line "$END_LINE" \
            --arg content "$CONTENT" \
            '{found: true, query: $query, type: $type, file: $file, start_line: $start_line, end_line: $end_line, content: $content}')")
    else
        echo "─── $MODE: $QUERY ─── $file:${START_LINE}-${END_LINE}"
        echo "$CONTENT"
        echo "───"
    fi
done

if $JSON; then
    if [[ ${#RECORDS[@]} -eq 1 ]]; then
        printf '%s\n' "${RECORDS[0]}" | jq .
    else
        printf '%s\n' "${RECORDS[@]}" | jq -s .
    fi
fi

exit $RC
