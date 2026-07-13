#!/usr/bin/env bash
# Central Package Management inventory: PackageVersion pins, direct PackageReference,
# ProjectReference edges. --orphans flags PackageVersion entries with no direct reference.
# Usage: deps-graph.sh [--orphans] [--json] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

JSON=false
ORPHANS_ONLY=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --orphans) ORPHANS_ONLY=true ;;
        --json)    JSON=true ;;
        --help)
            echo "Usage: deps-graph.sh [--orphans] [--json] [--help]"
            echo "  Inventories Central Package Management for a .NET solution:"
            echo "    - PackageVersion entries in Directory.Packages.props (with transitive-pin flag)"
            echo "    - direct PackageReference across all csproj + Directory.Build.props"
            echo "    - ProjectReference edge list"
            echo "  --orphans  PackageVersion entries with no matching direct PackageReference"
            echo "             (candidate unused; entries under a transitive-pin marker are flagged)"
            echo "  --json     Structured JSON output"
            exit 0
            ;;
        *) err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

ROOT="$(find_repo_root)"
PROPS="$ROOT/Directory.Packages.props"
BUILD_PROPS="$ROOT/Directory.Build.props"

if [[ ! -f "$PROPS" ]]; then
    err "No Directory.Packages.props found in $ROOT"
    exit 1
fi

# ── Project csproj paths (reuse solution-inventory) ──
INV="$("$(dirname "${BASH_SOURCE[0]}")/solution-inventory.sh" --json 2>/dev/null || echo '{}')"
declare -a CSPROJ_ABS=()
while IFS= read -r rel; do
    [[ -z "$rel" ]] && continue
    CSPROJ_ABS+=("$ROOT/$rel")
done < <(echo "$INV" | jq -r '.projects[].path' 2>/dev/null)

