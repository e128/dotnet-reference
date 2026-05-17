#!/usr/bin/env bash
# Read SDK version from global.json.
# Usage: sdk-version.sh [--json] [--path FILE]
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

JSON=false
FILE=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json)  JSON=true; shift ;;
        --path)  FILE="$2"; shift 2 ;;
        -h|--help)
            echo "Usage: sdk-version.sh [--json] [--path FILE]"
            exit 0 ;;
        *) err "Unknown option: $1"; exit 1 ;;
    esac
done

ROOT="$(find_repo_root)"
[[ -z "$FILE" ]] && FILE="$ROOT/global.json"

if [[ ! -f "$FILE" ]]; then
    if $JSON; then
        jq -n --arg path "$FILE" '{sdk: null, path: $path, exists: false}'
    else
        echo "SDK: not found ($FILE missing)"
    fi
    exit 0
fi

VERSION="$(jq -r '.sdk.version // "unknown"' "$FILE" 2>/dev/null || echo "unknown")"

if $JSON; then
    jq -n --arg sdk "$VERSION" --arg path "$FILE" '{sdk: $sdk, path: $path, exists: true}'
else
    echo "SDK: $VERSION"
fi
