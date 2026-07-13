#!/usr/bin/env bash
# Scan src + tests for analyzer suppressions (#pragma warning disable, [SuppressMessage]).
# Usage: suppression-scan.sh [--json] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

JSON=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json) JSON=true ;;
        --help)
            echo "Usage: suppression-scan.sh [--json] [--help]"
            echo "  Scans src/ and tests/ for analyzer suppressions and emits file:line:rule records."
            echo "  Detects '#pragma warning disable' directives and [SuppressMessage(...)] attributes."
            echo "  --json   Structured JSON array of {file,line,rule,kind} records"
            exit 0
            ;;
        *) err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

ROOT="$(find_repo_root)"

# Single-pass scan. Anchor to line start so doc-comment prose mentioning the
# tokens (e.g. '/// <c>#pragma warning disable</c>') is not matched.
RAW="$(rg --no-heading --line-number --no-messages --color never \
    -e '^\s*#pragma\s+warning\s+disable\b' \
    -e '^\s*\[[^]]*SuppressMessage' \
    -g '*.cs' "$ROOT/src" "$ROOT/tests" 2>/dev/null || true)"

# Accumulate tab-separated records: file<TAB>line<TAB>rule<TAB>kind
records=""
if [[ -n "$RAW" ]]; then
    while IFS= read -r hit; do
        [[ -z "$hit" ]] && continue
        file="${hit%%:*}"; rest="${hit#*:}"
        lineno="${rest%%:*}"; content="${rest#*:}"
        rel="${file#"$ROOT"/}"

        if [[ "$content" == *'#pragma'* ]]; then
            kind="pragma"
            rules_part="${content#*disable}"   # portion after 'disable'
            rules_part="${rules_part%%//*}"     # drop trailing // comment
            rules_part="${rules_part//,/ }"     # commas -> spaces
            for r in $rules_part; do
                [[ "$r" =~ ^[A-Za-z][A-Za-z0-9]+$ ]] || continue
                records+="${rel}"$'\t'"${lineno}"$'\t'"${r}"$'\t'"${kind}"$'\n'
            done
        else
            kind="suppressmessage"
            rule="?"
            if [[ "$content" =~ SuppressMessage[[:space:]]*\([[:space:]]*\"[^\"]*\"[[:space:]]*,[[:space:]]*\"([A-Za-z0-9]+) ]]; then
                rule="${BASH_REMATCH[1]}"
            fi
            records+="${rel}"$'\t'"${lineno}"$'\t'"${rule}"$'\t'"${kind}"$'\n'
        fi
    done <<< "$RAW"
fi

records="${records%$'\n'}"
COUNT=0
[[ -n "$records" ]] && COUNT=$(printf '%s\n' "$records" | grep -c '.' || true)

# ── JSON ──
if [[ "$JSON" == true ]]; then
    if [[ -z "$records" ]]; then
        echo '[]'
        exit 0
    fi
    printf '%s\n' "$records" | jq -R -s '
        split("\n") | map(select(length > 0)) | map(split("\t")) |
        map({file: .[0], line: (.[1] | tonumber), rule: .[2], kind: .[3]})'
    exit 0
fi

# ── Human-readable ──
if [[ "$COUNT" -eq 0 ]]; then
    ok "No suppressions found in src/ or tests/"
    exit 0
fi

printf "${BOLD}%-4s %-16s %s${RESET}\n" "KIND" "RULE" "LOCATION"
printf '%s\n' "$records" | while IFS=$'\t' read -r rel lineno rule kind; do
    printf "%-4s %-16s %s:%s\n" "${kind:0:4}" "$rule" "$rel" "$lineno"
done
printf "\n${BOLD}Total:${RESET} %d suppression(s)\n" "$COUNT"
