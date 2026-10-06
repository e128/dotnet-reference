#!/usr/bin/env bash
# Git diff wrapper with structured output.
# Usage: diff.sh [--json] [--full] [--files] [--staged] [--commits N | --days N]
# --commits N: scope to the last N commits plus the working tree.
# --days N:    scope to commits newer than N days plus the working tree.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

JSON=false; FULL=false; FILES_ONLY=false; STAGED_ONLY=false; COMMITS=""; DAYS=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json)    JSON=true ;;
        --full)    FULL=true ;;
        --files)   FILES_ONLY=true ;;
        --staged)  STAGED_ONLY=true ;;
        --commits) COMMITS="${2:-}"; shift ;;
        --days)    DAYS="${2:-}"; shift ;;
        *)         err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

ROOT="$(find_repo_root)"

EMPTY_TREE="4b825dc642cb6eb9a060e54bf8d69288fbee4904"
BASE=""
if [[ -n "$COMMITS" && -n "$DAYS" ]]; then
    err "Use --commits or --days, not both"; exit 1
fi
if [[ -n "$COMMITS" ]]; then
    [[ "$COMMITS" =~ ^[1-9][0-9]*$ ]] || { err "--commits needs a positive integer"; exit 1; }
    BASE=$(git -C "$ROOT" rev-parse --verify -q "HEAD~$COMMITS" 2>/dev/null || echo "$EMPTY_TREE")
elif [[ -n "$DAYS" ]]; then
    [[ "$DAYS" =~ ^[1-9][0-9]*$ ]] || { err "--days needs a positive integer"; exit 1; }
    BASE=$(git -C "$ROOT" rev-list -1 --before="$DAYS days ago" HEAD 2>/dev/null)
    BASE="${BASE:-$EMPTY_TREE}"
fi

if [[ "$FILES_ONLY" == true ]]; then
    git -C "$ROOT" diff --name-only "${BASE:-HEAD}" 2>/dev/null | sort -u
    exit 0
fi

if [[ -n "$BASE" ]]; then
    RANGE_STAT=$(git -C "$ROOT" diff --stat "$BASE" 2>/dev/null | tail -1)
    if [[ "$JSON" == true ]]; then
        RANGE_FILES=$(git -C "$ROOT" diff --name-only "$BASE" 2>/dev/null | wc -l | tr -d ' ')
        printf '{"base":"%s","range_files":%d,"stat":"%s"}\n' "$BASE" "$RANGE_FILES" "$RANGE_STAT"
    else
        printf "${BOLD}Range:${RESET} %s\n" "${RANGE_STAT:-No changes}"
        if [[ "$FULL" == true ]]; then
            git -C "$ROOT" diff "$BASE"
        fi
    fi
    exit 0
fi

if [[ "$STAGED_ONLY" == true ]]; then
    if [[ "$JSON" == true ]]; then
        STAGED_FILES=$(git -C "$ROOT" diff --cached --name-only 2>/dev/null | wc -l | tr -d ' ')
        STAGED_STAT=$(git -C "$ROOT" diff --cached --stat 2>/dev/null | tail -1)
        printf '{"staged_files":%d,"stat":"%s"}\n' "$STAGED_FILES" "$STAGED_STAT"
    else
        git -C "$ROOT" diff --cached --stat 2>/dev/null
    fi
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
