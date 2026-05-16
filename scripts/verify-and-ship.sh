#!/usr/bin/env bash
# Verify, commit, and push: format → check → version-bump → stage → precommit → commit → push.
# Usage: verify-and-ship.sh [--message MSG] [--squash] [--no-version-bump] [--json] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
JSON=false
SQUASH=false
VERSION_BUMP=true
MESSAGE=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json)             JSON=true ;;
        --squash)           SQUASH=true ;;
        --no-version-bump)  VERSION_BUMP=false ;;
        --message|-m)       shift; MESSAGE="${1:?--message requires a value}" ;;
        --help)
            echo "Usage: verify-and-ship.sh [OPTIONS]"
            echo "  Composed pipeline: format → check → version-bump → stage → precommit → commit → push."
            echo ""
            echo "Options:"
            echo "  --message, -m MSG    Commit message (required)"
            echo "  --squash             Squash all commits on branch into one before committing"
            echo "  --no-version-bump    Skip version-bump.sh step"
            echo "  --json               JSON status output"
            echo "  --help               Show this help"
            exit 0
            ;;
        *) err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

if [[ -z "$MESSAGE" ]]; then
    err "Commit message required: --message \"...\""
    exit 1
fi

step=0
total=7
report_step() {
    step=$((step + 1))
    if [[ "$JSON" != true ]]; then
        info "[$step/$total] $1"
    fi
}

report_step "Formatting changed files"
if ! "$ROOT/scripts/format.sh" --changed > /dev/null 2>&1; then
    warn "Format had issues, continuing"
fi

report_step "Running check (build + test)"
if ! "$ROOT/scripts/check.sh" --no-format --all 2>&1; then
    err "Check failed — fix errors before shipping"
    exit 1
fi

if [[ "$VERSION_BUMP" == true ]]; then
    report_step "Bumping version"
    "$ROOT/scripts/internal/version-bump.sh" E128.Analyzers 2>&1
else
    report_step "Skipping version bump"
fi

if [[ "$SQUASH" == true ]]; then
    report_step "Squashing commits onto merge base"
    merge_base=$(git merge-base main HEAD)
    git reset --soft "$merge_base"
else
    report_step "No squash requested"
fi

report_step "Staging all changes"
"$ROOT/scripts/internal/stage.sh" --include-new 2>&1

report_step "Running precommit checks"
if ! "$ROOT/scripts/internal/precommit.sh" 2>&1; then
    err "Precommit failed — fix issues before shipping"
    exit 1
fi

report_step "Committing and pushing"
"$ROOT/scripts/internal/commit.sh" --skip-precommit "$MESSAGE" 2>&1

branch=$(git branch --show-current)
git push -u origin "$branch" 2>&1

if [[ "$JSON" == true ]]; then
    json_object status=ok branch="$branch" message="$MESSAGE" squashed="$SQUASH" version_bumped="$VERSION_BUMP"
else
    ok "Shipped on $branch"
fi
