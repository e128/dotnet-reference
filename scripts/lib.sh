#!/usr/bin/env bash
# Shared library for E128.Reference bash scripts.
# Source this file: source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

set -euo pipefail

# ── Colors ───────────────────────────────────────
if [[ -t 1 ]]; then
    RED='\033[0;31m'
    GREEN='\033[0;32m'
    YELLOW='\033[0;33m'
    BLUE='\033[0;34m'
    DIM='\033[2m'
    BOLD='\033[1m'
    RESET='\033[0m'
else
    RED='' GREEN='' YELLOW='' BLUE='' DIM='' BOLD='' RESET=''
fi

# ── Logging ──────────────────────────────────────
info()  { printf "${BLUE}▸${RESET} %s\n" "$*"; }
ok()    { printf "${GREEN}✓${RESET} %s\n" "$*"; }
warn()  { printf "${YELLOW}⚠${RESET} %s\n" "$*" >&2; }
err()   { printf "${RED}✗${RESET} %s\n" "$*" >&2; }
dim()   { printf "${DIM}%s${RESET}\n" "$*"; }

# ── Solution discovery ───────────────────────────
# Find the .slnx file relative to the repo root.
find_solution() {
    local root
    root="$(find_repo_root)"
    local slnx
    slnx="$(fd -e slnx --max-depth 1 . "$root" 2>/dev/null | head -1)"
    if [[ -z "$slnx" ]]; then
        err "No .slnx file found in $root"
        return 1
    fi
    echo "$slnx"
}

# Find the repo root (directory containing .git/)
find_repo_root() {
    git rev-parse --show-toplevel 2>/dev/null || {
        err "Not inside a git repository"
        return 1
    }
}

# ── JSON helpers ─────────────────────────────────
# Emit a JSON object from key=value pairs.
# Usage: json_object status=ok count=5 message="hello world"
json_object() {
    local first=true
    printf '{'
    for pair in "$@"; do
        local key="${pair%%=*}"
        local val="${pair#*=}"
        if [[ "$first" == true ]]; then
            first=false
        else
            printf ','
        fi
        # Auto-detect numbers and booleans
        if [[ "$val" =~ ^[0-9]+$ ]]; then
            printf '"%s":%s' "$key" "$val"
        elif [[ "$val" == "true" || "$val" == "false" || "$val" == "null" ]]; then
            printf '"%s":%s' "$key" "$val"
        else
            printf '"%s":"%s"' "$key" "$(echo "$val" | sed 's/"/\\"/g')"
        fi
    done
    printf '}\n'
}

# ── Argument parsing helpers ─────────────────────
# Check if a flag is present in args.
has_flag() {
    local flag="$1"
    shift
    for arg in "$@"; do
        [[ "$arg" == "$flag" ]] && return 0
    done
    return 1
}

# Get a named option value from args (e.g., --project foo).
get_option() {
    local name="$1"
    shift
    while [[ $# -gt 0 ]]; do
        if [[ "$1" == "$name" && $# -gt 1 ]]; then
            echo "$2"
            return 0
        fi
        shift
    done
    return 1
}

# ── Project resolution ───────────────────────────
# Fuzzy-match a project name against src/ and tests/ directories.
resolve_project() {
    local name="$1"
    local root
    root="$(find_repo_root)"
    local match
    match="$(fd -t d --max-depth 2 "$name" "$root/src" "$root/tests" 2>/dev/null | head -1)"
    if [[ -n "$match" ]]; then
        fd -e csproj --max-depth 1 . "$match" 2>/dev/null | head -1
    fi
}

# ── Changed files ────────────────────────────────
# Get list of .cs files changed in the working tree (staged + unstaged).
changed_cs_files() {
    local root
    root="$(find_repo_root)"
    {
        git -C "$root" diff --name-only --diff-filter=d HEAD -- '*.cs' 2>/dev/null
        git -C "$root" diff --name-only --diff-filter=d --cached HEAD -- '*.cs' 2>/dev/null
    } | sort -u
}

# ── Timestamp ────────────────────────────────────
iso_timestamp() {
    date -u +"%Y-%m-%dT%H:%M:%SZ"
}
