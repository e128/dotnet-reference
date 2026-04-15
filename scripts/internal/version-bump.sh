#!/usr/bin/env bash
# Increment the <Version> in a project's .csproj file (patch version).
# Usage: version-bump.sh <ProjectName>
# Example: version-bump.sh E128.Analyzers
#
# Increments 1.17.0 → 1.17.1, 2.0.3 → 2.0.4, etc.
# Only increments if there are .cs file changes in the project directory.
source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

set -euo pipefail

[[ $# -lt 1 ]] && { err "Usage: version-bump.sh <ProjectName>"; exit 1; }

PROJECT="$1"
ROOT="$(find_repo_root)"
CSPROJ=""
for f in src/"$PROJECT"/"$PROJECT".csproj src/"$PROJECT"/*.csproj; do
    if [[ -f "$f" ]]; then
        CSPROJ="$f"
        break
    fi
done

[[ -z "$CSPROJ" ]] && { err "No .csproj found for $PROJECT"; exit 1; }

# Check if any .cs files changed in the project directory (recursive)
PROJECT_DIR=$(dirname "$CSPROJ")
CS_MODIFIED=$(git diff --name-only -- "$PROJECT_DIR/" | { grep '\.cs$' || true; } | head -1)
CS_STAGED=$(git diff --cached --name-only -- "$PROJECT_DIR/" | { grep '\.cs$' || true; } | head -1)
UNTRACKED=$(git ls-files --others --exclude-standard -- "$PROJECT_DIR/" | { grep '\.cs$' || true; } | head -1)

if [[ -z "$CS_MODIFIED" && -z "$CS_STAGED" && -z "$UNTRACKED" ]]; then
    dim "No .cs changes in $PROJECT — skipping version bump"
    exit 0
fi

# Extract current version
CURRENT=$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "$CSPROJ" | head -1)
if [[ -z "$CURRENT" ]]; then
    err "No <Version> found in $CSPROJ"
    exit 1
fi

# Parse major.minor.patch
IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT"
PATCH=$((PATCH + 1))
NEW_VERSION="${MAJOR}.${MINOR}.${PATCH}"

# Replace in file
sed -i '' "s|<Version>${CURRENT}</Version>|<Version>${NEW_VERSION}</Version>|" "$CSPROJ"

ok "Bumped $PROJECT: ${CURRENT} → ${NEW_VERSION}"