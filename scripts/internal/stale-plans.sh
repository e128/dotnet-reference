#!/usr/bin/env bash
# List plan directories older than N days with no recent modifications.
# Usage: stale-plans.sh [--days N] [--json]
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

DAYS=30
JSON=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --days) DAYS="$2"; shift 2 ;;
        --json) JSON=true; shift ;;
        *) err "Unknown flag: $1"; exit 1 ;;
    esac
done

ROOT="$(find_repo_root)"
PLANS_DIR="$ROOT/plans"

if [[ ! -d "$PLANS_DIR" ]]; then
    if [[ "$JSON" == true ]]; then
        printf '{"days":%d,"stale":[]}\n' "$DAYS"
    else
        warn "No plans/ directory found"
    fi
    exit 0
fi

CUTOFF=$(date -v-"${DAYS}"d +%s 2>/dev/null || date -d "${DAYS} days ago" +%s)
stale=()

shopt -s nullglob
for plan_dir in "$PLANS_DIR"/*/; do
    [[ ! -d "$plan_dir" ]] && continue
    name="$(basename "$plan_dir")"

    # Skip non-plan files at root level
    [[ "$name" == "tmp" ]] && continue

    # Find most recent modification time across all files in the plan dir
    latest=0
    while IFS= read -r file; do
        mtime=$(stat -f %m "$file" 2>/dev/null || stat -c %Y "$file" 2>/dev/null || echo 0)
        if [[ "$mtime" -gt "$latest" ]]; then
            latest="$mtime"
        fi
    done < <(find "$plan_dir" -type f 2>/dev/null)

    if [[ "$latest" -gt 0 && "$latest" -lt "$CUTOFF" ]]; then
        age_days=$(( ($(date +%s) - latest) / 86400 ))
        stale+=("$name|$age_days")
    fi
done

# Also check standalone .md files in plans/ (not in subdirectories)
for plan_file in "$PLANS_DIR"/*.md; do
    [[ ! -f "$plan_file" ]] && continue
    name="$(basename "$plan_file")"
    mtime=$(stat -f %m "$plan_file" 2>/dev/null || stat -c %Y "$plan_file" 2>/dev/null || echo 0)
    if [[ "$mtime" -gt 0 && "$mtime" -lt "$CUTOFF" ]]; then
        age_days=$(( ($(date +%s) - mtime) / 86400 ))
        stale+=("$name|$age_days")
    fi
done

if [[ "$JSON" == true ]]; then
    printf '{"days":%d,"stale":[' "$DAYS"
    first=true
    for entry in "${stale[@]}"; do
        IFS='|' read -r name age <<< "$entry"
        if [[ "$first" == true ]]; then first=false; else printf ','; fi
        printf '{"name":"%s","age_days":%s}' "$name" "$age"
    done
    printf ']}\n'
else
    if [[ ${#stale[@]} -eq 0 ]]; then
        ok "No stale plans (threshold: ${DAYS} days)"
    else
        printf "${BOLD}Stale Plans (>%d days)${RESET}\n\n" "$DAYS"
        printf "%-40s %s\n" "Plan" "Age (days)"
        printf "%-40s %s\n" "----------------------------------------" "----------"
        for entry in "${stale[@]}"; do
            IFS='|' read -r name age <<< "$entry"
            printf "%-40s %s\n" "$name" "$age"
        done
    fi
fi
