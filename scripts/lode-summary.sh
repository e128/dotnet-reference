#!/usr/bin/env bash
# Lode section lookup: find and display lode content by section.
# Usage: lode-summary.sh [SECTION] [--search PATTERN]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
SECTION="${1:-}"; SEARCH=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --search) SEARCH="$2"; shift ;;
        -*)       err "Unknown flag: $1"; exit 1 ;;
        *)        [[ -z "$SECTION" ]] && SECTION="$1" ;;
    esac
    shift
done

LODE_DIR="$ROOT/lode"

if [[ ! -d "$LODE_DIR" ]]; then
    err "No lode/ directory found"
    exit 1
fi

if [[ -n "$SEARCH" ]]; then
    # Search lode content
    rg --no-heading -n "$SEARCH" "$LODE_DIR" 2>/dev/null || dim "No matches for: $SEARCH"
    exit 0
fi

if [[ -z "$SECTION" ]]; then
    # Count markdown files per top-level directory in one walk.
    declare -A _dir_count=()
    while IFS= read -r file; do
        rel="${file#"$LODE_DIR"/}"
        rel="${rel%%/*}"
        _dir_count["$rel"]=$(( ${_dir_count["$rel"]:-0} + 1 ))
    done < <(fd -e md . "$LODE_DIR" 2>/dev/null)

    # List all sections (top-level directories)
    printf "${BOLD}Lode sections:${RESET}\n"
    fd -t d --max-depth 1 . "$LODE_DIR" 2>/dev/null | while IFS= read -r dir; do
        name="${dir%/}"
        name="${name##*/}"
        count="${_dir_count["$name"]:-0}"
        printf "  %-30s %d files\n" "$name/" "$count"
    done

    # List root files
    fd -e md --max-depth 1 . "$LODE_DIR" 2>/dev/null | while IFS= read -r file; do
        printf "  %s\n" "${file##*/}"
    done
    exit 0
fi

# Show files in a specific section
SECTION_DIR="$LODE_DIR/$SECTION"
if [[ -d "$SECTION_DIR" ]]; then
    fd -e md . "$SECTION_DIR" 2>/dev/null | while IFS= read -r file; do
        desc=""
        IFS= read -r desc < "$file" || true
        desc="${desc#'# '}"
        printf "  %-40s %s\n" "${file##*/}" "$desc"
    done
elif [[ -f "$LODE_DIR/$SECTION.md" ]]; then
    cat "$LODE_DIR/$SECTION.md"
else
    err "Section not found: $SECTION"
    exit 1
fi
