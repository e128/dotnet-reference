#!/usr/bin/env bash
# Detect trigger-phrase overlap between agents and skills.
# Usage: overlap-detect.sh [--json] [--threshold N]
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

JSON=false
THRESHOLD=50  # percentage overlap to flag

for arg in "$@"; do
    case "$arg" in
        --json) JSON=true ;;
        --threshold) ;; # next arg is the value
        [0-9]*) THRESHOLD="$arg" ;;
        *) err "Unknown flag: $arg"; exit 1 ;;
    esac
done

ROOT="$(find_repo_root)"
AGENTS_DIR="$ROOT/.claude/agents"
SKILLS_DIR="$ROOT/.claude/skills"

declare -A TRIGGERS

extract_triggers() {
    local file="$1"
    local name="$2"
    local triggers=""
    local in_triggers=false

    while IFS= read -r line; do
        if [[ "$line" =~ Triggers\ on: ]]; then
            in_triggers=true
            triggers="${line#*Triggers on:}"
        elif [[ "$in_triggers" == true ]]; then
            if [[ "$line" =~ ^[[:space:]] && ! "$line" =~ ^--- && ! "$line" =~ ^[a-z] ]]; then
                triggers="$triggers $line"
            else
                in_triggers=false
            fi
        fi
    done < "$file"

    # Normalize: lowercase, split on comma, trim whitespace, sort
    echo "$triggers" | tr ',' '\n' | sed 's/^[[:space:]]*//;s/[[:space:]]*$//' | tr '[:upper:]' '[:lower:]' | sed '/^$/d' | sort -u
}

# Collect all items with triggers
declare -A ITEM_TRIGGERS
shopt -s nullglob

for f in "$AGENTS_DIR"/*.md; do
    [[ ! -f "$f" ]] && continue
    name="$(basename "$f" .md)"
    triggers="$(extract_triggers "$f" "$name")"
    [[ -n "$triggers" ]] && ITEM_TRIGGERS["$name"]="$triggers"
done

for d in "$SKILLS_DIR"/*/SKILL.md; do
    [[ ! -f "$d" ]] && continue
    name="$(basename "$(dirname "$d")")"
    triggers="$(extract_triggers "$d" "$name")"
    [[ -n "$triggers" ]] && ITEM_TRIGGERS["$name"]="$triggers"
done

# Compare all pairs
overlaps=()
items=("${!ITEM_TRIGGERS[@]}")
count="${#items[@]}"

for ((i=0; i<count; i++)); do
    for ((j=i+1; j<count; j++)); do
        a="${items[$i]}"
        b="${items[$j]}"

        a_triggers="${ITEM_TRIGGERS[$a]}"
        b_triggers="${ITEM_TRIGGERS[$b]}"

        # Count triggers
        a_count=$(echo "$a_triggers" | wc -l | tr -d ' ')
        b_count=$(echo "$b_triggers" | wc -l | tr -d ' ')

        # Find common triggers
        common=$(comm -12 <(echo "$a_triggers") <(echo "$b_triggers") | wc -l | tr -d ' ')

        if [[ "$common" -gt 0 ]]; then
            # Calculate overlap percentage (against the smaller set)
            smaller=$((a_count < b_count ? a_count : b_count))
            if [[ "$smaller" -gt 0 ]]; then
                pct=$((common * 100 / smaller))
            else
                pct=0
            fi

            if [[ "$pct" -ge "$THRESHOLD" ]]; then
                shared=$(comm -12 <(echo "$a_triggers") <(echo "$b_triggers") | paste -sd',' -)
                overlaps+=("$a|$b|$common|$pct|$shared")
            fi
        fi
    done
done

if [[ "$JSON" == true ]]; then
    printf '{"threshold":%d,"overlaps":[' "$THRESHOLD"
    first=true
    for entry in "${overlaps[@]}"; do
        IFS='|' read -r item_a item_b shared_count pct shared_phrases <<< "$entry"
        if [[ "$first" == true ]]; then first=false; else printf ','; fi
        printf '{"a":"%s","b":"%s","shared_count":%s,"overlap_pct":%s,"shared_phrases":"%s"}' \
            "$item_a" "$item_b" "$shared_count" "$pct" "$shared_phrases"
    done
    printf ']}\n'
else
    if [[ ${#overlaps[@]} -eq 0 ]]; then
        ok "No overlaps above ${THRESHOLD}% threshold"
    else
        printf "${BOLD}Trigger Phrase Overlaps (>=%d%%)${RESET}\n\n" "$THRESHOLD"
        printf "%-25s %-25s %8s %6s  %s\n" "Item A" "Item B" "Shared" "Pct" "Phrases"
        printf "%-25s %-25s %8s %6s  %s\n" "-------------------------" "-------------------------" "--------" "------" "---"
        for entry in "${overlaps[@]}"; do
            IFS='|' read -r item_a item_b shared_count pct shared_phrases <<< "$entry"
            printf "%-25s %-25s %8s %5s%%  %s\n" "$item_a" "$item_b" "$shared_count" "$pct" "$shared_phrases"
        done
    fi
fi
