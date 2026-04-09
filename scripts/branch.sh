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

if [[ "$FILES_ONLY" == true ]]; then
    git -C "$ROOT" diff --name-only "$BASE...HEAD" 2>/dev/null | sort -u
    exit 0
fi

if [[ "$JSON" == true ]]; then
    CHANGED=$(git -C "$ROOT" diff --name-only "$BASE...HEAD" 2>/dev/null | wc -l | tr -d ' ')
    printf '{"branch":"%s","base":"%s","ahead":%d,"changed_files":%d}\n' \
        "$CURRENT" "$BASE" "$AHEAD" "$CHANGED"
elif [[ "$HUMAN" == true ]]; then
    printf "%s is %d commit(s) ahead of %s\n" "$CURRENT" "$AHEAD" "$BASE"
else
    printf "${BOLD}Branch:${RESET} %s (base: %s)\n" "$CURRENT" "$BASE"
    printf "${BOLD}Ahead:${RESET} %d commit(s)\n" "$AHEAD"
    if [[ $AHEAD -gt 0 ]]; then
        echo
        printf "${BOLD}Commits:${RESET}\n"
        git -C "$ROOT" log --oneline "$BASE..HEAD" 2>/dev/null
    fi
fi
