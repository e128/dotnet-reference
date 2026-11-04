#!/usr/bin/env bash
# Set the <Version> in a project's .csproj to the next semantic version.
# Usage: version-bump.sh <ProjectName> [auto|patch|minor|major] [--dry-run]
# Example: version-bump.sh E128.Analyzers
# Example: version-bump.sh E128.Analyzers major --dry-run
#
# The target is computed from the version on the merge base with main, never from
# the working copy. Running the script twice therefore gives the same result,
# and a branch that bumps once ships one version step, however many commits it has.
#
# Level `auto` (default) takes the strongest signal found since the merge base:
#   major  a commit subject `type!:` or a `BREAKING CHANGE:` footer,
#          or a rule added to `### Removed Rules` in AnalyzerReleases.Unshipped.md
#   minor  a `feat:` commit, or a rule added to `### New Rules` or `### Changed Rules`
#   patch  any other change to a .cs file in the project
# A 0.y.z version treats a major signal as minor, per semver section 4.
# An explicit level overrides the signals. No .cs change since the merge base
# skips the bump.
source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

set -euo pipefail

usage() { err "Usage: version-bump.sh <ProjectName> [auto|patch|minor|major] [--dry-run]"; }

PROJECT=""
LEVEL="auto"
DRY_RUN=false
for arg in "$@"; do
    case "$arg" in
        --dry-run) DRY_RUN=true ;;
        auto | patch | minor | major) LEVEL="$arg" ;;
        -*) usage; exit 1 ;;
        *) [[ -z "$PROJECT" ]] && PROJECT="$arg" || { usage; exit 1; } ;;
    esac
done
[[ -z "$PROJECT" ]] && { usage; exit 1; }

CSPROJ=""
for f in src/"$PROJECT"/"$PROJECT".csproj src/"$PROJECT"/*.csproj; do
    if [[ -f "$f" ]]; then
        CSPROJ="$f"
        break
    fi
done
[[ -z "$CSPROJ" ]] && { err "No .csproj found for $PROJECT"; exit 1; }
PROJECT_DIR=$(dirname "$CSPROJ")

# Merge base with the main branch. Without one, fall back to HEAD.
BASE=""
for ref in origin/main main; do
    if git rev-parse --verify -q "$ref^{commit}" > /dev/null; then
        BASE=$(git merge-base "$ref" HEAD)
        break
    fi
done
[[ -z "$BASE" ]] && BASE=$(git rev-parse HEAD)

# Guard: a .cs change in the project since the merge base, committed or not.
CS_CHANGED=$({
    git diff --name-only "$BASE" -- "$PROJECT_DIR/"
    git ls-files --others --exclude-standard -- "$PROJECT_DIR/"
} | { grep '\.cs$' || true; } | head -1)
if [[ -z "$CS_CHANGED" ]]; then
    dim "No .cs changes in $PROJECT since the merge base, skipping version bump"
    exit 0
fi

read_version() { rg -o -r '$1' -m1 '<Version>([^<]+)</Version>' || true; }
SEMVER_RE='^([0-9]+)\.([0-9]+)\.([0-9]+)(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$'

CURRENT=$(read_version < "$CSPROJ")
[[ -z "$CURRENT" ]] && { err "No <Version> found in $CSPROJ"; exit 1; }
[[ "$CURRENT" =~ $SEMVER_RE ]] || { err "Current version '$CURRENT' is not semver"; exit 1; }

BASE_VERSION=$(git show "$BASE:$CSPROJ" 2> /dev/null | read_version)
[[ -z "$BASE_VERSION" ]] && BASE_VERSION="$CURRENT"
[[ "$BASE_VERSION" =~ $SEMVER_RE ]] || { err "Base version '$BASE_VERSION' is not semver"; exit 1; }
MAJOR=${BASH_REMATCH[1]} MINOR=${BASH_REMATCH[2]} PATCH=${BASH_REMATCH[3]}

# Auto level: strongest signal wins.
REASON="explicit level"
if [[ "$LEVEL" == "auto" ]]; then
    LEVEL="patch"
    REASON="a .cs change"

    # Commit signals since the merge base.
    while IFS= read -r -d $'\x1e' record; do
        record=${record#$'\n'}
        subject=${record%%$'\n'*}
        if [[ "$subject" =~ ^[a-z]+(\([^\)]*\))?!: || "$record" =~ (^|$'\n')BREAKING[\ -]CHANGE: ]]; then
            LEVEL="major"; REASON="breaking commit: $subject"; break
        elif [[ "$subject" =~ ^feat(\([^\)]*\))?: && "$LEVEL" == "patch" ]]; then
            LEVEL="minor"; REASON="feat commit: $subject"
        fi
    done < <(git log --format='%s%n%b%x1e' "$BASE..HEAD" -- "$PROJECT_DIR/")

    # Release-notes signals: rows added to the unshipped file since the merge base.
    UNSHIPPED="$PROJECT_DIR/AnalyzerReleases.Unshipped.md"
    if [[ -f "$UNSHIPPED" ]]; then
        release_rows() {
            awk '/^### /{sec=$0; sub(/^### /, "", sec); next}
                 /^[A-Za-z]+[0-9]+ *\|/{id=$0; sub(/ *\|.*/, "", id); print sec "|" id}' | sort -u
        }
        ADDED=$(comm -13 \
            <(git show "$BASE:$UNSHIPPED" 2> /dev/null | release_rows) \
            <(release_rows < "$UNSHIPPED"))
        if [[ "$ADDED" == *"Removed Rules|"* ]]; then
            LEVEL="major"; REASON="rule removed in $UNSHIPPED"
        elif [[ "$LEVEL" == "patch" && "$ADDED" == *"New Rules|"* ]]; then
            LEVEL="minor"; REASON="rule added in $UNSHIPPED"
        elif [[ "$LEVEL" == "patch" && "$ADDED" == *"Changed Rules|"* ]]; then
            LEVEL="minor"; REASON="rule changed in $UNSHIPPED"
        fi
    fi
fi

# Semver section 4: a 0.y.z API is unstable, so a breaking change raises y.
if [[ "$LEVEL" == "major" && "$MAJOR" -eq 0 ]]; then
    LEVEL="minor"
fi

case "$LEVEL" in
    major) TARGET="$((MAJOR + 1)).0.0" ;;
    minor) TARGET="${MAJOR}.$((MINOR + 1)).0" ;;
    patch) TARGET="${MAJOR}.${MINOR}.$((PATCH + 1))" ;;
esac

# Idempotent: keep a current version that already meets or passes the target.
CURRENT_CORE="${CURRENT%%[-+]*}"
if [[ "$CURRENT" == "$CURRENT_CORE" && "$(printf '%s\n%s\n' "$CURRENT" "$TARGET" | sort -V | tail -1)" == "$CURRENT" ]]; then
    dim "$PROJECT is already at $CURRENT (target $TARGET from base $BASE_VERSION, $LEVEL), no bump"
    exit 0
fi

if [[ "$DRY_RUN" == true ]]; then
    ok "Would bump $PROJECT: $CURRENT → $TARGET ($LEVEL from base $BASE_VERSION, $REASON)"
    exit 0
fi

perl -i -pe "s|<Version>\Q${CURRENT}\E</Version>|<Version>${TARGET}</Version>|" "$CSPROJ"
ok "Bumped $PROJECT: $CURRENT → $TARGET ($LEVEL from base $BASE_VERSION, $REASON)"
