#!/usr/bin/env bash
# Analyzer rule and source file statistics.
# Usage: analyzer-stats.sh [--json] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
ANALYZER_DIR="$ROOT/src/E128.Analyzers"
JSON=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json) JSON=true ;;
        --help)
            echo "Usage: analyzer-stats.sh [--json] [--help]"
            echo "  Counts diagnostic IDs, analyzer classes, code fix providers,"
            echo "  PublicAPI entries, and source file metrics."
            echo "  --json   Structured JSON output"
            exit 0
            ;;
        *) err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

if [[ ! -d "$ANALYZER_DIR" ]]; then
    err "Analyzer project not found at $ANALYZER_DIR"
    exit 1
fi

mapfile -t diagnostic_ids < <(rg 'DiagnosticId\s*=\s*"(E128[0-9]{3})"' "$ANALYZER_DIR/" -or '$1' --no-filename 2>/dev/null | sort -u)
id_count=${#diagnostic_ids[@]}

analyzer_count=$(rg -l 'class \w+ .* DiagnosticAnalyzer' "$ANALYZER_DIR/" --type cs 2>/dev/null | wc -l | tr -d ' ')
fixer_count=$(rg -l 'class \w+ .* CodeFixProvider' "$ANALYZER_DIR/" --type cs 2>/dev/null | wc -l | tr -d ' ')

pub_shipped=0
if [[ -f "$ANALYZER_DIR/PublicAPI.Shipped.txt" ]]; then
    pub_shipped=$(grep -cvE '^#|^$' "$ANALYZER_DIR/PublicAPI.Shipped.txt" 2>/dev/null || true)
    pub_shipped=${pub_shipped:-0}
fi

pub_unshipped=0
if [[ -f "$ANALYZER_DIR/PublicAPI.Unshipped.txt" ]]; then
    pub_unshipped=$(grep -cvE '^#|^$' "$ANALYZER_DIR/PublicAPI.Unshipped.txt" 2>/dev/null || true)
    pub_unshipped=${pub_unshipped:-0}
fi

source_files=$(fd -e cs . "$ANALYZER_DIR" --exclude obj --exclude bin 2>/dev/null | wc -l | tr -d ' ')
total_loc=$(fd -e cs . "$ANALYZER_DIR" --exclude obj --exclude bin -x wc -l {} \; 2>/dev/null | awk '{s+=$1} END {print s+0}')

mapfile -t largest < <(fd -e cs . "$ANALYZER_DIR" --exclude obj --exclude bin -x wc -l {} \; 2>/dev/null | sort -rn | head -5)

if [[ "$JSON" == true ]]; then
    ids_json=$(printf '%s\n' "${diagnostic_ids[@]}" | jq -R . | jq -s .)
    largest_json=$(printf '%s\n' "${largest[@]}" | awk '{print "{\"lines\":" $1 ",\"file\":\"" $2 "\"}"}' | jq -s .)
    jq -n \
        --argjson ids "$ids_json" \
        --argjson id_count "$id_count" \
        --argjson analyzers "$analyzer_count" \
        --argjson fixers "$fixer_count" \
        --argjson pub_shipped "$pub_shipped" \
        --argjson pub_unshipped "$pub_unshipped" \
        --argjson source_files "$source_files" \
        --argjson total_loc "$total_loc" \
        --argjson largest "$largest_json" \
        '{diagnostic_ids: $ids, id_count: $id_count, analyzer_classes: $analyzers, code_fix_providers: $fixers, public_api_shipped: $pub_shipped, public_api_unshipped: $pub_unshipped, source_files: $source_files, total_loc: $total_loc, largest_files: $largest}'
else
    printf "${BOLD}E128.Analyzers Statistics${RESET}\n"
    printf "  Diagnostic IDs:     %d\n" "$id_count"
    printf "  Analyzer classes:   %d\n" "$analyzer_count"
    printf "  Code fix providers: %d\n" "$fixer_count"
    printf "  PublicAPI shipped:  %d entries\n" "$pub_shipped"
    printf "  PublicAPI unshipped:%d entries\n" "$pub_unshipped"
    printf "  Source files:       %d\n" "$source_files"
    printf "  Total LOC:          %d\n" "$total_loc"

    if [[ ${#largest[@]} -gt 0 ]]; then
        printf "\n${BOLD}Largest files:${RESET}\n"
        for entry in "${largest[@]}"; do
            printf "  %s\n" "$entry"
        done
    fi

    if [[ ${#diagnostic_ids[@]} -gt 0 ]]; then
        printf "\n${BOLD}Diagnostic IDs:${RESET}\n"
        printf "  %s\n" "${diagnostic_ids[@]}"
    fi
fi
