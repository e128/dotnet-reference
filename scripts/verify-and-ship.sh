#!/usr/bin/env bash
# Verify, squash, commit, and push a feature branch: format → bump → check → stage → squash → scan and commit → push.
# Usage: verify-and-ship.sh --message MSG [--no-version-bump] [--json] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
cd "$ROOT" || exit 1
JSON=false
VERSION_BUMP=true
MESSAGE=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json)             JSON=true ;;
        --no-version-bump)  VERSION_BUMP=false ;;
        --message|-m)       shift; MESSAGE="${1:?--message requires a value}" ;;
        --help)
            echo "Usage: verify-and-ship.sh --message MSG [--no-version-bump] [--json]"
            echo "  Ship the current feature branch as one commit."
            echo "  Pipeline: format → bump → check → stage → squash → scan and commit → push."
            echo "  The script squashes only commits that no remote has seen and never force-pushes."
            echo ""
            echo "Options:"
            echo "  --message, -m MSG    Commit message (required)"
            echo "  --no-version-bump    Skip version-bump.sh"
            echo "  --json               Print one JSON object"
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
if [[ "$MESSAGE" =~ $EMAIL_REGEX ]]; then
    err "The commit message contains an email address. Remove every address, placeholders included."
    exit 1
fi

# ── Validate before any mutation ──────────────────
BRANCH="$(git branch --show-current)"
if [[ -z "$BRANCH" ]]; then
    err "Detached HEAD. Check out a feature branch first."
    exit 1
fi

default_ref="$(git symbolic-ref -q --short refs/remotes/origin/HEAD || true)"
DEFAULT_BRANCH="${default_ref#origin/}"
DEFAULT_BRANCH="${DEFAULT_BRANCH:-main}"
if [[ "$BRANCH" == "$DEFAULT_BRANCH" || "$BRANCH" == main ]]; then
    err "Refusing to ship the default branch ($BRANCH). Check out a feature branch."
    exit 1
fi

BASE_REF="refs/remotes/origin/$DEFAULT_BRANCH"
if ! git rev-parse --verify -q "$BASE_REF^{commit}" > /dev/null; then
    err "Cannot find origin/$DEFAULT_BRANCH. Fetch it, then retry."
    exit 1
fi

# SQUASH_BASE is the newest commit that a remote already has, or the merge base for a first push.
if git config --get "branch.$BRANCH.merge" > /dev/null; then
    upstream_remote="$(git config --get "branch.$BRANCH.remote" || true)"
    upstream_merge="$(git config --get "branch.$BRANCH.merge" || true)"
    if [[ "$upstream_remote" != origin || "$upstream_merge" != "refs/heads/$BRANCH" ]]; then
        err "The upstream of $BRANCH must be origin/$BRANCH. Found ${upstream_remote:-none} ${upstream_merge:-none}."
        exit 1
    fi
    if ! SQUASH_BASE="$(git rev-parse --verify -q '@{upstream}^{commit}')"; then
        err "The upstream of $BRANCH is configured but cannot be resolved."
        exit 1
    fi
    if ! git merge-base --is-ancestor "$SQUASH_BASE" HEAD; then
        err "$BRANCH has diverged from its upstream. Shipping would need a force push."
        exit 1
    fi
else
    if git rev-parse --verify -q "refs/remotes/origin/$BRANCH" > /dev/null; then
        err "origin/$BRANCH exists but $BRANCH has no upstream. Set the upstream, then retry."
        exit 1
    fi
    if ! SQUASH_BASE="$(git merge-base "$BASE_REF" HEAD)"; then
        err "$BRANCH shares no history with origin/$DEFAULT_BRANCH."
        exit 1
    fi
fi

if [[ "$(git rev-parse HEAD)" == "$SQUASH_BASE" && -z "$(git status --porcelain)" ]]; then
    err "Nothing to ship on $BRANCH."
    exit 1
fi

# ── Pipeline ──────────────────────────────────────
step=0
total=7
report_step() {
    step=$((step + 1))
    if [[ "$JSON" != true ]]; then
        info "[$step/$total] $1"
    fi
}

# Run a gate. A failure prints the gate output and stops the run.
run_gate() {
    local label="$1"
    shift
    report_step "$label"
    local out
    if ! out="$("$@" 2>&1)"; then
        err "$label failed"
        printf '%s\n' "$out" >&2
        exit 1
    fi
    if [[ "$JSON" != true && -n "$out" ]]; then
        printf '%s\n' "$out"
    fi
}

run_gate "Formatting changed files" "$ROOT/scripts/format.sh" --changed

if [[ "$VERSION_BUMP" == true ]]; then
    run_gate "Bumping version" "$ROOT/scripts/internal/version-bump.sh" E128.Analyzers --message "$MESSAGE"
else
    report_step "Skipping version bump"
fi

run_gate "Running check (shell lint, format check, build, test)" "$ROOT/scripts/check.sh" --all
run_gate "Staging all changes" "$ROOT/scripts/internal/stage.sh" --include-new

report_step "Squashing unpushed commits"
PRE_SQUASH_HEAD="$(git rev-parse HEAD)"
git reset --soft "$SQUASH_BASE"

# commit.sh runs the precommit scan over the whole squashed index, then commits.
report_step "Scanning and committing"
if ! commit_out="$("$ROOT/scripts/internal/commit.sh" "$MESSAGE" 2>&1)"; then
    git reset --soft "$PRE_SQUASH_HEAD"
    err "Scan or commit failed. History is restored."
    printf '%s\n' "$commit_out" >&2
    exit 1
fi

report_step "Pushing $BRANCH"
if ! push_out="$(git push -u origin "$BRANCH" 2>&1)"; then
    err "Push failed. The result on origin may be unknown."
    printf '%s\n' "$push_out" >&2
    err "The squashed commit stays on $BRANCH. Run git fetch origin and compare origin/$BRANCH before you retry."
    err "To restore the original commits, run: git reset --soft $PRE_SQUASH_HEAD"
    exit 1
fi

if [[ "$JSON" == true ]]; then
    json_object status=ok branch="$BRANCH" commit="$(git rev-parse HEAD)" message="$MESSAGE" version_bumped="$VERSION_BUMP"
else
    ok "Shipped $BRANCH"
fi
