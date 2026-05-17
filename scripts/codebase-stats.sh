#!/usr/bin/env bash
# Codebase file and LOC statistics by project.
# Usage: codebase-stats.sh [--json] [--threshold N] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
JSON=false
THRESHOLD=500

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json) JSON=true ;;
        --threshold) shift; THRESHOLD="${1:?--threshold requires a number}" ;;
        --help)
            echo "Usage: codebase-stats.sh [--json] [--threshold N] [--help]"
            echo "  Counts .cs files and LOC by project directory."
            echo "  Lists files exceeding the line threshold."
            echo "  --json          Structured JSON output"
            echo "  --threshold N   Line count threshold for large files (default: 500)"
            exit 0
            ;;
        *) err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

src_files=$(fd -e cs . "$ROOT/src" --exclude obj --exclude bin 2>/dev/null | wc -l | tr -d ' ')
test_files=$(fd -e cs . "$ROOT/tests" --exclude obj --exclude bin 2>/dev/null | wc -l | tr -d ' ')
src_loc=$(fd -e cs . "$ROOT/src" --exclude obj --exclude bin -x wc -l {} \; 2>/dev/null | awk '{s+=$1} END {print s+0}')
test_loc=$(fd -e cs . "$ROOT/tests" --exclude obj --exclude bin -x wc -l {} \; 2>/dev/null | awk '{s+=$1} END {print s+0}')

declare -A proj_files proj_loc
while IFS= read -r dir; do
    name=$(basename "$dir")
    count=$(fd -e cs . "$dir" --exclude obj --exclude bin 2>/dev/null | wc -l | tr -d ' ')
    loc=$(fd -e cs . "$dir" --exclude obj --exclude bin -x wc -l {} \; 2>/dev/null | awk '{s+=$1} END {print s+0}')
    proj_files[$name]=$count
    proj_loc[$name]=$loc
done < <(fd -t d --max-depth 1 . "$ROOT/src" "$ROOT/tests" 2>/dev/null)

mapfile -t large_files < <(fd -e cs . "$ROOT/src" "$ROOT/tests" --exclude obj --exclude bin -x wc -l {} \; 2>/dev/null | awk -v t="$THRESHOLD" '$1 >= t' | sort -rn)

mapfile -t top_files < <(fd -e cs . "$ROOT/src" "$ROOT/tests" --exclude obj --exclude bin -x wc -l {} \; 2>/dev/null | sort -rn | head -10)

if [[ "$JSON" == true ]]; then
    proj_json="{"
    first=true
    for name in $(echo "${!proj_files[@]}" | tr ' ' '\n' | sort); do
        if [[ "$first" == true ]]; then first=false; else proj_json+=","; fi
        proj_json+="\"$name\":{\"files\":${proj_files[$name]},\"loc\":${proj_loc[$name]}}"
    done
    proj_json+="}"

    large_json=$(for entry in "${large_files[@]}"; do
        lines=$(echo "$entry" | awk '{print $1}')
        file=$(echo "$entry" | awk '{print $2}')
        printf '{"lines":%s,"file":"%s"}\n' "$lines" "$file"
    done | jq -s '.')

    top_json=$(for entry in "${top_files[@]}"; do
        lines=$(echo "$entry" | awk '{print $1}')
        file=$(echo "$entry" | awk '{print $2}')
        printf '{"lines":%s,"file":"%s"}\n' "$lines" "$file"
    done | jq -s '.')

    jq -n \
        --argjson src_files "$src_files" \
        --argjson test_files "$test_files" \
        --argjson src_loc "$src_loc" \
        --argjson test_loc "$test_loc" \
        --argjson projects "$proj_json" \
        --argjson large "$large_json" \
        --argjson top "$top_json" \
        --argjson threshold "$THRESHOLD" \
        '{src_files: $src_files, test_files: $test_files, src_loc: $src_loc, test_loc: $test_loc, projects: $projects, large_files: $large, top_10: $top, threshold: $threshold}'
else
    printf "${BOLD}Codebase Statistics${RESET}\n"
    printf "  Source files:    %d (%d LOC)\n" "$src_files" "$src_loc"
    printf "  Test files:      %d (%d LOC)\n" "$test_files" "$test_loc"
    printf "  Total:           %d files, %d LOC\n" "$((src_files + test_files))" "$((src_loc + test_loc))"

    printf "\n${BOLD}By project:${RESET}\n"
    for name in $(echo "${!proj_files[@]}" | tr ' ' '\n' | sort); do
        printf "  %-40s %4d files  %6d LOC\n" "$name" "${proj_files[$name]}" "${proj_loc[$name]}"
    done

    if [[ ${#large_files[@]} -gt 0 ]]; then
        printf "\n${BOLD}Files over %d lines:${RESET}\n" "$THRESHOLD"
        for entry in "${large_files[@]}"; do
            printf "  %s\n" "$entry"
        done
    fi

    printf "\n${BOLD}Top 10 largest files:${RESET}\n"
    for entry in "${top_files[@]}"; do
        printf "  %s\n" "$entry"
    done
fi