# ── Parse Directory.Packages.props: PackageVersion + transitive-pin flag ──
# A comment containing "transitive" marks the start of the transitive-pin section;
# every PackageVersion at or after it is flagged as a deliberate pin.
declare -a PV_NAMES=() PV_VERS=() PV_PINNED=()
in_transitive=false
while IFS= read -r line; do
    if [[ "$line" =~ \<!--(.*)--\> ]]; then
        comment="${BASH_REMATCH[1]}"
        shopt -s nocasematch
        [[ "$comment" == *transitive* ]] && in_transitive=true
        shopt -u nocasematch
        continue
    fi
    if [[ "$line" =~ PackageVersion[[:space:]]+Include=\"([^\"]+)\"[[:space:]]+Version=\"([^\"]+)\" ]]; then
        PV_NAMES+=("${BASH_REMATCH[1]}")
        PV_VERS+=("${BASH_REMATCH[2]}")
        if [[ "$in_transitive" == true ]]; then PV_PINNED+=("true"); else PV_PINNED+=("false"); fi
    fi
done < "$PROPS"

# ── Direct PackageReference across csproj + Directory.Build.props ──
declare -a REF_FILES=("${CSPROJ_ABS[@]}")
[[ -f "$BUILD_PROPS" ]] && REF_FILES+=("$BUILD_PROPS")
mapfile -t DIRECT_REFS < <(
    rg -oN --no-filename -r '$1' 'PackageReference Include="([^"]+)"' "${REF_FILES[@]}" 2>/dev/null | sort -u
)

is_referenced() {
    local name="$1"
    for ref in "${DIRECT_REFS[@]}"; do
        [[ "$ref" == "$name" ]] && return 0
    done
    return 1
}

# ── ProjectReference edges ──
# from = referencing csproj basename; to = referenced csproj basename.
declare -a EDGE_FROM=() EDGE_TO=()
if [[ ${#CSPROJ_ABS[@]} -gt 0 ]]; then
    while IFS= read -r hit; do
        [[ -z "$hit" ]] && continue
        src="${hit%%:*}"
        inc="${hit#*:}"
        from="$(basename "$src" .csproj)"
        inc="${inc//\\//}"                 # normalize Windows separators
        to="$(basename "$inc" .csproj)"
        EDGE_FROM+=("$from")
        EDGE_TO+=("$to")
    done < <(rg -oN -r '$1' 'ProjectReference Include="([^"]+)"' "${CSPROJ_ABS[@]}" 2>/dev/null)
fi

# ── Build orphan list ──
declare -a ORPH_NAMES=() ORPH_PINNED=()
for i in "${!PV_NAMES[@]}"; do
    if ! is_referenced "${PV_NAMES[$i]}"; then
        ORPH_NAMES+=("${PV_NAMES[$i]}")
        ORPH_PINNED+=("${PV_PINNED[$i]}")
    fi
done

# ── JSON output ──
if [[ "$JSON" == true ]]; then
    if [[ "$ORPHANS_ONLY" == true ]]; then
        orph_json=$(for i in "${!ORPH_NAMES[@]}"; do
            jq -n --arg name "${ORPH_NAMES[$i]}" --argjson pinned "${ORPH_PINNED[$i]}" \
                '{name: $name, transitive_pin: $pinned}'
        done | jq -s '.')
        jq -n --argjson orphans "${orph_json:-[]}" '{orphans: $orphans}'
        exit 0
    fi
    pv_json=$(for i in "${!PV_NAMES[@]}"; do
        jq -n --arg name "${PV_NAMES[$i]}" --arg version "${PV_VERS[$i]}" \
            --argjson pinned "${PV_PINNED[$i]}" \
            '{name: $name, version: $version, transitive_pin: $pinned}'
    done | jq -s '.')
    ref_json=$(printf '%s\n' "${DIRECT_REFS[@]}" | jq -R '.' | jq -s 'map(select(length > 0))')
    edge_json=$(for i in "${!EDGE_FROM[@]}"; do
        jq -n --arg from "${EDGE_FROM[$i]}" --arg to "${EDGE_TO[$i]}" '{from: $from, to: $to}'
    done | jq -s '.')
    orph_json=$(for i in "${!ORPH_NAMES[@]}"; do
        jq -n --arg name "${ORPH_NAMES[$i]}" --argjson pinned "${ORPH_PINNED[$i]}" \
            '{name: $name, transitive_pin: $pinned}'
    done | jq -s '.')
    jq -n \
        --argjson package_versions "${pv_json:-[]}" \
        --argjson direct_references "${ref_json:-[]}" \
        --argjson project_edges "${edge_json:-[]}" \
        --argjson orphans "${orph_json:-[]}" \
        '{package_versions: $package_versions, direct_references: $direct_references, project_edges: $project_edges, orphans: $orphans}'
    exit 0
fi

# ── Human-readable ──
if [[ "$ORPHANS_ONLY" == true ]]; then
    if [[ ${#ORPH_NAMES[@]} -eq 0 ]]; then
        ok "No orphaned PackageVersion entries"
        exit 0
    fi
    printf "${BOLD}Orphaned PackageVersion entries (no direct PackageReference):${RESET}\n"
    for i in "${!ORPH_NAMES[@]}"; do
        tag=""
        [[ "${ORPH_PINNED[$i]}" == "true" ]] && tag=" ${YELLOW}[transitive-pin marker]${RESET}"
        printf "  %s%b\n" "${ORPH_NAMES[$i]}" "$tag"
    done
    exit 0
fi

printf "${BOLD}PackageVersion entries (%d):${RESET}\n" "${#PV_NAMES[@]}"
for i in "${!PV_NAMES[@]}"; do
    tag=""
    [[ "${PV_PINNED[$i]}" == "true" ]] && tag=" ${DIM}[pin]${RESET}"
    printf "  %-50s %s%b\n" "${PV_NAMES[$i]}" "${PV_VERS[$i]}" "$tag"
done
printf "\n${BOLD}Direct PackageReference (%d):${RESET}\n" "${#DIRECT_REFS[@]}"
for r in "${DIRECT_REFS[@]}"; do
    [[ -z "$r" ]] && continue
    printf "  %s\n" "$r"
done
printf "\n${BOLD}ProjectReference edges (%d):${RESET}\n" "${#EDGE_FROM[@]}"
for i in "${!EDGE_FROM[@]}"; do
    printf "  %s -> %s\n" "${EDGE_FROM[$i]}" "${EDGE_TO[$i]}"
done
printf "\n${BOLD}Orphan candidates (%d):${RESET}\n" "${#ORPH_NAMES[@]}"
for i in "${!ORPH_NAMES[@]}"; do
    tag=""
    [[ "${ORPH_PINNED[$i]}" == "true" ]] && tag=" ${YELLOW}[transitive-pin marker]${RESET}"
    printf "  %s%b\n" "${ORPH_NAMES[$i]}" "$tag"
done
