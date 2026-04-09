#!/usr/bin/env bash
# Lode timestamp bumper: update timestamps on lode files.
# Usage: lode-ts.sh [--changed] [--stale] [FILE...]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
CHANGED=false; STALE=false
FILES=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --changed) CHANGED=true ;;
        --stale)   STALE=true ;;
        -*)        err "Unknown flag: $1"; exit 1 ;;
        *)         FILES+=("$1") ;;
    esac
    shift
done

TS="$(iso_timestamp)"

if [[ "$STALE" == true ]]; then
    # Report lode files by staleness
    printf "${BOLD}%-50s %s${RESET}\n" "File" "Last Updated"
    fd -e md . "$ROOT/lode" 2>/dev/null | while IFS= read -r file; do
        updated=$(grep -oP '\*Updated: \K[^*]+' "$file" 2>/dev/null || echo "never")
        rel="${file#$ROOT/}"
        printf "%-50s %s\n" "$rel" "$updated"
    done | sort -k2
    exit 0
fi

if [[ "$CHANGED" == true ]]; then
    # Auto-detect changed lode files from git diff
    while IFS= read -r f; do FILES+=("$f"); done < <(git -C "$ROOT" diff --name-only HEAD -- 'lode/*.md' 2>/dev/null)
    if [[ ${#FILES[@]} -eq 0 ]]; then
        ok "No changed lode files"
        exit 0
    fi
fi

if [[ ${#FILES[@]} -eq 0 ]]; then
    err "No files specified. Use --changed or pass file paths."
    exit 1
fi

for file in "${FILES[@]}"; do
    # Resolve relative paths
    [[ "$file" != /* ]] && file="$ROOT/$file"
    if [[ ! -f "$file" ]]; then
        warn "File not found: $file"
        continue
    fi

    if grep -q '\*Updated:' "$file"; then
        sed -i '' "s|\*Updated: .*\*|\*Updated: ${TS}\*|" "$file"
        ok "Updated: $(basename "$file")"
    elif grep -q '\*Created:' "$file"; then
        sed -i '' "s|\*Created: .*\*|\*Created: ${TS}\*|" "$file"
        ok "Updated created: $(basename "$file")"
    else
        warn "No timestamp line in: $(basename "$file")"
    fi
done
