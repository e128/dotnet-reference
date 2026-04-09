#!/usr/bin/env bash
# Git diff wrapper with structured output.
# Usage: diff.sh [--json] [--full] [--files]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

JSON=false; FULL=false; FILES_ONLY=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json)  JSON=true ;;
        --full)  FULL=true ;;
        --files) FILES_ONLY=true ;;
        *)       err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

ROOT="$(find_repo_root)"

if [[ "$FILES_ONLY" == true ]]; then
    git -C "$ROOT" diff --name-only HEAD 2>/dev/null | sort -u
    exit 0
fi

STAGED_STAT=$(git -C "$ROOT" diff --cached --stat 2>/dev/null | tail -1)
UNSTAGED_STAT=$(git -C "$ROOT" diff --stat 2>/dev/null | tail -1)

# Recent commits (last 5)
RECENT=$(git -C "$ROOT" log --oneline -5 2>/dev/null)

if [[ "$JSON" == true ]]; then
    STAGED_FILES=$(git -C "$ROOT" diff --cached --name-only 2>/dev/null | wc -l | tr -d ' ')
    UNSTAGED_FILES=$(git -C "$ROOT" diff --name-only 2>/dev/null | wc -l | tr -d ' ')
    COMMIT_COUNT=$(git -C "$ROOT" log --oneline -5 2>/dev/null | wc -l | tr -d ' ')
    printf '{"staged_files":%d,"unstaged_files":%d,"recent_commits":%d}\n' \
        "$STAGED_FILES" "$UNSTAGED_FILES" "$COMMIT_COUNT"
else
    if [[ -n "$STAGED_STAT" ]]; then
        printf "${BOLD}Staged:${RESET} %s\n" "$STAGED_STAT"
    fi
    if [[ -n "$UNSTAGED_STAT" ]]; then
        printf "${BOLD}Unstaged:${RESET} %s\n" "$UNSTAGED_STAT"
    fi
    if [[ -z "$STAGED_STAT" && -z "$UNSTAGED_STAT" ]]; then
        dim "No changes"
    fi
    echo
    printf "${BOLD}Recent commits:${RESET}\n"
    echo "$RECENT"

    if [[ "$FULL" == true ]]; then
        echo
        printf "${BOLD}Full diff:${RESET}\n"
        git -C "$ROOT" diff HEAD
    fi
fi
