#!/usr/bin/env bash
# Type dependency graph: callers, callees, interfaces.
# Usage: deps.sh TYPE [--callers] [--callees] [--interfaces] [--dir DIR] [--json]
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

TYPE=""
SHOW_CALLERS=false
SHOW_CALLEES=false
SHOW_IFACES=false
SEARCH_DIR=""
JSON=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --callers)    SHOW_CALLERS=true; shift ;;
        --callees)    SHOW_CALLEES=true; shift ;;
        --interfaces) SHOW_IFACES=true; shift ;;
        --dir)        SEARCH_DIR="$2"; shift 2 ;;
        --json)       JSON=true; shift ;;
        -h|--help)
            echo "Usage: deps.sh TYPE [--callers] [--callees] [--interfaces] [--dir DIR] [--json]"
            exit 0 ;;
        -*)  err "Unknown option: $1"; exit 1 ;;
        *)   TYPE="$1"; shift ;;
    esac
done

if [[ -z "$TYPE" ]]; then
    err "Specify a type or method name"
    exit 1
fi

# Default: show all
if ! $SHOW_CALLERS && ! $SHOW_CALLEES && ! $SHOW_IFACES; then
    SHOW_CALLERS=true
    SHOW_CALLEES=true
    SHOW_IFACES=true
fi

ROOT="$(find_repo_root)"
if [[ -n "$SEARCH_DIR" ]]; then
    DIRS=("$SEARCH_DIR")
else
    DIRS=("$ROOT/src" "$ROOT/tests")
fi

find_refs() {
    local pattern="\b${TYPE}\b"
    for dir in "${DIRS[@]}"; do
        [[ -d "$dir" ]] || continue
        rg --line-number --max-count 50 "$pattern" "$dir" -g '*.cs' 2>/dev/null || true
    done
}

find_interfaces() {
    local pattern="\b(class|record)\s+${TYPE}\b.*:"
    for dir in "${DIRS[@]}"; do
        [[ -d "$dir" ]] || continue
        rg --line-number "$pattern" "$dir" -g '*.cs' 2>/dev/null || true
    done
}

is_definition() {
    local text="$1"
    echo "$text" | rg -q '\b(class|record|struct|interface|enum)\s+'"$TYPE"'\b' 2>/dev/null
}

format_refs() {
    local raw="$1" kind="$2"
    [[ -z "$raw" ]] && return
    while IFS= read -r rawline; do
        [[ -z "$rawline" ]] && continue
        local file line rest
        file="$(echo "$rawline" | cut -d: -f1)"
        line="$(echo "$rawline" | cut -d: -f2)"
        rest="$(echo "$rawline" | cut -d: -f3- | sed 's/^ *//')"

        if is_definition "$rest"; then
            continue
        fi

        if $JSON; then
            local escaped
            escaped="$(echo "$rest" | head -c 120 | sed 's/\\/\\\\/g; s/"/\\"/g')"
            printf '{"file":"%s","line":%s,"match":"%s","kind":"%s"}\n' \
                "$file" "$line" "$escaped" "$kind"
        else
            local fshort="${file#"$ROOT/"}"
            printf "  %s:%s  %s\n" "$fshort" "$line" "$(echo "$rest" | head -c 100)"
        fi
    done <<< "$raw"
}

CALLERS_RAW=""
CALLEES_RAW=""
IFACES_RAW=""

$SHOW_CALLERS && CALLERS_RAW="$(find_refs)"
$SHOW_CALLEES && CALLEES_RAW="$(find_refs)"
$SHOW_IFACES && IFACES_RAW="$(find_interfaces)"

if $JSON; then
    callers_json="$(format_refs "$CALLERS_RAW" "caller" | jq -s '.' 2>/dev/null || echo '[]')"
    callees_json="$(format_refs "$CALLEES_RAW" "callee" | jq -s '.' 2>/dev/null || echo '[]')"
    ifaces_json="[]"
    if [[ -n "$IFACES_RAW" ]]; then
        ifaces_json="$(echo "$IFACES_RAW" | while IFS= read -r rawline; do
            [[ -z "$rawline" ]] && continue
            local_file="$(echo "$rawline" | cut -d: -f1)"
            local_line="$(echo "$rawline" | cut -d: -f2)"
            local_rest="$(echo "$rawline" | cut -d: -f3-)"
            local_ifaces="$(echo "$local_rest" | sed 's/.*://' | tr ',' '\n' | sed 's/^ *//;s/ *$//' | rg '^I' 2>/dev/null | jq -R . | jq -s '.' 2>/dev/null || echo '[]')"
            printf '{"file":"%s","line":%s,"interfaces":%s}\n' "$local_file" "$local_line" "$local_ifaces"
        done | jq -s '.' 2>/dev/null || echo '[]')"
    fi
    jq -n --arg type "$TYPE" \
        --argjson callers "$callers_json" \
        --argjson callees "$callees_json" \
        --argjson interfaces "$ifaces_json" \
        '{type: $type, callers: $callers, callees: $callees, interfaces: $interfaces}'
else
    CALLERS_FMT="$(format_refs "$CALLERS_RAW" "caller")"
    CALLEES_FMT="$(format_refs "$CALLEES_RAW" "callee")"

    c_count=0; [[ -n "$CALLERS_FMT" ]] && c_count=$(echo "$CALLERS_FMT" | wc -l | tr -d ' ')
    d_count=0; [[ -n "$CALLEES_FMT" ]] && d_count=$(echo "$CALLEES_FMT" | wc -l | tr -d ' ')

    if [[ $c_count -eq 0 && $d_count -eq 0 && -z "$IFACES_RAW" ]]; then
        echo "No dependencies found for '$TYPE'"
        exit 0
    fi

    echo "Dependencies for '$TYPE'"
    echo "────────────────────────────────────────────────────────────────"

    if [[ $c_count -gt 0 ]] && $SHOW_CALLERS; then
        echo ""
        echo "Callers ($c_count):"
        echo "$CALLERS_FMT"
    fi

    if [[ $d_count -gt 0 ]] && $SHOW_CALLEES; then
        echo ""
        echo "Callees ($d_count):"
        echo "$CALLEES_FMT"
    fi

    if [[ -n "$IFACES_RAW" ]] && $SHOW_IFACES; then
        echo ""
        echo "Implemented interfaces:"
        while IFS= read -r rawline; do
            [[ -z "$rawline" ]] && continue
            local_file="$(echo "$rawline" | cut -d: -f1)"
            local_line="$(echo "$rawline" | cut -d: -f2)"
            local_rest="$(echo "$rawline" | cut -d: -f3- | sed 's/.*://')"
            local_fshort="${local_file#"$ROOT/"}"
            echo "  $local_fshort:$local_line  $local_rest"
        done <<< "$IFACES_RAW"
    fi
fi
