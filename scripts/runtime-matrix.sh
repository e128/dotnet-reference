#!/usr/bin/env bash
# Runtime pinning matrix: SDK pin, per-project target frameworks, Docker base images.
# Usage: runtime-matrix.sh [--json] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(find_repo_root)"
JSON=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json) JSON=true ;;
        --help)
            echo "Usage: runtime-matrix.sh [--json] [--help]"
            echo "  Collects the runtime-pinning matrix: .NET SDK pin + rollForward from global.json,"
            echo "  TargetFramework(s) per project, and Docker base-image FROM tags. Flags anything"
            echo "  unpinned or using :latest. Reuses sdk-version.sh and solution-inventory.sh."
            echo "  --json   Structured JSON output"
            exit 0
            ;;
        *) err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

declare -a flags

# ── SDK pin ──
sdk="$(bash "$SCRIPT_DIR/sdk-version.sh" --json | jq -r '.sdk // "unknown"')"
rollforward="$(jq -r '.sdk.rollForward // "none"' "$ROOT/global.json" 2>/dev/null || echo "none")"
if [[ "$sdk" == "unknown" || "$sdk" == "null" ]]; then
    flags+=("SDK not pinned in global.json")
fi
case "$rollforward" in
    major|latestMajor) flags+=("SDK rollForward is permissive: $rollforward") ;;
esac

# ── Target frameworks (per project; fall back to Directory.Build.props default) ──
default_tfm="$(rg -oN '<TargetFramework>([^<]+)</TargetFramework>' -r '$1' "$ROOT/Directory.Build.props" 2>/dev/null | head -1 || true)"
declare -a fw_proj fw_path fw_tfm fw_src
while IFS=$'\t' read -r pname ppath _pkind; do
    [[ -z "$pname" ]] && continue
    csproj="$ROOT/$ppath"
    [[ -f "$csproj" ]] || continue
    tfm="$(rg -oN '<TargetFrameworks?>([^<]+)</TargetFrameworks?>' -r '$1' "$csproj" 2>/dev/null | head -1 || true)"
    if [[ -n "$tfm" ]]; then
        src="csproj"
    else
        tfm="$default_tfm"
        src="Directory.Build.props"
    fi
    fw_proj+=("$pname"); fw_path+=("$ppath"); fw_tfm+=("${tfm:-unknown}"); fw_src+=("$src")
done < <(bash "$SCRIPT_DIR/solution-inventory.sh" --json | jq -r '.projects[] | [.name, .path, .kind] | @tsv')

# ── Docker base images ──
declare -a dk_file dk_image dk_tag dk_stage dk_status
while IFS= read -r dockerfile; do
    [[ -z "$dockerfile" ]] && continue
    rel="${dockerfile#"$ROOT"/}"
    declare -A seen_stages=()
    while IFS= read -r line; do
        # shellcheck disable=SC2086
        set -- $line               # FROM=$1 image=$2 [AS=$3 stage=$4]
        image="${2:-}"
        [[ -z "$image" ]] && continue
        stage=""
        [[ "${3:-}" == "AS" || "${3:-}" == "as" ]] && stage="${4:-}"
        if [[ -n "${seen_stages[$image]:-}" ]]; then
            # FROM references a prior build stage, not a registry image.
            tag=""
            status="stage-ref"
        elif [[ "$image" != *:* ]]; then
            tag=""
            status="unpinned"
            flags+=("$rel: '$image' has no tag (unpinned)")
        else
            tag="${image##*:}"
            if [[ "$tag" == "latest" ]]; then
                status="latest"
                flags+=("$rel: '$image' uses :latest")
            else
                status="pinned"
            fi
        fi
        [[ -n "$stage" ]] && seen_stages["$stage"]=1
        dk_file+=("$rel"); dk_image+=("$image"); dk_tag+=("$tag")
        dk_stage+=("$stage"); dk_status+=("$status")
    done < <(rg -N '^FROM ' "$dockerfile" 2>/dev/null)
done < <(fd -i -g 'Dockerfile*' "$ROOT" --exclude obj --exclude bin --exclude .git 2>/dev/null | sort)

# ── JSON ──
if [[ "$JSON" == true ]]; then
    fw_json="$(for i in "${!fw_proj[@]}"; do
        jq -n --arg project "${fw_proj[$i]}" --arg path "${fw_path[$i]}" \
              --arg framework "${fw_tfm[$i]}" --arg source "${fw_src[$i]}" \
              '{project: $project, path: $path, framework: $framework, source: $source}'
    done | jq -s '.')"

    dk_json="$(for i in "${!dk_file[@]}"; do
        jq -n --arg file "${dk_file[$i]}" --arg image "${dk_image[$i]}" \
              --arg tag "${dk_tag[$i]}" --arg stage "${dk_stage[$i]}" --arg status "${dk_status[$i]}" \
              '{file: $file, image: $image, tag: $tag, stage: $stage, status: $status}'
    done | jq -s '.')"

    flags_json="$(printf '%s\n' "${flags[@]}" | grep -v '^$' | jq -R '.' | jq -s '.')"

    jq -n \
        --arg sdk "$sdk" \
        --arg rollForward "$rollforward" \
        --argjson frameworks "${fw_json:-[]}" \
        --argjson docker "${dk_json:-[]}" \
        --argjson flags "${flags_json:-[]}" \
        '{sdk: {version: $sdk, rollForward: $rollForward}, frameworks: $frameworks, docker: $docker, flags: $flags}'
    exit 0
fi

# ── Human-readable ──
printf "${BOLD}SDK pin${RESET}\n"
printf "  version     : %s\n" "$sdk"
printf "  rollForward : %s\n\n" "$rollforward"

printf "${BOLD}Target frameworks${RESET}\n"
printf "  ${DIM}%-30s %-14s %s${RESET}\n" "PROJECT" "FRAMEWORK" "SOURCE"
for i in "${!fw_proj[@]}"; do
    printf "  %-30s %-14s %s\n" "${fw_proj[$i]}" "${fw_tfm[$i]}" "${fw_src[$i]}"
done
echo

printf "${BOLD}Docker base images${RESET}\n"
if [[ ${#dk_file[@]} -eq 0 ]]; then
    dim "  (no Dockerfile found)"
else
    printf "  ${DIM}%-14s %-12s %-45s %s${RESET}\n" "FILE" "STAGE" "IMAGE" "STATUS"
    for i in "${!dk_file[@]}"; do
        printf "  %-14s %-12s %-45s %s\n" "${dk_file[$i]}" "${dk_stage[$i]:-—}" "${dk_image[$i]}" "${dk_status[$i]}"
    done
fi
echo

printf "${BOLD}Flags${RESET}\n"
if [[ ${#flags[@]} -eq 0 ]]; then
    ok "all runtime versions pinned"
else
    for f in "${flags[@]}"; do warn "$f"; done
fi
