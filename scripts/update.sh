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
PROPS="$ROOT/Directory.Packages.props"

if [[ "$APPLY" == true ]]; then
    # dotnet-outdated rewrites the central PackageVersion lines under CPM.
    # Lock the major version so it does not cross a .NET major, and revert the
    # file when the new graph does not restore.
    if [[ ! -f "$PROPS" ]]; then
        err "Central package file not found: $PROPS"
        exit 1
    fi
    backup="$(mktemp "${TMPDIR:-/tmp}/dp-backup.XXXXXX")"
    cp "$PROPS" "$backup"
    info "Updating outdated NuGet packages (version lock: major)..."
    if ! dotnet outdated "$SOLUTION" --upgrade --version-lock Major 2>&1; then
        cp "$backup" "$PROPS"
        rm -f "$backup"
        err "Package update failed. Reverted $PROPS."
        exit 1
    fi
    if dotnet restore "$SOLUTION" >/dev/null 2>&1; then
        # dotnet-outdated drops the trailing newline when it rewrites the file.
        if [[ -s "$PROPS" && "$(tail -c 1 "$PROPS")" != "" ]]; then
            printf '\n' >> "$PROPS"
        fi
        info "Restore passed. Central versions updated."
        rm -f "$backup"
    else
        cp "$backup" "$PROPS"
        rm -f "$backup"
        err "Restore failed after update. Reverted $PROPS."
        exit 1
    fi
else
    if [[ "$JSON" == true ]]; then
        output=$(dotnet outdated "$SOLUTION" 2>&1 || true)
        outdated_count=$(printf '%s\n' "$output" | awk '/ -> /{print $1}' | sort -u | wc -l)
        outdated_count="${outdated_count// /}"
        json_object status=ok outdated="$outdated_count"
    else
        info "Checking NuGet packages..."
        if ! output=$(dotnet outdated "$SOLUTION" 2>&1); then
            err "dotnet outdated failed. Install it with: dotnet tool install -g dotnet-outdated-tool"
            printf '%s\n' "$output"
            exit 1
        fi
        printf '%s\n' "$output"
    fi
fi

# ── GitHub Actions ───────────────────────────────
if [[ "$ACTIONS" == true ]]; then
    echo
    info "Checking GitHub Actions versions..."
    WORKFLOW_DIR="$ROOT/.github/workflows"
    if [[ -d "$WORKFLOW_DIR" ]]; then
        # Extract uses: lines and show current versions
        rg -o 'uses:\s*\S+' "$WORKFLOW_DIR" --no-heading --no-filename 2>/dev/null | while IFS= read -r line; do
            action="${line#uses:}"
            action="${action# }"
            if [[ -n "$action" ]]; then
                printf "  %s\n" "$action"
            fi
        done
        dim "Check https://github.com/actions for latest versions"
    else
        dim "No .github/workflows/ directory found"
    fi
fi
