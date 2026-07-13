#!/usr/bin/env bash
# NuGet dependency heat map: classify packages and map cross-project sharing.
# Usage: nuget-heat-map.sh [--json] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(find_repo_root)"
JSON=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json) JSON=true ;;
        --help)
            echo "Usage: nuget-heat-map.sh [--json] [--help]"
            echo "  Scans every project's PackageReference entries plus Directory.Packages.props,"
            echo "  classifies each package (Microsoft / first-party E128 / third-party), and builds"
            echo "  a cross-project shared-dependency heat map (packages referenced by 2+ projects)."
            echo "  Reuses solution-inventory.sh --json for the canonical project list."
            echo "  --json   Structured JSON output"
            exit 0
            ;;
        *) err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

classify_pkg() {
    case "$1" in
        Microsoft.*|System.*|Azure.*) echo "microsoft" ;;
        E128.*|E128*)                 echo "first-party" ;;
        *)                            echo "third-party" ;;
    esac
}

# ── Central Package Management version map ──
declare -A pkg_version
props="$ROOT/Directory.Packages.props"
if [[ -f "$props" ]]; then
    while IFS='=' read -r name ver; do
        [[ -z "$name" ]] && continue
        pkg_version["$name"]="$ver"
    done < <(rg -oN 'PackageVersion Include="([^"]+)" Version="([^"]+)"' -r '$1=$2' "$props" 2>/dev/null)
fi

# ── Scan each project's PackageReference entries ──
declare -A pkg_count pkg_projects
while IFS=$'\t' read -r pname ppath _pkind; do
    [[ -z "$pname" ]] && continue
    csproj="$ROOT/$ppath"
    [[ -f "$csproj" ]] || continue
    while IFS= read -r pkg; do
        [[ -z "$pkg" ]] && continue
        pkg_count["$pkg"]=$(( ${pkg_count["$pkg"]:-0} + 1 ))
        pkg_projects["$pkg"]+="$pname "
    done < <(rg -oN 'PackageReference Include="([^"]+)"' -r '$1' "$csproj" 2>/dev/null | sort -u)
done < <(bash "$SCRIPT_DIR/solution-inventory.sh" --json | jq -r '.projects[] | [.name, .path, .kind] | @tsv')

# ── Classification tallies over unique referenced packages ──
declare -A class_count=([microsoft]=0 [first-party]=0 [third-party]=0)
for pkg in "${!pkg_count[@]}"; do
    cls="$(classify_pkg "$pkg")"
    class_count["$cls"]=$(( class_count["$cls"] + 1 ))
done

# ── Build per-package JSON, sorted by descending count then name ──
pkgs_json="$(
    for pkg in "${!pkg_count[@]}"; do
        projs_json="$(echo "${pkg_projects[$pkg]}" | tr ' ' '\n' | sort -u | grep -v '^$' | jq -R '.' | jq -s '.')"
        jq -n \
            --arg name "$pkg" \
            --arg version "${pkg_version[$pkg]:-}" \
            --arg class "$(classify_pkg "$pkg")" \
            --argjson count "${pkg_count[$pkg]}" \
            --argjson projects "$projs_json" \
            '{name: $name, version: $version, class: $class, count: $count, projects: $projects}'
    done | jq -s 'sort_by(-.count, .name)'
)"

# ── JSON ──
if [[ "$JSON" == true ]]; then
    jq -n \
        --argjson microsoft "${class_count[microsoft]}" \
        --argjson firstparty "${class_count[first-party]}" \
        --argjson thirdparty "${class_count[third-party]}" \
        --argjson packages "$pkgs_json" \
        '{classification: {microsoft: $microsoft, "first-party": $firstparty, "third-party": $thirdparty},
          total: ($packages | length),
          packages: $packages}'
    exit 0
fi

# ── Human-readable ──
printf "${BOLD}NuGet classification${RESET} (unique referenced packages: %s)\n" "$(echo "$pkgs_json" | jq 'length')"
printf "  Microsoft/Azure : %s\n" "${class_count[microsoft]}"
printf "  First-party     : %s\n" "${class_count[first-party]}"
printf "  Third-party     : %s\n\n" "${class_count[third-party]}"

printf "${BOLD}Shared dependency heat map${RESET} (referenced by 2+ projects)\n"
shared="$(echo "$pkgs_json" | jq -r '.[] | select(.count >= 2) | [.count, .name, (.projects | join(", "))] | @tsv')"
if [[ -z "$shared" ]]; then
    dim "  (no package is shared across 2+ projects)"
else
    printf "  ${DIM}%-5s %-45s %s${RESET}\n" "COUNT" "PACKAGE" "PROJECTS"
    while IFS=$'\t' read -r c n p; do
        printf "  %-5s %-45s %s\n" "$c" "$n" "$p"
    done <<< "$shared"
fi
