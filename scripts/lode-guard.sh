#!/usr/bin/env bash
# Lode file size guard: check line count before appending to lode files.
# Usage: lode-guard.sh <file> [--json] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lode-lib.sh"

JSON=false
FILE=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json) JSON=true ;;
        --help)
            echo "Usage: lode-guard.sh <file> [--json] [--help]"
            echo "  Check lode file line count against the 250-line limit."
            echo "  Exit 0 if safe to append, exit 1 if over limit."
            echo ""
            echo "  Status thresholds:"
            echo "    ok    ≤${LODE_OK_LIMIT} lines — safe to append"
            echo "    warn  $((LODE_OK_LIMIT + 1))-${LODE_FILE_LIMIT} lines — approaching limit"
            echo "    over  >${LODE_FILE_LIMIT} lines — must decompose into sub-files"
            exit 0
            ;;
        -*)  err "Unknown flag: $1"; exit 1 ;;
        *)
            if [[ -z "$FILE" ]]; then
                FILE="$1"
            else
                err "Unexpected argument: $1"
                exit 1
            fi
            ;;
    esac
    shift
done

if [[ -z "$FILE" ]]; then
    err "File path required"
    echo "Usage: lode-guard.sh <file> [--json]" >&2
    exit 1
fi

if [[ ! -f "$FILE" ]]; then
    if [[ "$JSON" == true ]]; then
        json_object file="$FILE" lines=0 status=ok note="file does not exist yet"
    else
        ok "$FILE does not exist yet — safe to create"
    fi
    exit 0
fi

lines=$(wc -l < "$FILE" | tr -d ' ')

if (( lines <= LODE_OK_LIMIT )); then
    status="ok"
    exit_code=0
elif (( lines <= LODE_FILE_LIMIT )); then
    status="warn"
    exit_code=0
else
    status="over"
    exit_code=1
fi

if [[ "$JSON" == true ]]; then
    json_object file="$FILE" lines="$lines" status="$status"
else
    case "$status" in
        ok)   ok "$FILE: $lines lines — safe to append" ;;
        warn) warn "$FILE: $lines lines — approaching ${LODE_FILE_LIMIT}-line limit" ;;
        over) err "$FILE: $lines lines — over ${LODE_FILE_LIMIT}-line limit, must decompose into sub-files" ;;
    esac
fi

exit "$exit_code"
