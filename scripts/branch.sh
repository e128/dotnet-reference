#!/usr/bin/env bash
# Branch info: ahead count, commit list, changed files.
# Usage: branch.sh [--base BRANCH] [--json] [--human] [--files]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

BASE="main"; JSON=true; HUMAN=false; FILES_ONLY=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --base)  BASE="$2"; shift ;;
        --json)  JSON=true; HUMAN=false ;;
        --human) HUMAN=true; JSON=false ;;
        --files) FILES_ONLY=true ;;
        *)       err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

ROOT="$(find_repo_root)"
CURRENT="$(git -C "$ROOT" branch --show-current 2>/dev/null || echo detached)"

# Ahead count
AHEAD=$(git -C "$ROOT" rev-list --count "$BASE..HEAD" 2>/dev/null || echo 0)

# Unpushed count: commits on HEAD not on the upstream tracking branch.
# No upstream configured means the branch has never been pushed, so every
# commit ahead of base is unpushed.
UPSTREAM=$(git -C "$ROOT" rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>/dev/null || echo "")
if [[ -n "$UPSTREAM" ]]; then
    UNPUSHED=$(git -C "$ROOT" rev-list --count "$UPSTREAM..HEAD" 2>/dev/null || echo 0)
else
    UNPUSHED=$AHEAD
fi

if [[ "$FILES_ONLY" == true ]]; then
    git -C "$ROOT" diff --name-only "$BASE...HEAD" 2>/dev/null | sort -u
    exit 0
fi

if [[ "$JSON" == true ]]; then
    CHANGED=$(git -C "$ROOT" diff --name-only "$BASE...HEAD" 2>/dev/null | wc -l | tr -d ' ')
    printf '{"branch":"%s","base":"%s","ahead":%d,"changed_files":%d,"upstream":"%s","unpushed":%d}\n' \
        "$CURRENT" "$BASE" "$AHEAD" "$CHANGED" "$UPSTREAM" "$UNPUSHED"
elif [[ "$HUMAN" == true ]]; then
    printf "%s is %d commit(s) ahead of %s\n" "$CURRENT" "$AHEAD" "$BASE"
    if [[ -n "$UPSTREAM" ]]; then
        printf "%d commit(s) unpushed to %s\n" "$UNPUSHED" "$UPSTREAM"
    else
        printf "no upstream configured — %d commit(s) unpushed\n" "$UNPUSHED"
    fi
else
    printf "${BOLD}Branch:${RESET} %s (base: %s)\n" "$CURRENT" "$BASE"
    printf "${BOLD}Ahead:${RESET} %d commit(s)\n" "$AHEAD"
    if [[ -n "$UPSTREAM" ]]; then
        printf "${BOLD}Unpushed:${RESET} %d commit(s) (upstream: %s)\n" "$UNPUSHED" "$UPSTREAM"
    else
        printf "${BOLD}Unpushed:${RESET} %d commit(s) (no upstream configured)\n" "$UNPUSHED"
    fi
    if [[ $AHEAD -gt 0 ]]; then
        echo
        printf "${BOLD}Commits:${RESET}\n"
        git -C "$ROOT" log --oneline "$BASE..HEAD" 2>/dev/null
    fi
fi
