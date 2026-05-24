#!/usr/bin/env bash
# Validate analyzer release files (Unshipped/Shipped) against source DiagnosticId constants.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

JSON_MODE=false
for arg in "$@"; do
    case "$arg" in
        --json) JSON_MODE=true ;;
        --help|-h)
            echo "Usage: analyzer-release-check.sh [--json]"
            echo "Reports: missing Unshipped entries, duplicates, orphans."
            exit 0
            ;;
    esac
done

ROOT="$(find_repo_root)"
ANALYZER_DIR="$ROOT/src/E128.Analyzers"
UNSHIPPED="$ANALYZER_DIR/AnalyzerReleases.Unshipped.md"
SHIPPED="$ANALYZER_DIR/AnalyzerReleases.Shipped.md"

if [[ ! -d "$ANALYZER_DIR" ]]; then
    if $JSON_MODE; then
        printf '{"status":"skip","reason":"no analyzer directory"}\n'
    else
        dim "No analyzer directory found — skipping"
    fi
    exit 0
fi

# 1. Extract all DiagnosticId constants from source
source_ids=()
while IFS= read -r id; do
    source_ids+=("$id")
done < <(rg -o '"E128\d{3}"' "$ANALYZER_DIR/" --no-filename 2>/dev/null | tr -d '"' | sort -u)

# 2. Extract IDs from Unshipped.md
unshipped_ids=()
if [[ -f "$UNSHIPPED" ]]; then
    while IFS= read -r id; do
        unshipped_ids+=("$id")
    done < <(rg -o 'E128\d{3}' "$UNSHIPPED" 2>/dev/null | sort -u)
fi

# 3. Extract IDs from Shipped.md
shipped_ids=()
if [[ -f "$SHIPPED" ]]; then
    while IFS= read -r id; do
        shipped_ids+=("$id")
    done < <(rg -o 'E128\d{3}' "$SHIPPED" 2>/dev/null | sort -u)
fi

# 4. Combine Unshipped + Shipped for comparison
declare -A all_released=()
for id in "${unshipped_ids[@]+"${unshipped_ids[@]}"}"; do
    all_released["$id"]="unshipped"
done
for id in "${shipped_ids[@]+"${shipped_ids[@]}"}"; do
    if [[ -v "all_released[$id]" ]]; then
        all_released["$id"]="both"
    else
        all_released["$id"]="shipped"
    fi
done

# 5. Find missing: in source but not in Unshipped or Shipped
missing=()
for id in "${source_ids[@]+"${source_ids[@]}"}"; do
    if [[ ! -v "all_released[$id]" ]]; then
        missing+=("$id")
    fi
done

# 6. Find duplicates: in both Unshipped AND Shipped
duplicates=()
for id in "${!all_released[@]}"; do
    if [[ "${all_released[$id]}" == "both" ]]; then
        duplicates+=("$id")
    fi
done

# 7. Find orphans: in Unshipped but not in source
orphans=()
declare -A source_set=()
for id in "${source_ids[@]+"${source_ids[@]}"}"; do
    source_set["$id"]=1
done
for id in "${unshipped_ids[@]+"${unshipped_ids[@]}"}"; do
    if [[ ! -v "source_set[$id]" ]]; then
        orphans+=("$id")
    fi
done

# Sort arrays for consistent output
if [[ ${#missing[@]} -gt 0 ]]; then
    mapfile -t missing < <(printf '%s\n' "${missing[@]}" | sort)
fi
if [[ ${#duplicates[@]} -gt 0 ]]; then
    mapfile -t duplicates < <(printf '%s\n' "${duplicates[@]}" | sort)
fi
if [[ ${#orphans[@]} -gt 0 ]]; then
    mapfile -t orphans < <(printf '%s\n' "${orphans[@]}" | sort)
fi

# 8. Report
has_issues=false
[[ ${#missing[@]} -gt 0 || ${#duplicates[@]} -gt 0 || ${#orphans[@]} -gt 0 ]] && has_issues=true

if $JSON_MODE; then
    to_json_array() {
        local first=true
        printf '['
        for item in "$@"; do
            [[ -z "$item" ]] && continue
            $first || printf ','
            printf '"%s"' "$item"
            first=false
        done
        printf ']'
    }

    printf '{"status":"%s","source_ids":%d,"unshipped_ids":%d,"shipped_ids":%d,' \
        "$( $has_issues && echo "issues" || echo "clean" )" \
        "${#source_ids[@]}" "${#unshipped_ids[@]}" "${#shipped_ids[@]}"
    printf '"missing":%s,' "$(to_json_array "${missing[@]+"${missing[@]}"}")"
    printf '"duplicates":%s,' "$(to_json_array "${duplicates[@]+"${duplicates[@]}"}")"
    printf '"orphans":%s}\n' "$(to_json_array "${orphans[@]+"${orphans[@]}"}")"
else
    info "Source IDs: ${#source_ids[@]}, Unshipped: ${#unshipped_ids[@]}, Shipped: ${#shipped_ids[@]}"

    if [[ ${#missing[@]} -gt 0 ]]; then
        err "Missing from release files (in source, not in Unshipped or Shipped):"
        for id in "${missing[@]}"; do
            printf "  %s\n" "$id"
        done
    fi

    if [[ ${#duplicates[@]} -gt 0 ]]; then
        warn "Duplicates (in both Unshipped AND Shipped — remove from Unshipped):"
        for id in "${duplicates[@]}"; do
            printf "  %s\n" "$id"
        done
    fi

    if [[ ${#orphans[@]} -gt 0 ]]; then
        warn "Orphans (in Unshipped but no source defines them):"
        for id in "${orphans[@]}"; do
            printf "  %s\n" "$id"
        done
    fi

    if ! $has_issues; then
        ok "Analyzer release files are consistent"
    fi
fi

$has_issues && exit 1 || exit 0
