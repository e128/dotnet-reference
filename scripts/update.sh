#!/usr/bin/env bash
# Check for outdated NuGet packages and optionally GitHub Actions versions.
# Usage: update.sh [--apply] [--actions] [--json]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
APPLY=false; JSON=false; ACTIONS=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --apply)   APPLY=true ;;
        --json)    JSON=true ;;
        --actions) ACTIONS=true ;;
        *)         err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

SOLUTION="$(find_solution)"

# ── NuGet packages ───────────────────────────────
if [[ "$APPLY" == true ]]; then
    info "Updating outdated NuGet packages..."
    dotnet outdated "$SOLUTION" --upgrade 2>&1 || warn "dotnet-outdated not installed — run: dotnet tool install -g dotnet-outdated-tool"
else
    if [[ "$JSON" == true ]]; then
        output=$(dotnet outdated "$SOLUTION" 2>&1 || true)
        outdated_count=$(echo "$output" | grep -c '→' || true)
        json_object status=ok outdated="$outdated_count"
    else
        info "Checking NuGet packages..."
        dotnet outdated "$SOLUTION" 2>&1 || warn "dotnet-outdated not installed — run: dotnet tool install -g dotnet-outdated-tool"
    fi
fi

# ── GitHub Actions ───────────────────────────────
if [[ "$ACTIONS" == true ]]; then
    echo
    info "Checking GitHub Actions versions..."
    WORKFLOW_DIR="$ROOT/.github/workflows"
    if [[ -d "$WORKFLOW_DIR" ]]; then
        # Extract uses: lines and show current versions
        rg "uses:" "$WORKFLOW_DIR" --no-heading 2>/dev/null | while IFS= read -r line; do
            action=$(echo "$line" | grep -oP 'uses: \K\S+' || true)
            if [[ -n "$action" ]]; then
                printf "  %s\n" "$action"
            fi
        done
        dim "Check https://github.com/actions for latest versions"
    else
        dim "No .github/workflows/ directory found"
    fi
fi
