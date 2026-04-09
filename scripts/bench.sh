#!/usr/bin/env bash
# Benchmark runner stub — delegates to dotnet run on a benchmarks project.
# Usage: bench.sh [--list] [--dry] [--changed] [FILTER]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
LIST=false; DRY=false; FILTER=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --list)    LIST=true ;;
        --dry)     DRY=true ;;
        --changed) warn "Changed-file detection not yet implemented for benchmarks"; exit 0 ;;
        -*)        err "Unknown flag: $1"; exit 1 ;;
        *)         FILTER="$1" ;;
    esac
    shift
done

# Find benchmarks project
BENCH_PROJ=$(fd -e csproj 'Benchmark' "$ROOT" --max-depth 3 2>/dev/null | head -1)

if [[ -z "$BENCH_PROJ" ]]; then
    dim "No benchmarks project found"
    exit 0
fi

ARGS=(dotnet run --project "$BENCH_PROJ" --configuration Release --)

if [[ "$LIST" == true ]]; then
    "${ARGS[@]}" --list 2>&1
elif [[ "$DRY" == true ]]; then
    if [[ -n "$FILTER" ]]; then
        "${ARGS[@]}" --filter "$FILTER" --job Dry
    else
        "${ARGS[@]}" --job Dry
    fi
elif [[ -n "$FILTER" ]]; then
    "${ARGS[@]}" --filter "$FILTER"
else
    "${ARGS[@]}"
fi
