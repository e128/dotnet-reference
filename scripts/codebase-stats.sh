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

# One walk over src and tests feeds every count below.
mapfile -t cs_loc < <(fd -e cs . "$ROOT/src" "$ROOT/tests" --exclude obj --exclude bin -x wc -l {} \; 2>/dev/null | sort -rn)

src_files=0; test_files=0; src_loc=0; test_loc=0
src_prefix="$ROOT/src/"
test_prefix="$ROOT/tests/"
declare -A proj_files=() proj_loc=()
for entry in "${cs_loc[@]}"; do
    [[ -z "$entry" ]] && continue
    read -r count file <<< "$entry"
    case "$file" in
        "$src_prefix"*)  src_files=$((src_files + 1));  src_loc=$((src_loc + count));  rest="${file#"$src_prefix"}" ;;
        "$test_prefix"*) test_files=$((test_files + 1)); test_loc=$((test_loc + count)); rest="${file#"$test_prefix"}" ;;
        *) continue ;;
    esac
    proj="${rest%%/*}"
    [[ "$proj" == "$rest" ]] && continue    # file sits directly in src/ or tests/
    proj_files["$proj"]=$(( ${proj_files["$proj"]:-0} + 1 ))
    proj_loc["$proj"]=$(( ${proj_loc["$proj"]:-0} + count ))
done

declare -a sorted_proj=()
if [[ ${#proj_files[@]} -gt 0 ]]; then
    mapfile -t sorted_proj < <(printf '%s\n' "${!proj_files[@]}" | sort)
fi

declare -a large_files=()
mapfile -t large_files < <(printf '%s\n' "${cs_loc[@]}" | awk -v t="$THRESHOLD" 'NF && $1 >= t')

declare -a top_files=()
if [[ ${#cs_loc[@]} -gt 0 ]]; then
    mapfile -t top_files < <(printf '%s\n' "${cs_loc[@]}" | head -10)
fi

if [[ "$JSON" == true ]]; then
    proj_json="{"
    first=true
    for name in "${sorted_proj[@]}"; do
        if [[ "$first" == true ]]; then first=false; else proj_json+=","; fi
        proj_json+="\"$name\":{\"files\":${proj_files[$name]},\"loc\":${proj_loc[$name]}}"
    done
    proj_json+="}"

    large_json=$(printf '%s\n' "${large_files[@]}" | awk 'NF{printf "{\"lines\":%s,\"file\":\"%s\"}\n", $1, $2}' | jq -s '.')

    top_json=$(printf '%s\n' "${top_files[@]}" | awk 'NF{printf "{\"lines\":%s,\"file\":\"%s\"}\n", $1, $2}' | jq -s '.')

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
    for name in "${sorted_proj[@]}"; do
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
