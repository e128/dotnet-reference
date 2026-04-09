#!/usr/bin/env bash
# Git status wrapper with structured output.
# Usage: status.sh [--json] [--files] [--cs-only] [--classify]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

JSON=false; FILES_ONLY=false; CS_ONLY=false; CLASSIFY=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json)     JSON=true ;;
        --files)    FILES_ONLY=true ;;
        --cs-only)  CS_ONLY=true ;;
        --classify) CLASSIFY=true ;;
        *)          err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

ROOT="$(find_repo_root)"
BRANCH="$(git -C "$ROOT" branch --show-current 2>/dev/null || echo detached)"

# Get file lists
STAGED=$(git -C "$ROOT" diff --cached --name-only 2>/dev/null)
UNSTAGED=$(git -C "$ROOT" diff --name-only 2>/dev/null)
UNTRACKED=$(git -C "$ROOT" ls-files --others --exclude-standard 2>/dev/null)

if [[ "$CS_ONLY" == true ]]; then
    STAGED=$(echo "$STAGED" | grep '\.cs$' || true)
    UNSTAGED=$(echo "$UNSTAGED" | grep '\.cs$' || true)
    UNTRACKED=$(echo "$UNTRACKED" | grep '\.cs$' || true)
fi

STAGED_COUNT=$(echo -n "$STAGED" | grep -c '.' || true)
UNSTAGED_COUNT=$(echo -n "$UNSTAGED" | grep -c '.' || true)
UNTRACKED_COUNT=$(echo -n "$UNTRACKED" | grep -c '.' || true)

if [[ "$FILES_ONLY" == true ]]; then
    { echo "$STAGED"; echo "$UNSTAGED"; echo "$UNTRACKED"; } | grep -v '^$' | sort -u
    exit 0
fi

if [[ "$CLASSIFY" == true ]]; then
    ALL_FILES=$({ echo "$STAGED"; echo "$UNSTAGED"; } | grep -v '^$' | sort -u)
    if [[ -z "$ALL_FILES" ]]; then
        echo "clean"
    elif echo "$ALL_FILES" | grep -qv '\.md$\|\.txt$'; then
        if echo "$ALL_FILES" | grep -q '\.cs$\|\.csproj$\|\.slnx$'; then
            echo "code"
        else
            echo "mixed"
        fi
    else
        echo "docs-only"
    fi
    exit 0
fi

if [[ "$JSON" == true ]]; then
    printf '{"branch":"%s","staged":%d,"unstaged":%d,"untracked":%d}\n' \
        "$BRANCH" "$STAGED_COUNT" "$UNSTAGED_COUNT" "$UNTRACKED_COUNT"
else
    printf "${BOLD}Branch:${RESET} %s\n" "$BRANCH"
    printf "Staged: %d  Unstaged: %d  Untracked: %d\n" \
        "$STAGED_COUNT" "$UNSTAGED_COUNT" "$UNTRACKED_COUNT"
    if [[ $((STAGED_COUNT + UNSTAGED_COUNT + UNTRACKED_COUNT)) -eq 0 ]]; then
        ok "Working tree clean"
    fi
fi
