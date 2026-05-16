#!/usr/bin/env bash
# Analyzer project context: version, rules, fix providers, and public API surface.
# Usage: analyzer-context.sh [--json] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
ANALYZER_DIR="$ROOT/src/E128.Analyzers"
JSON=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json) JSON=true ;;
        --help)
            echo "Usage: analyzer-context.sh [--json] [--help]"
            echo "  Emit analyzer project context: version, diagnostic IDs, unshipped rules,"
            echo "  fix provider coverage, and public API delta."
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

version=$(grep -m1 '<Version>' "$ANALYZER_DIR/E128.Analyzers.csproj" | sed 's/.*<Version>//;s/<.*//' | tr -d ' ')

mapfile -t diagnostic_ids < <(rg 'DiagnosticId\s*=\s*"(E128[0-9]{3})"' "$ROOT/src/" -or '$1' --no-filename 2>/dev/null | sort -u)

unshipped_count=0
unshipped_rules=""
if [[ -f "$ANALYZER_DIR/AnalyzerReleases.Unshipped.md" ]]; then
    unshipped_rules=$(grep -E 'E128[0-9]{3}' "$ANALYZER_DIR/AnalyzerReleases.Unshipped.md" 2>/dev/null | grep -v '^-' || true)
    if [[ -n "$unshipped_rules" ]]; then
        unshipped_count=$(echo "$unshipped_rules" | wc -l | tr -d ' ')
    fi
fi

shipped_count=0
if [[ -f "$ANALYZER_DIR/AnalyzerReleases.Shipped.md" ]]; then
    shipped_count=$(grep -cE 'E128[0-9]{3}' "$ANALYZER_DIR/AnalyzerReleases.Shipped.md" 2>/dev/null || true)
    shipped_count=${shipped_count:-0}
fi

mapfile -t fix_providers < <(fd 'CodeFixProvider\.cs$' "$ANALYZER_DIR" 2>/dev/null | while read -r fp; do
    # CodeFixProviders reference analyzer classes via FixableDiagnosticIds.
    # Extract referenced analyzer class names, then resolve their DiagnosticId.
    rg '\[(\w+)\.DiagnosticId\]' "$fp" -or '$1' --no-filename 2>/dev/null | while read -r cls; do
        rg "class $cls" "$ANALYZER_DIR" -l --no-filename 2>/dev/null | head -1 | while read -r analyzer_file; do
            rg 'DiagnosticId\s*=\s*"(E128[0-9]{3})"' "$analyzer_file" -or '$1' --no-filename 2>/dev/null
        done
    done
done | sort -u)

pub_unshipped=0
if [[ -f "$ANALYZER_DIR/PublicAPI.Unshipped.txt" ]]; then
    pub_unshipped=$(grep -cvE '^#|^$' "$ANALYZER_DIR/PublicAPI.Unshipped.txt" 2>/dev/null || true)
    pub_unshipped=${pub_unshipped:-0}
fi

if [[ "$JSON" == true ]]; then
    ids_json=$(printf '%s\n' "${diagnostic_ids[@]}" | jq -R . | jq -s .)
    fix_json=$(printf '%s\n' "${fix_providers[@]}" | jq -R . | jq -s .)
    jq -n \
        --arg version "$version" \
        --argjson ids "$ids_json" \
        --argjson shipped "$shipped_count" \
        --argjson unshipped "$unshipped_count" \
        --argjson fixes "$fix_json" \
        --argjson pub_unshipped "$pub_unshipped" \
        '{version: $version, diagnostic_ids: $ids, shipped_rules: $shipped, unshipped_rules: $unshipped, fix_provider_ids: $fixes, public_api_unshipped: $pub_unshipped}'
else
    printf "${BOLD}E128.Analyzers Context${RESET}\n"
    printf "  Version:            %s\n" "$version"
    printf "  Diagnostic IDs:     %d total\n" "${#diagnostic_ids[@]}"
    printf "  Shipped rules:      %d\n" "$shipped_count"
    printf "  Unshipped rules:    %d\n" "$unshipped_count"
    printf "  Fix providers:      %d IDs covered\n" "${#fix_providers[@]}"
    printf "  Public API delta:   %d unshipped entries\n" "$pub_unshipped"

    if [[ ${#diagnostic_ids[@]} -gt 0 ]]; then
        printf "\n${BOLD}Diagnostic IDs:${RESET}\n"
        printf "  %s\n" "${diagnostic_ids[@]}"
    fi

    if [[ ${#fix_providers[@]} -gt 0 ]]; then
        printf "\n${BOLD}IDs with fix providers:${RESET}\n"
        printf "  %s\n" "${fix_providers[@]}"
    fi

    ids_without_fix=()
    for id in "${diagnostic_ids[@]}"; do
        has_fix=false
        for fid in "${fix_providers[@]}"; do
            if [[ "$id" == "$fid" ]]; then has_fix=true; break; fi
        done
        if [[ "$has_fix" == false ]]; then ids_without_fix+=("$id"); fi
    done

    if [[ ${#ids_without_fix[@]} -gt 0 ]]; then
        printf "\n${BOLD}IDs without fix providers:${RESET}\n"
        printf "  %s\n" "${ids_without_fix[@]}"
    fi

    if [[ -n "$unshipped_rules" ]]; then
        printf "\n${BOLD}Unshipped rules:${RESET}\n"
        echo "$unshipped_rules"
    fi
fi
