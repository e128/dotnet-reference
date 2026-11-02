#!/usr/bin/env bash
# Analyzer project context: version, rules, fix providers, and public API surface.
# Usage: analyzer-context.sh [--json] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
ANALYZER_DIR="$ROOT/src/E128.Analyzers"
FIXER_DIR="$ROOT/src/E128.Analyzers.CodeFixers"
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

# A CodeFixProvider declares its targets through FixableDiagnosticIds, which
# names an analyzer class. Resolve each named class to the diagnostic IDs
# declared in that class's file. Two rg passes replace one full tree scan per
# class name.
mapfile -t analyzer_files < <(fd -e cs . "$ANALYZER_DIR" --type f 2>/dev/null || true)
mapfile -t provider_files < <(fd 'CodeFixProvider\.cs$' "$FIXER_DIR" 2>/dev/null || true)

declare -A file_class=() class_ids=() covered_ids=()
if (( ${#analyzer_files[@]} > 0 )); then
    # Match only a real declaration. A doc comment can hold the words
    # "class level", and that must not win the per-file race below.
    while IFS=: read -r src_file src_class; do
        if [[ -n "$src_file" && -z "${file_class[$src_file]:-}" ]]; then
            file_class["$src_file"]="$src_class"
        fi
    done < <(rg -Ho '\b(?:public|internal)\s+(?:(?:sealed|static|abstract|partial)\s+)*class\s+(\w+)' \
        -r '$1' "${analyzer_files[@]}" 2>/dev/null || true)

    while IFS=: read -r src_file src_id; do
        src_class="${file_class[$src_file]:-}"
        if [[ -n "$src_class" ]]; then
            class_ids["$src_class"]="${class_ids[$src_class]:-}${class_ids[$src_class]:+ }${src_id}"
        fi
    done < <(rg -Ho 'DiagnosticId\s*=\s*"(E128[0-9]{3})"' -r '$1' "${analyzer_files[@]}" 2>/dev/null || true)
fi

if (( ${#provider_files[@]} > 0 )); then
    while IFS=: read -r _provider_file ref_class; do
        for ref_id in ${class_ids[$ref_class]:-}; do
            covered_ids["$ref_id"]=1
        done
    done < <(rg -Ho '\[(\w+)\.DiagnosticId\]' -r '$1' "${provider_files[@]}" 2>/dev/null || true)
fi

if (( ${#covered_ids[@]} > 0 )); then
    mapfile -t fix_providers < <(printf '%s\n' "${!covered_ids[@]}" | sort -u)
else
    fix_providers=()
fi

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
