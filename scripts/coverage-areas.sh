#!/usr/bin/env bash
# Test coverage heuristic: estimate coverage by namespace/project.
# Usage: coverage-areas.sh [--json] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
JSON=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json) JSON=true ;;
        --help)
            echo "Usage: coverage-areas.sh [--json] [--help]"
            echo "  Estimate test coverage by project from the source and test file counts."
            echo "  --json   Structured JSON output"
            exit 0
            ;;
        *) err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

# Count C# files per project in one walk of both trees.
declare -A _cs_count=()
while IFS= read -r f; do
    rel="${f#"$ROOT"/}"
    case "$rel" in
        src/*)   key="s:${rel#src/}"; key="${key%%/*}" ;;
        tests/*) key="t:${rel#tests/}"; key="${key%%/*}" ;;
        *)       continue ;;
    esac
    _cs_count["$key"]=$(( ${_cs_count["$key"]:-0} + 1 ))
done < <(fd -e cs . "$ROOT/src" "$ROOT/tests" 2>/dev/null)

declare -a proj_names=() proj_src=() proj_tests=() proj_ratio=()
for src_dir in "$ROOT"/src/*/; do
    [[ ! -d "$src_dir" ]] && continue
    project="$(basename "$src_dir")"
    src_count="${_cs_count["s:$project"]:-0}"

    # Find corresponding test project
    test_dir="$ROOT/tests/${project}.Tests"
    if [[ -d "$test_dir" ]]; then
        test_count="${_cs_count["t:${project}.Tests"]:-0}"
    else
        test_count=0
    fi

    if [[ $src_count -gt 0 ]]; then
        ratio=$(printf "%.0f%%" "$(echo "scale=2; $test_count * 100 / $src_count" | bc 2>/dev/null || echo 0)")
    else
        ratio="N/A"
    fi

    proj_names+=("$project")
    proj_src+=("$src_count")
    proj_tests+=("$test_count")
    proj_ratio+=("$ratio")
done

if [[ "$JSON" == true ]]; then
    for i in "${!proj_names[@]}"; do
        printf '%s\t%s\t%s\t%s\n' \
            "${proj_names[$i]}" "${proj_src[$i]}" "${proj_tests[$i]}" "${proj_ratio[$i]}"
    done | jq -R -s 'split("\n") | map(select(length > 0) | split("\t") |
        {project: .[0], source_files: (.[1] | tonumber),
         test_files: (.[2] | tonumber), ratio: .[3]})'
else
    printf "${BOLD}%-30s %8s %8s %8s${RESET}\n" "Project" "Source" "Tests" "Ratio"
    for i in "${!proj_names[@]}"; do
        printf "%-30s %8d %8d %8s\n" \
            "${proj_names[$i]}" "${proj_src[$i]}" "${proj_tests[$i]}" "${proj_ratio[$i]}"
    done
fi
