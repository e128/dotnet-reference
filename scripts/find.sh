#!/usr/bin/env bash
# Deterministic symbol lookup via rg and fd.
# Usage: find.sh --class Name | --method Name | --callers Name | --refs Name | --file Name [--dir DIR] [--json]
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

MODE=""
QUERY=""
SEARCH_DIR=""
JSON=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --class)   MODE="class";   QUERY="$2"; shift 2 ;;
        --method)  MODE="method";  QUERY="$2"; shift 2 ;;
        --callers) MODE="callers"; QUERY="$2"; shift 2 ;;
        --refs)    MODE="refs";    QUERY="$2"; shift 2 ;;
        --file)    MODE="file";    QUERY="$2"; shift 2 ;;
        --dir)     SEARCH_DIR="$2"; shift 2 ;;
        --json)    JSON=true; shift ;;
        -h|--help)
            echo "Usage: find.sh --class|--method|--callers|--refs|--file NAME [--dir DIR] [--json]"
            exit 0 ;;
        *) err "Unknown option: $1"; exit 1 ;;
    esac
done

if [[ -z "$MODE" || -z "$QUERY" ]]; then
    err "Specify one of: --class, --method, --callers, --refs, --file"
    exit 1
fi

ROOT="$(find_repo_root)"
if [[ -n "$SEARCH_DIR" ]]; then
    DIRS=("$SEARCH_DIR")
else
    DIRS=("$ROOT/src" "$ROOT/tests")
fi

find_by_file() {
    for dir in "${DIRS[@]}"; do
        [[ -d "$dir" ]] || continue
        fd "$QUERY" "$dir" --type f -e cs -e razor -e md 2>/dev/null || true
    done
}

find_by_pattern() {
    local pattern="$1"
    local kind="$2"
    for dir in "${DIRS[@]}"; do
        [[ -d "$dir" ]] || continue
        rg --line-number --max-count 50 "$pattern" "$dir" -g '*.cs' -g '*.razor' 2>/dev/null || true
    done
}

is_definition_line() {
    local text="$1"
    [[ "$text" =~ (^|[^[:alnum:]_])(public|private|protected|internal)([^[:alnum:]_]|$) ]]
}

format_results() {
    local kind="$1"
    local raw="$2"

    if [[ -z "$raw" ]]; then
        return
    fi

    while IFS= read -r rawline; do
        [[ -z "$rawline" ]] && continue
        local file line rest
        file="${rawline%%:*}"
        rest="${rawline#*:}"
        line="${rest%%:*}"
        rest="${rest#*:}"
        rest="${rest#"${rest%%[! ]*}"}"

        if $JSON; then
            local escaped_rest
            escaped_rest="${rest:0:120}"
            escaped_rest="${escaped_rest//\\/\\\\}"
            escaped_rest="${escaped_rest//\"/\\\"}"
            printf '{"file":"%s","line":%s,"match":"%s","kind":"%s"}\n' \
                "$file" "$line" "$escaped_rest" "$kind"
        else
            local fshort="${file#"$ROOT/"}"
            printf "  %s:%s  %s\n" "$fshort" "$line" "${rest:0:100}"
        fi
    done <<< "$raw"
}

case "$MODE" in
    file)
        RAW="$(find_by_file)"
        if $JSON; then
            arr="$(jq -R -s 'split("\n") | map(select(length > 0)) | map({file: ., kind: "file", match: (split("/") | last), line: 1})' <<< "$RAW")"
            jq -n --arg query "$QUERY" --arg type "$MODE" --argjson results "$arr" \
                '{query: $query, type: $type, results: $results}'
        else
            if [[ -z "$RAW" ]]; then
                echo "No file matches for '$QUERY'"
            else
                echo "file matches for '$QUERY':"
                while IFS= read -r f; do
                    [[ -z "$f" ]] && continue
                    echo "  ${f#"$ROOT/"}"
                done <<< "$RAW"
            fi
        fi
        exit 0
        ;;
    class)
        PATTERN="\b(class|record|struct|interface|enum)\s+${QUERY}\b"
        RAW="$(find_by_pattern "$PATTERN" "type")"
        RESULTS="$(format_results "type" "$RAW")"
        ;;
    method)
        PATTERN="\b${QUERY}\s*\("
        RAW="$(find_by_pattern "$PATTERN" "method")"
        RESULTS="$(format_results "method" "$RAW")"
        ;;
    callers)
        PATTERN="\b${QUERY}\s*\("
        RAW="$(find_by_pattern "$PATTERN" "caller")"
        # Filter out definition lines
        FILTERED=""
        while IFS= read -r rawline; do
            [[ -z "$rawline" ]] && continue
            local_rest="$(echo "$rawline" | cut -d: -f3-)"
            if is_definition_line "$local_rest"; then
                continue
            fi
            FILTERED="${FILTERED}${rawline}
"
        done <<< "$RAW"
        RESULTS="$(format_results "caller" "$FILTERED")"
        ;;
    refs)
        PATTERN="\b${QUERY}\b"
        RAW="$(find_by_pattern "$PATTERN" "reference")"
        RESULTS="$(format_results "reference" "$RAW")"
        ;;
esac

if $JSON; then
    local_arr="$(echo "$RESULTS" | jq -s '.' 2>/dev/null || echo '[]')"
    jq -n --arg query "$QUERY" --arg type "$MODE" --argjson results "$local_arr" \
        '{query: $query, type: $type, results: $results}'
else
    COUNT=0
    if [[ -n "$RESULTS" ]]; then
        COUNT=$(echo "$RESULTS" | wc -l | tr -d ' ')
    fi
    if [[ "$COUNT" -eq 0 ]]; then
        echo "No $MODE matches for '$QUERY'"
    else
        echo "$MODE matches for '$QUERY': $COUNT results"
        echo "────────────────────────────────────────────────────────────────────────────────"
        echo "$RESULTS"
        echo "────────────────────────────────────────────────────────────────────────────────"
    fi
fi
