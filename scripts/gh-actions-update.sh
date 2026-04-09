#!/usr/bin/env bash
# Check and update GitHub Actions versions in workflow files.
# Usage: gh-actions-update.sh [--apply] [--json]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
APPLY=false; JSON=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --apply) APPLY=true ;;
        --json)  JSON=true ;;
        *)       err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

WORKFLOW_DIR="$ROOT/.github/workflows"

if [[ ! -d "$WORKFLOW_DIR" ]]; then
    [[ "$JSON" == true ]] && json_object status=ok actions=0 outdated=0 || dim "No .github/workflows/ directory"
    exit 0
fi

# Known latest versions — simple lookup function (bash 3.2 compatible)
get_latest() {
    case "$1" in
        actions/checkout)              echo "v4" ;;
        actions/setup-dotnet)          echo "v4" ;;
        actions/setup-node)            echo "v4" ;;
        actions/setup-python)          echo "v5" ;;
        actions/setup-java)            echo "v4" ;;
        actions/setup-go)              echo "v5" ;;
        actions/upload-artifact)       echo "v4" ;;
        actions/download-artifact)     echo "v4" ;;
        actions/cache)                 echo "v4" ;;
        actions/github-script)         echo "v7" ;;
        docker/setup-buildx-action)    echo "v3" ;;
        docker/login-action)           echo "v3" ;;
        docker/build-push-action)      echo "v6" ;;
        docker/metadata-action)        echo "v5" ;;
        *)                             echo "" ;;
    esac
}

TOTAL=0; OUTDATED=0; UPDATES=()

for workflow in "$WORKFLOW_DIR"/*.yml "$WORKFLOW_DIR"/*.yaml; do
    [[ ! -f "$workflow" ]] && continue
    rel="${workflow#$ROOT/}"

    while IFS= read -r line; do
        # Parse "uses: owner/repo@version" — portable grep (no PCRE)
        action=$(echo "$line" | sed -n 's/.*uses:[[:space:]]*\([^@[:space:]]*\)@.*/\1/p')
        current=$(echo "$line" | sed -n 's/.*@\([^[:space:]]*\).*/\1/p')
        [[ -z "$action" || -z "$current" ]] && continue
        TOTAL=$((TOTAL + 1))

        latest=$(get_latest "$action")

        if [[ -n "$latest" && "$current" != "$latest" ]]; then
            OUTDATED=$((OUTDATED + 1))
            UPDATES+=("$rel|$action|$current|$latest")

            if [[ "$APPLY" == true ]]; then
                sed -i '' "s|${action}@${current}|${action}@${latest}|g" "$workflow"
            fi
        fi
    done < <(grep 'uses:' "$workflow" 2>/dev/null)
done

if [[ "$JSON" == true ]]; then
    printf '{"status":"%s","actions":%d,"outdated":%d' \
        "$([ $OUTDATED -eq 0 ] && echo ok || echo outdated)" "$TOTAL" "$OUTDATED"
    if [[ ${#UPDATES[@]} -gt 0 ]]; then
        printf ',"updates":['
        first=true
        for update in "${UPDATES[@]}"; do
            IFS='|' read -r file action current latest <<< "$update"
            [[ "$first" == true ]] && first=false || printf ','
            printf '{"file":"%s","action":"%s","current":"%s","latest":"%s"}' \
                "$file" "$action" "$current" "$latest"
        done
        printf ']'
    fi
    printf '}\n'
else
    if [[ $OUTDATED -eq 0 ]]; then
        ok "All $TOTAL actions are up to date"
    else
        if [[ "$APPLY" == true ]]; then
            ok "Updated $OUTDATED action(s):"
        else
            warn "$OUTDATED outdated action(s) found:"
        fi
        printf "\n"
        printf "  ${BOLD}%-40s %-12s %-12s %s${RESET}\n" "Action" "Current" "Latest" "File"
        for update in "${UPDATES[@]}"; do
            IFS='|' read -r file action current latest <<< "$update"
            printf "  %-40s %-12s %-12s %s\n" "$action" "$current" "$latest" "$file"
        done
        if [[ "$APPLY" == false ]]; then
            echo
            dim "Run with --apply to update automatically"
        fi
    fi
fi
