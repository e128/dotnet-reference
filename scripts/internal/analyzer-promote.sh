#!/usr/bin/env bash
# Move the Unshipped rule rows into a new release section in Shipped.md.
# Usage: analyzer-promote.sh [--project NAME] [--version X.Y.Z] [--dry-run] [--json]
# Example: analyzer-promote.sh
# Example: analyzer-promote.sh --dry-run
#
# Run this after version-bump.sh and before the squash commit. The version
# defaults to the <Version> in the project .csproj. The New Rules rows and the
# Changed Rules rows leave Unshipped.md and join a `## Release <version>`
# section at the top of Shipped.md. Unshipped.md keeps its table headers and
# every line this script does not promote.
#
# The script stops when Shipped.md already carries the same version heading.
# A second release needs a new version, so bump first. The script also stops
# when Unshipped.md holds a non-blank line it cannot classify, so a hand-written
# note is never dropped.
#
# Both files change together. The two results are staged first, Unshipped.md
# moves first, and a failure on Shipped.md restores Unshipped.md.
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

# The project name comes from the command line and becomes a path segment.
# Allow a plain directory name only, then resolve the directory and require it
# to sit directly under src/, so a later move can never leave src/.
if [[ ! "$PROJECT" =~ ^[A-Za-z0-9._-]+$ || "$PROJECT" == "." || "$PROJECT" == ".." ]]; then
    err "Invalid project name: '$PROJECT'."
    err "Use only letters, digits, dot, underscore, or hyphen."
    exit 1
fi

SRC_DIR="$(cd "$ROOT/src" 2>/dev/null && pwd -P)" || SRC_DIR=""
if [[ -z "$SRC_DIR" ]]; then
    err "No src directory under $ROOT."
    exit 1
fi
PROJECT_DIR="$SRC_DIR/$PROJECT"
if [[ -d "$PROJECT_DIR" ]]; then
    PROJECT_DIR="$(cd "$PROJECT_DIR" && pwd -P)"
fi
if [[ "$(dirname "$PROJECT_DIR")" != "$SRC_DIR" ]]; then
    err "Project directory must be a direct child of $SRC_DIR: '$PROJECT'."
    exit 1
fi

SHIPPED="$PROJECT_DIR/AnalyzerReleases.Shipped.md"
UNSHIPPED="$PROJECT_DIR/AnalyzerReleases.Unshipped.md"

if [[ ! -f "$SHIPPED" || ! -f "$UNSHIPPED" ]]; then
    if $JSON_MODE; then
        json_object status=skip reason="no analyzer release files"
    else
        dim "No analyzer release files under src/$PROJECT: skipping"
    fi
    exit 0
fi

if [[ -z "$VERSION" ]]; then
    CSPROJ="$PROJECT_DIR/$PROJECT.csproj"
    [[ -f "$CSPROJ" ]] || CSPROJ=$(fd -e csproj . "$PROJECT_DIR" --max-depth 1 | head -1)
    # rg exits non-zero when the file holds no match. The `|| true` keeps the
    # emptiness check below reachable under pipefail.
    VERSION=$(rg -o '<Version>[^<]+</Version>' "$CSPROJ" --no-filename 2>/dev/null \
        | head -1 | sd '<Version>|</Version>' '' || true)
fi

if [[ -z "$VERSION" ]]; then
    err "No version found. Pass --version X.Y.Z."
    exit 1
fi

# The version reaches a markdown heading and a JSON string. Allow tag
# characters only, so it can carry no quote, newline, or path separator.
if [[ ! "$VERSION" =~ ^[A-Za-z0-9.+-]+$ ]]; then
    err "Invalid version: '$VERSION'."
    err "Use only letters, digits, dot, plus, or hyphen."
    exit 1
fi

# Match the exact heading line anywhere in the file, not only at the top.
if rg -qxF -- "## Release $VERSION" "$SHIPPED"; then
    err "Shipped.md already has a release section for $VERSION."
    err "Run scripts/internal/version-bump.sh first, then promote again."
    exit 1
fi

mkdir -p "$ROOT/.claude/tmp"
WORK=$(mktemp -d "$ROOT/.claude/tmp/release-promote.XXXXXX")
trap 'rm -rf "$WORK"' EXIT

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

