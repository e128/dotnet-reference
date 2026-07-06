#!/usr/bin/env bash
# Check GitHub Actions versions and detect outdated or un-pinned actions.
# Usage: gh-actions-update.sh [--json]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
JSON=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json) JSON=true ;;
        *)      err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

WORKFLOW_DIR="$ROOT/.github/workflows"

if [[ ! -d "$WORKFLOW_DIR" ]]; then
    [[ "$JSON" == true ]] && json_object status=ok actions=0 || dim "No .github/workflows/ directory"
    exit 0
fi

# Latest major version, queried live from GitHub (memoized per run).
# ponytail: gh api over a hardcoded table — the table drifted and flagged
# up-to-date pins as behind. Returns "" if gh is unavailable/offline.
declare -A LATEST_CACHE
get_latest_major() {
    local action="$1"
    if [[ -n "${LATEST_CACHE[$action]+x}" ]]; then
        echo "${LATEST_CACHE[$action]}"; return
    fi
    local tag=""
    command -v gh >/dev/null 2>&1 && \
        tag=$(gh api "repos/$action/releases/latest" --jq .tag_name 2>/dev/null)
    local major=""
    [[ -n "$tag" ]] && major=$(echo "$tag" | sed -n 's/\(v[0-9]*\).*/\1/p')
    LATEST_CACHE[$action]="$major"
    echo "$major"
}

# True when major $1 is strictly older than major $2 (e.g. v6 behind v7).
is_behind() {
    local a="${1#v}" b="${2#v}"
    [[ "$a" =~ ^[0-9]+$ && "$b" =~ ^[0-9]+$ ]] && (( a < b ))
}

TOTAL=0
MAJOR_BEHIND=()
UNPINNED=()

for workflow in "$WORKFLOW_DIR"/*.yml "$WORKFLOW_DIR"/*.yaml; do
    [[ ! -f "$workflow" ]] && continue
    rel="${workflow#$ROOT/}"

    while IFS= read -r line; do
        # Parse: uses: owner/repo@ref  # optional comment
        action=$(echo "$line" | sed -n 's/.*uses:[[:space:]]*\([^@[:space:]]*\)@.*/\1/p')
        ref=$(echo "$line" | sed -n 's/.*@\([^[:space:]#]*\).*/\1/p')
        comment=$(echo "$line" | sed -n 's/.*#[[:space:]]*\(.*\)/\1/p')
        [[ -z "$action" || -z "$ref" ]] && continue
        TOTAL=$((TOTAL + 1))

        latest_major=$(get_latest_major "$action")
        ref_len=${#ref}

        if [[ $ref_len -eq 40 ]] && echo "$ref" | grep -qE '^[a-f0-9]+$'; then
            # SHA-pinned — extract version from comment
            pinned_version=$(echo "$comment" | sed -n 's/.*\(v[0-9][0-9.]*\).*/\1/p')
            pinned_major=$(echo "$pinned_version" | sed -n 's/\(v[0-9]*\).*/\1/p')

            if [[ -n "$latest_major" && -n "$pinned_major" ]] && is_behind "$pinned_major" "$latest_major"; then
                MAJOR_BEHIND+=("$rel|$action|${pinned_version:-unknown}|$latest_major")
            fi
        else
            # Tag ref
            ref_major=$(echo "$ref" | sed -n 's/\(v[0-9]*\).*/\1/p')

            if [[ -n "$latest_major" && -n "$ref_major" ]] && is_behind "$ref_major" "$latest_major"; then
                MAJOR_BEHIND+=("$rel|$action|$ref|$latest_major")
            fi

            UNPINNED+=("$rel|$action|$ref")
        fi
    done < <(grep 'uses:' "$workflow" 2>/dev/null)
done

if [[ "$JSON" == true ]]; then
    behind=${#MAJOR_BEHIND[@]}
    unpinned=${#UNPINNED[@]}
    printf '{"status":"%s","actions":%d,"major_behind":%d,"unpinned":%d}\n' \
        "$([ $behind -eq 0 ] && [ $unpinned -eq 0 ] && echo ok || echo findings)" \
        "$TOTAL" "$behind" "$unpinned"
else
    if [[ ${#MAJOR_BEHIND[@]} -eq 0 && ${#UNPINNED[@]} -eq 0 ]]; then
        ok "All $TOTAL actions are up to date and SHA-pinned"
    else
        if [[ ${#MAJOR_BEHIND[@]} -gt 0 ]]; then
            warn "${#MAJOR_BEHIND[@]} action(s) behind latest major version:"
            echo
            printf "  ${BOLD}%-35s %-12s %-12s %s${RESET}\n" "Action" "Current" "Latest" "File"
            for item in "${MAJOR_BEHIND[@]}"; do
                IFS='|' read -r file action current latest <<< "$item"
                printf "  %-35s %-12s %-12s %s\n" "$action" "$current" "$latest" "$file"
            done
        fi

        if [[ ${#UNPINNED[@]} -gt 0 ]]; then
            echo
            warn "${#UNPINNED[@]} action(s) not SHA-pinned:"
            echo
            printf "  ${BOLD}%-35s %-12s %s${RESET}\n" "Action" "Current" "File"
            for item in "${UNPINNED[@]}"; do
                IFS='|' read -r file action current <<< "$item"
                printf "  %-35s %-12s %s\n" "$action" "$current" "$file"
            done
            echo
            dim "Consider SHA-pinning for supply chain security"
        fi
    fi
fi
