#!/usr/bin/env bash
# Git diff wrapper with structured output.
# Usage: diff.sh [--json] [--full] [--files] [--staged] [--commits N | --days N] [--max-lines [N]]
# --commits N: scope to the last N commits plus the working tree.
# --days N:    scope to commits newer than N days plus the working tree.
# --max-lines [N]: warn when the branch changes more than N lines (default 400). Over the limit, it also
#                  prints substantive, mechanical, and lode/plans lines apart. The count
#                  spans the merge base with origin/main, main, or origin/HEAD through the working tree and untracked text files.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

MAX_LINES=""; JSON=false; FULL=false; FILES_ONLY=false; STAGED_ONLY=false; COMMITS=""; DAYS=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json)    JSON=true ;;
        --full)    FULL=true ;;
        --files)   FILES_ONLY=true ;;
        --staged)  STAGED_ONLY=true ;;
        --commits) COMMITS="${2:-}"; shift ;;
        --days)    DAYS="${2:-}"; shift ;;
        --max-lines) if [[ "${2:-}" =~ ^[1-9][0-9]*$ ]]; then MAX_LINES="$2"; shift; else MAX_LINES=400; fi ;;
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

if [[ -n "$MAX_LINES" ]]; then
    SIZE_BASE=""
    for ref in origin/main main origin/HEAD; do
        if git -C "$ROOT" rev-parse --verify -q "$ref^{commit}" > /dev/null; then
            SIZE_BASE=$(git -C "$ROOT" merge-base "$ref" HEAD 2>/dev/null || true)
            [[ -n "$SIZE_BASE" ]] && break
        fi
    done
    SIZE_BASE="${SIZE_BASE:-$EMPTY_TREE}"
    TRACKED=$(git -C "$ROOT" diff --numstat "$SIZE_BASE" 2>/dev/null | awk '$1 != "-" { n += $1 + $2 } END { print n + 0 }')
    UNTRACKED=0
    while IFS= read -r file; do
        [[ -f "$ROOT/$file" ]] || continue
        # rg -I skips binary files. It prints nothing for an empty file.
        lines=$(rg -I --no-filename -c "" -- "$ROOT/$file" 2>/dev/null || true)
        UNTRACKED=$((UNTRACKED + ${lines:-0}))
    done < <(git -C "$ROOT" ls-files --others --exclude-standard)
    TOTAL=$((TRACKED + UNTRACKED))
    OVER=false
    [[ "$TOTAL" -gt "$MAX_LINES" ]] && OVER=true
    SUBS=0; MECH=0; DOCS=0
    if [[ "$OVER" == true ]]; then
        # Fail soft: any error leaves the breakdown at zero.
        MECH_PATHS=$("$(dirname "${BASH_SOURCE[0]}")/internal/mechanical-diff.sh" --json 2>/dev/null \
            | jq -r '.files[] | select(.class == "MECHANICAL") | .path' 2>/dev/null) || MECH_PATHS=""
        BREAKDOWN=$({
            git -C "$ROOT" diff --numstat "$SIZE_BASE" 2>/dev/null | awk -F'\t' '$1 != "-" { print $1 + $2 "\t" $3 }'
            while IFS= read -r file; do
                [[ -f "$ROOT/$file" ]] || continue
                lines=$(rg -I --no-filename -c "" -- "$ROOT/$file" 2>/dev/null || true)
                printf '%s\t%s\n' "${lines:-0}" "$file"
            done < <(git -C "$ROOT" ls-files --others --exclude-standard)
        } | awk -F'\t' -v mech="$MECH_PATHS" '
            BEGIN { n = split(mech, a, "\n"); for (i = 1; i <= n; i++) m[a[i]] = 1 }
            $2 ~ /^(lode|plans)\// { d += $1; next }
            ($2 in m) { x += $1; next }
            { s += $1 }
            END { printf "%d %d %d\n", s, x, d }') || BREAKDOWN=""
        read -r SUBS MECH DOCS <<< "${BREAKDOWN:-0 0 0}"
    fi
    if [[ "$JSON" == true ]]; then
        printf '{"changed_lines":%d,"max_lines":%d,"over":%s,"substantive_lines":%d,"mechanical_lines":%d,"docs_lines":%d}\n' \
            "$TOTAL" "$MAX_LINES" "$OVER" "$SUBS" "$MECH" "$DOCS"
    elif [[ "$OVER" == true ]]; then
        warn "Branch changes $TOTAL lines, over the limit of $MAX_LINES. Split the change."
        printf "Substantive %d, mechanical %d, lode and plans %d.\n" "$SUBS" "$MECH" "$DOCS"
    else
        printf "Branch changes %d lines, within the limit of %d.\n" "$TOTAL" "$MAX_LINES"
    fi
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
