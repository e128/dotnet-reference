#!/usr/bin/env bash
# Test coverage heuristic: estimate coverage by namespace/project.
# Usage: coverage-areas.sh [--json]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
JSON=false
[[ "${1:-}" == "--json" ]] && JSON=true

# Count source files and test files per project
printf "${BOLD}%-30s %8s %8s %8s${RESET}\n" "Project" "Source" "Tests" "Ratio"

for src_dir in "$ROOT"/src/*/; do
    [[ ! -d "$src_dir" ]] && continue
    project="$(basename "$src_dir")"
    src_count=$(fd -e cs . "$src_dir" 2>/dev/null | wc -l | tr -d ' ')

    # Find corresponding test project
    test_dir="$ROOT/tests/${project}.Tests"
    if [[ -d "$test_dir" ]]; then
        test_count=$(fd -e cs . "$test_dir" 2>/dev/null | wc -l | tr -d ' ')
    else
        test_count=0
    fi

    if [[ $src_count -gt 0 ]]; then
        ratio=$(printf "%.0f%%" "$(echo "scale=2; $test_count * 100 / $src_count" | bc 2>/dev/null || echo 0)")
    else
        ratio="N/A"
    fi

    printf "%-30s %8d %8d %8s\n" "$project" "$src_count" "$test_count" "$ratio"
done
