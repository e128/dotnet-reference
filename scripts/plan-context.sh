#!/usr/bin/env bash
# Plan context: list active plans, roadmap items, or plan details.
# Usage: plan-context.sh [--active-only] [--roadmap-only] [--json]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
ACTIVE_ONLY=false; ROADMAP_ONLY=false; JSON=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --active-only)  ACTIVE_ONLY=true ;;
        --roadmap-only) ROADMAP_ONLY=true ;;
        --json)         JSON=true ;;
        *)              err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

PLANS_DIR="$ROOT/plans"

if [[ "$ROADMAP_ONLY" == true ]]; then
    ROADMAP="$PLANS_DIR/roadmap.md"
    if [[ -f "$ROADMAP" ]]; then
        # Extract ### headings as roadmap items
        if [[ "$JSON" == true ]]; then
            items=$(grep '^### ' "$ROADMAP" 2>/dev/null | sed 's/^### //' | jq -R . | jq -s .)
            echo "$items"
        else
            grep '^### ' "$ROADMAP" 2>/dev/null | sed 's/^### /  - /' || dim "No roadmap items"
        fi
    else
        [[ "$JSON" == true ]] && echo '[]' || dim "No roadmap.md found"
    fi
    exit 0
fi

# Find active plans (directories under plans/ with a *-tasks.md file)
ACTIVE=()
if [[ -d "$PLANS_DIR" ]]; then
    while IFS= read -r tasks_file; do
        [[ -z "$tasks_file" ]] && continue
        plan_dir="$(dirname "$tasks_file")"
        plan_name="$(basename "$plan_dir")"
        [[ "$plan_name" == "plans" ]] && continue
        ACTIVE+=("$plan_name")
    done < <(fd -e md -p 'tasks\.md$' "$PLANS_DIR" 2>/dev/null)
fi

if [[ "$JSON" == true ]]; then
    if [[ "$ACTIVE_ONLY" == true ]]; then
        printf '%s\n' "${ACTIVE[@]}" | jq -R . | jq -s .
    else
        # Full context: plan names + progress hints
        printf '['
        first=true
        for plan in "${ACTIVE[@]}"; do
            [[ "$first" == true ]] && first=false || printf ','
            total=$(grep -c '^\- \[' "$PLANS_DIR/$plan/$plan-tasks.md" 2>/dev/null || echo 0)
            done_count=$(grep -c '^\- \[x\]' "$PLANS_DIR/$plan/$plan-tasks.md" 2>/dev/null || echo 0)
            printf '{"name":"%s","total":%d,"done":%d}' "$plan" "$total" "$done_count"
        done
        printf ']\n'
    fi
else
    if [[ ${#ACTIVE[@]} -eq 0 ]]; then
        dim "No active plans"
    else
        printf "${BOLD}Active plans:${RESET}\n"
        for plan in "${ACTIVE[@]}"; do
            total=$(grep -c '^\- \[' "$PLANS_DIR/$plan/$plan-tasks.md" 2>/dev/null || echo 0)
            done_count=$(grep -c '^\- \[x\]' "$PLANS_DIR/$plan/$plan-tasks.md" 2>/dev/null || echo 0)
            printf "  %s (%d/%d tasks)\n" "$plan" "$done_count" "$total"
        done
    fi
fi
