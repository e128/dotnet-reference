#!/usr/bin/env bash
# Move the Unshipped rule rows into a new release section in Shipped.md.
# Usage: analyzer-promote.sh [--project NAME] [--version X.Y.Z] [--dry-run] [--json]
# Example: analyzer-promote.sh
# Example: analyzer-promote.sh --dry-run
#
# Run this after version-bump.sh and before the squash commit. The version
# defaults to the <Version> in the project .csproj. The New Rules rows and the
# Changed Rules rows leave Unshipped.md and join a `## Release <version>`
# section at the top of Shipped.md. Unshipped.md keeps its table headers.
#
# The script stops when Shipped.md already carries the same version at the top.
# A second release needs a new version, so bump first.
source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

set -euo pipefail

PROJECT="E128.Analyzers"
VERSION=""
DRY_RUN=false
JSON_MODE=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --project) PROJECT="${2:-}"; shift 2 ;;
        --version) VERSION="${2:-}"; shift 2 ;;
        --dry-run) DRY_RUN=true; shift ;;
        --json) JSON_MODE=true; shift ;;
        --help|-h)
            echo "Usage: analyzer-promote.sh [--project NAME] [--version X.Y.Z] [--dry-run] [--json]"
            exit 0
            ;;
        *) err "Unknown argument: $1"; exit 1 ;;
    esac
done

ROOT="$(find_repo_root)"
PROJECT_DIR="$ROOT/src/$PROJECT"
SHIPPED="$PROJECT_DIR/AnalyzerReleases.Shipped.md"
UNSHIPPED="$PROJECT_DIR/AnalyzerReleases.Unshipped.md"

if [[ ! -f "$SHIPPED" || ! -f "$UNSHIPPED" ]]; then
    if $JSON_MODE; then
        printf '{"status":"skip","reason":"no analyzer release files"}\n'
    else
        dim "No analyzer release files under src/$PROJECT — skipping"
    fi
    exit 0
fi

if [[ -z "$VERSION" ]]; then
    CSPROJ="$PROJECT_DIR/$PROJECT.csproj"
    [[ -f "$CSPROJ" ]] || CSPROJ=$(fd -e csproj . "$PROJECT_DIR" --max-depth 1 | head -1)
    VERSION=$(rg -o '<Version>[^<]+</Version>' "$CSPROJ" --no-filename | head -1 | sd '<Version>|</Version>' '')
fi

if [[ -z "$VERSION" ]]; then
    err "No version found. Pass --version X.Y.Z."
    exit 1
fi

if [[ "$(head -1 "$SHIPPED")" == "## Release $VERSION" ]]; then
    err "Shipped.md already has a release section for $VERSION."
    err "Run scripts/internal/version-bump.sh first, then promote again."
    exit 1
fi

# Row lines of one `### <name>` section. A release heading ends the block.
rows_of() {
    local file="$1" name="$2"
    awk -v want="### $name" '
        /^## Release/ { inside = 0; next }
        /^### / { inside = ($0 == want); next }
        inside && /^E128[0-9][0-9][0-9][[:space:]]*\|/ { print }
    ' "$file"
}

table_header() {
    case "$1" in
        "New Rules") printf 'Rule ID | Category | Severity | Notes\n--------|----------|----------|------\n' ;;
        "Changed Rules") printf 'Rule ID | New Category | New Severity | Old Category | Old Severity | Notes\n--------|--------------|--------------|--------------|--------------|-------\n' ;;
        "Removed Rules") printf 'Rule ID | Category | Severity | Notes\n--------|----------|----------|------\n' ;;
        *) return 0 ;;
    esac
}

NEW_ROWS=$(rows_of "$UNSHIPPED" "New Rules")
CHANGED_ROWS=$(rows_of "$UNSHIPPED" "Changed Rules")
REMOVED_ROWS=$(rows_of "$UNSHIPPED" "Removed Rules")

if [[ -z "$NEW_ROWS" && -z "$CHANGED_ROWS" && -z "$REMOVED_ROWS" ]]; then
    if $JSON_MODE; then
        printf '{"status":"empty","version":"%s","new":0,"changed":0}\n' "$VERSION"
    else
        ok "Unshipped.md holds no rule rows — nothing to promote"
    fi
    exit 0
fi

NEW_COUNT=$(printf '%s\n' "$NEW_ROWS" | rg -c '^E128' || echo 0)
CHANGED_COUNT=$(printf '%s\n' "$CHANGED_ROWS" | rg -c '^E128' || echo 0)

if $DRY_RUN; then
    if $JSON_MODE; then
        printf '{"status":"dry-run","version":"%s","new":%s,"changed":%s,"removed":%s}\n' \
            "$VERSION" "${NEW_COUNT:-0}" "${CHANGED_COUNT:-0}" "$([[ -n "$REMOVED_ROWS" ]] && echo 1 || echo 0)"
    else
        info "Would move to release $VERSION: ${NEW_COUNT:-0} new, ${CHANGED_COUNT:-0} changed"
        [[ -n "$REMOVED_ROWS" ]] && warn "Removed Rules rows stay in Unshipped.md and need a hand-written release note"
    fi
    exit 0
fi

WORK=$(mktemp -d "$ROOT/.claude/tmp/release-promote.XXXXXX")
trap 'rm -rf "$WORK"' EXIT

{
    printf '## Release %s\n\n' "$VERSION"
    if [[ -n "$NEW_ROWS" ]]; then
        printf '### New Rules\n\n'
        table_header "New Rules"
        printf '%s\n\n' "$NEW_ROWS"
    fi
    if [[ -n "$CHANGED_ROWS" ]]; then
        printf '### Changed Rules\n\n'
        table_header "Changed Rules"
        printf '%s\n\n' "$CHANGED_ROWS"
    fi
    cat "$SHIPPED"
} > "$WORK/shipped.md"

{
    if rg -q '^### New Rules' "$UNSHIPPED"; then
        printf '### New Rules\n\n'
        table_header "New Rules"
        printf '\n'
    fi
    if rg -q '^### Changed Rules' "$UNSHIPPED"; then
        printf '### Changed Rules\n\n'
        table_header "Changed Rules"
        printf '\n'
    fi
    if [[ -n "$REMOVED_ROWS" ]]; then
        printf '### Removed Rules\n\n'
        table_header "Removed Rules"
        printf '%s\n' "$REMOVED_ROWS"
    fi
} > "$WORK/unshipped.md"

mv "$WORK/shipped.md" "$SHIPPED"
mv "$WORK/unshipped.md" "$UNSHIPPED"

if $JSON_MODE; then
    printf '{"status":"promoted","version":"%s","new":%s,"changed":%s}\n' \
        "$VERSION" "${NEW_COUNT:-0}" "${CHANGED_COUNT:-0}"
else
    ok "Promoted ${NEW_COUNT:-0} new and ${CHANGED_COUNT:-0} changed rules into release $VERSION"
    [[ -n "$REMOVED_ROWS" ]] && warn "Removed Rules rows stay in Unshipped.md and need a hand-written release note"
fi