: > "$WORK/unshipped.problems"

# Write the kept Unshipped.md to the scratch directory and list every
# non-blank line the script cannot classify. New Rules rows and Changed Rules
# rows leave. Headings, table headers, separators, blank lines, and Removed
# Rules rows stay. A heading or a row outside the three known sections is a
# problem, so a hand-written note is never dropped without a report.
awk -v problems="$WORK/unshipped.problems" '
    function is_row(line) { return line ~ /^E128[0-9][0-9][0-9][[:space:]]*\|/ }
    {
        line = $0
        if (line ~ /^[[:space:]]*$/) { print; next }
        if (line == "### New Rules" || line == "### Changed Rules" || line == "### Removed Rules") {
            section = line
            print
            next
        }
        if (line ~ /^### / || line ~ /^## Release/) {
            print NR ": " line >> problems
            next
        }
        if (section != "" && (line ~ /^Rule ID[[:space:]]*\|/ || line ~ /^-+[|][-|]*$/)) {
            print
            next
        }
        if (section != "" && is_row(line)) {
            if (section == "### New Rules" || section == "### Changed Rules") { next }
            print
            next
        }
        print NR ": " line >> problems
    }
' "$UNSHIPPED" > "$WORK/unshipped.md"

BAD_COUNT=$(awk 'END { print NR + 0 }' "$WORK/unshipped.problems")

if (( BAD_COUNT > 0 )); then
    if $JSON_MODE; then
        json_object status=blocked version="$VERSION" reason="unclassified lines in Unshipped.md"
    else
        err "Unshipped.md holds $BAD_COUNT line(s) this script cannot classify:"
        while IFS= read -r problem; do
            err "  $problem"
        done < "$WORK/unshipped.problems"
        err "Move each line into a known section, then rerun. Nothing was written."
    fi
    exit 1
fi

if [[ -z "$NEW_ROWS" && -z "$CHANGED_ROWS" && -z "$REMOVED_ROWS" ]]; then
    if $JSON_MODE; then
        json_object status=empty version="$VERSION" new=0 changed=0
    else
        ok "Unshipped.md holds no rule rows, nothing to promote"
    fi
    exit 0
fi

NEW_COUNT=$(printf '%s\n' "$NEW_ROWS" | rg -c '^E128' || echo 0)
CHANGED_COUNT=$(printf '%s\n' "$CHANGED_ROWS" | rg -c '^E128' || echo 0)
REMOVED_FLAG=$([[ -n "$REMOVED_ROWS" ]] && echo 1 || echo 0)

if $DRY_RUN; then
    if $JSON_MODE; then
        json_object status=dry-run version="$VERSION" new="${NEW_COUNT:-0}" \
            changed="${CHANGED_COUNT:-0}" removed="$REMOVED_FLAG"
    else
        info "Would move to release $VERSION: ${NEW_COUNT:-0} new, ${CHANGED_COUNT:-0} changed"
        if [[ -n "$REMOVED_ROWS" ]]; then
            warn "Removed Rules rows stay in Unshipped.md and need a hand-written release note"
        fi
    fi
    exit 0
fi

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

# Both results are staged. Unshipped.md moves first so a failure on the second
# move can restore it, and no row ever sits in both files.
cp -p "$UNSHIPPED" "$WORK/unshipped.orig"
mv "$WORK/unshipped.md" "$UNSHIPPED"
if ! mv "$WORK/shipped.md" "$SHIPPED"; then
    if mv "$WORK/unshipped.orig" "$UNSHIPPED"; then
        err "Failed to write $SHIPPED. Unshipped.md was restored."
    else
        trap - EXIT
        err "Failed to write $SHIPPED, and Unshipped.md could not be restored."
        err "The original Unshipped.md is at $WORK/unshipped.orig."
    fi
    exit 1
fi

if $JSON_MODE; then
    json_object status=promoted version="$VERSION" new="${NEW_COUNT:-0}" \
        changed="${CHANGED_COUNT:-0}"
else
    ok "Promoted ${NEW_COUNT:-0} new and ${CHANGED_COUNT:-0} changed rules into release $VERSION"
    if [[ -n "$REMOVED_ROWS" ]]; then
        warn "Removed Rules rows stay in Unshipped.md and need a hand-written release note"
    fi
fi
