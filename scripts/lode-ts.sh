#!/usr/bin/env bash
# Lode timestamp bumper: update timestamps on lode files.
# Usage: lode-ts.sh [--changed] [--stale [--json]] [FILE...]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
CHANGED=false; STALE=false; JSON=false
FILES=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --changed) CHANGED=true ;;
        --stale)   STALE=true ;;
        --json)    JSON=true ;;
        -*)        err "Unknown flag: $1"; exit 1 ;;
        *)         FILES+=("$1") ;;
    esac
    shift
done

TS="$(iso_timestamp)"

# Portable extraction of the *Updated: ...* timestamp (BSD grep has no -P).
lode_updated() { sed -n 's/.*\*Updated: \([^*]*\)\*.*/\1/p' "$1" 2>/dev/null | head -1; }

# Commits to code/config since a timestamp (signal for staleness).
commits_since() { git -C "$ROOT" rev-list --count "--since=$1" HEAD -- src tests scripts .claude 2>/dev/null || echo 0; }

if [[ "$STALE" == true ]]; then
    now_epoch=$(date -u +%s)
    if [[ "$JSON" == true ]]; then
        first=true
        printf '['
        fd -e md . "$ROOT/lode" 2>/dev/null | sort | while IFS= read -r file; do
            updated=$(lode_updated "$file")
            [[ -z "$updated" ]] && continue   # no timestamp: reported via Phase 1, not here
            rel="${file#"$ROOT"/}"
            since=$(commits_since "$updated")
            up_epoch=$(date -u -j -f "%Y-%m-%dT%H:%M:%SZ" "$updated" +%s 2>/dev/null || date -u -d "$updated" +%s 2>/dev/null || echo "$now_epoch")
            days=$(( (now_epoch - up_epoch) / 86400 ))
            $first || printf ','
            first=false
            printf '{"file":"%s","updated":"%s","days_ago":%d,"commits_since":%d}' "$rel" "$updated" "$days" "$since"
        done
        printf ']\n'
        exit 0
    fi
    printf "${BOLD}%-50s %-22s %s${RESET}\n" "File" "Last Updated" "Commits Since"
    fd -e md . "$ROOT/lode" 2>/dev/null | while IFS= read -r file; do
        updated=$(lode_updated "$file"); [[ -z "$updated" ]] && updated="never"
        rel="${file#"$ROOT"/}"
        if [[ "$updated" == never ]]; then since="—"; else since=$(commits_since "$updated"); fi
        printf "%-50s %-22s %s\n" "$rel" "$updated" "$since"
    done | sort -t$'\t' -k1
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
