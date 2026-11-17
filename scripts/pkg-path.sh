#!/usr/bin/env bash
# Resolve a NuGet package's ref and lib directories and their assemblies.
# Usage: pkg-path.sh PACKAGE [--version V] [--ref|--lib] [--json]
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

PACKAGE=""
VERSION=""
KIND=""
JSON=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --version) VERSION="$2"; shift 2 ;;
        --ref)     KIND="ref"; shift ;;
        --lib)     KIND="lib"; shift ;;
        --json)    JSON=true; shift ;;
        -h|--help)
            echo "Usage: pkg-path.sh PACKAGE [--version V] [--ref|--lib] [--json]"
            echo "Prints the package's ref and lib directories with their assemblies."
            echo "Without --version it selects the highest installed version."
            exit 0 ;;
        -*)  err "Unknown option: $1"; exit 1 ;;
        *)   PACKAGE="$1"; shift ;;
    esac
done

if [[ -z "$PACKAGE" ]]; then
    err "Usage: pkg-path.sh PACKAGE [--version V] [--ref|--lib] [--json]"
    exit 1
fi

ROOT="${NUGET_PACKAGES:-$HOME/.nuget/packages}/$(printf '%s' "$PACKAGE" | tr '[:upper:]' '[:lower:]')"
if [[ ! -d "$ROOT" ]]; then
    err "Package not found: $PACKAGE"
    dim "Looked in $ROOT"
    exit 1
fi

if [[ -z "$VERSION" ]]; then
    VERSION="$(find "$ROOT" -mindepth 1 -maxdepth 1 -type d -exec basename {} \; | sort -V | tail -1)"
fi
if [[ -z "$VERSION" || ! -d "$ROOT/$VERSION" ]]; then
    err "Version not found: ${VERSION:-<none>} in $ROOT"
    exit 1
fi

KINDS=(ref lib)
if [[ -n "$KIND" ]]; then
    KINDS=("$KIND")
fi

DIRS=()
ASSEMBLIES=()
for kind in "${KINDS[@]}"; do
    for dir in "$ROOT/$VERSION/$kind"/*/; do
        dir="${dir%/}"
        [[ -d "$dir" ]] || continue
        DIRS+=("$dir")
        while IFS= read -r dll; do
            ASSEMBLIES+=("$dll")
        done < <(find "$dir" -maxdepth 1 -name '*.dll' -type f 2> /dev/null | sort)
    done
done

if [[ ${#DIRS[@]} -eq 0 ]]; then
    err "No ${KIND:-ref or lib} directory in $ROOT/$VERSION"
    exit 1
fi

if $JSON; then
    dirs_json="$(printf '%s\n' "${DIRS[@]}" | jq -R . | jq -s .)"
    asm_json='[]'
    if [[ ${#ASSEMBLIES[@]} -gt 0 ]]; then
        asm_json="$(printf '%s\n' "${ASSEMBLIES[@]}" | jq -R . | jq -s .)"
    fi
    jq -n \
        --arg package "$PACKAGE" \
        --arg version "$VERSION" \
        --argjson dirs "$dirs_json" \
        --argjson assemblies "$asm_json" \
        '{package: $package, version: $version, dirs: $dirs, assemblies: $assemblies}'
else
    ok "$PACKAGE $VERSION"
    for dir in "${DIRS[@]}"; do
        echo "$dir"
        if [[ ${#ASSEMBLIES[@]} -gt 0 ]]; then
            for dll in "${ASSEMBLIES[@]}"; do
                if [[ "$(dirname "$dll")" == "$dir" ]]; then
                    dim "  $(basename "$dll")"
                fi
            done
        fi
    done
fi
