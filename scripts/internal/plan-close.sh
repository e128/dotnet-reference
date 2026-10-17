#!/usr/bin/env bash
# Plan closure helper: verify all tasks complete, then remove plan directory.
# Usage: plan-close.sh --plan NAME [--dry-run]
source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

ROOT="$(find_repo_root)"
PLAN=""; DRY_RUN=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --plan)    PLAN="$2"; shift ;;
        --dry-run) DRY_RUN=true ;;
        *)         err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

if [[ -z "$PLAN" ]]; then
    err "Usage: plan-close.sh --plan NAME"
    exit 1
fi

PLAN_DIR="$ROOT/plans/$PLAN"
TASKS_FILE="$PLAN_DIR/$PLAN-tasks.md"

if [[ ! -d "$PLAN_DIR" ]]; then
    err "Plan directory not found: $PLAN_DIR"
    exit 1
fi

# Check for unchecked tasks (excluding Discovered During Implementation).
# A count of zero exits 1. The old pipeline exited 1 on an empty grep result
# under pipefail, so a complete plan stopped the script here.
UNCHECKED=$(awk '/^- \[ \]/ && !/Discovered/ {n++} END {print n+0; if (n == 0) exit 1}' "$TASKS_FILE" 2>/dev/null) || exit 1

if [[ $UNCHECKED -gt 0 ]]; then
    err "$UNCHECKED unchecked task(s) remain — cannot close plan"
    grep '^\- \[ \]' "$TASKS_FILE" | head -5
    exit 1
fi

if [[ "$DRY_RUN" == true ]]; then
    info "Would remove: $PLAN_DIR"
    exit 0
fi

# Remove plan directory via git
git -C "$ROOT" rm -rf "$PLAN_DIR"
ok "Plan closed: $PLAN (git history preserves all files)"
