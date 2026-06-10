#!/usr/bin/env bash
# Solution inventory: solution file, projects with paths/kind/packable flag, and READMEs.
# Usage: solution-inventory.sh [--json] [--packable] [--readmes] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
JSON=false
PACKABLE_ONLY=false
READMES_ONLY=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json)     JSON=true ;;
        --packable) PACKABLE_ONLY=true ;;
        --readmes)  READMES_ONLY=true ;;
        --help)
            echo "Usage: solution-inventory.sh [--json] [--packable] [--readmes] [--help]"
            echo "  Enumerates the solution file, every project (path, kind, packable),"
            echo "  and all README.md files. Complements codebase-stats.sh, which gives"
            echo "  names+LOC but not paths, the solution file, or the IsPackable flag."
            echo "  --json       Structured JSON output"
            echo "  --packable   Print only packable project csproj paths (one per line)"
            echo "  --readmes    Print only the README inventory (one per line)"
            exit 0
            ;;
        *) err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

# Solution file (relative to root)
solution=""
if sln="$(find_solution 2>/dev/null)"; then
    solution="${sln#"$ROOT"/}"
fi

# A project is packable iff its own .csproj sets <IsPackable>true</IsPackable>.
# (Directory.Build.props default is false.)
is_packable() {
    rg -q '<IsPackable>\s*true\s*</IsPackable>' "$1" 2>/dev/null
}

# Collect projects: path (relative), name, kind (src/test), packable.
declare -a proj_paths proj_names proj_kinds proj_packable
while IFS= read -r csproj; do
    [[ -z "$csproj" ]] && continue
    rel="${csproj#"$ROOT"/}"
    name="$(basename "$csproj" .csproj)"
    case "$rel" in
        tests/*) kind="test" ;;
        *)       kind="src" ;;
    esac
    if is_packable "$csproj"; then packable="true"; else packable="false"; fi
    proj_paths+=("$rel")
    proj_names+=("$name")
    proj_kinds+=("$kind")
    proj_packable+=("$packable")
done < <(fd -e csproj . "$ROOT/src" "$ROOT/tests" 2>/dev/null | sort)

# READMEs (relative)
mapfile -t readmes < <(fd README.md --type f "$ROOT" --exclude obj --exclude bin --exclude .git 2>/dev/null | sed "s#^$ROOT/##" | sort)

# ── --packable: csproj paths of packable projects only ──
if [[ "$PACKABLE_ONLY" == true ]]; then
    for i in "${!proj_paths[@]}"; do
        [[ "${proj_packable[$i]}" == "true" ]] && echo "${proj_paths[$i]}"
    done
    exit 0
fi

# ── --readmes: README inventory only ──
if [[ "$READMES_ONLY" == true ]]; then
    printf '%s\n' "${readmes[@]}"
    exit 0
fi

# ── JSON ──
if [[ "$JSON" == true ]]; then
    proj_json=$(for i in "${!proj_paths[@]}"; do
        jq -n \
            --arg name "${proj_names[$i]}" \
            --arg path "${proj_paths[$i]}" \
            --arg kind "${proj_kinds[$i]}" \
            --argjson packable "${proj_packable[$i]}" \
            '{name: $name, path: $path, kind: $kind, packable: $packable}'
    done | jq -s '.')

    readme_json=$(printf '%s\n' "${readmes[@]}" | jq -R '.' | jq -s '.')

    jq -n \
        --arg solution "$solution" \
        --argjson projects "$proj_json" \
        --argjson readmes "$readme_json" \
        '{solution: $solution, projects: $projects, readmes: $readmes}'
    exit 0
fi

# ── Human-readable ──
printf "${BOLD}Solution:${RESET} %s\n\n" "${solution:-<none>}"
printf "${BOLD}Projects:${RESET}\n"
for i in "${!proj_paths[@]}"; do
    tag=""
    [[ "${proj_packable[$i]}" == "true" ]] && tag=" ${GREEN}[packable]${RESET}"
    printf "  %-5s %s%b\n" "${proj_kinds[$i]}" "${proj_paths[$i]}" "$tag"
done
printf "\n${BOLD}READMEs:${RESET}\n"
for r in "${readmes[@]}"; do
    printf "  %s\n" "$r"
done
