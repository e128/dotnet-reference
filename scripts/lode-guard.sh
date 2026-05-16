#!/usr/bin/env bash
# Lode file size guard: check line count before appending to lode files.
# Usage: lode-guard.sh <file> [--json] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

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
            echo "    ok    ≤200 lines — safe to append"
            echo "    warn  201-250 lines — approaching limit"
            echo "    over  >250 lines — must decompose into sub-files"
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

if (( lines <= 200 )); then
    status="ok"
    exit_code=0
elif (( lines <= 250 )); then
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
        warn) warn "$FILE: $lines lines — approaching 250-line limit" ;;
        over) err "$FILE: $lines lines — over 250-line limit, must decompose into sub-files" ;;
    esac
fi

exit "$exit_code"
