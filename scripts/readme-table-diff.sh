#!/usr/bin/env bash
# Detect drift between scripts/README.md and the actual scripts on disk.
# Usage: readme-table-diff.sh [--json] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
SCRIPTS_DIR="$ROOT/scripts"
README="$SCRIPTS_DIR/README.md"
JSON=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json) JSON=true ;;
        --help)
            echo "Usage: readme-table-diff.sh [--json] [--help]"
            echo "  Compares the set of script names documented in scripts/README.md"
            echo "  against the scripts actually present on disk (public + internal)."
            echo "  Pure set-diff — no LLM judgment needed."
            echo "  --json   Structured output: {missing_from_readme, extra_in_readme}"
            exit 0
            ;;
        *) err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

if [[ ! -f "$README" ]]; then
    err "scripts/README.md not found at $README"
    exit 1
fi

# Actual scripts on disk (public + internal), excluding help.sh and lib.sh.
mapfile -t actual < <(
    {
        fd -e sh --max-depth 1 . "$SCRIPTS_DIR" 2>/dev/null
        fd -e sh --max-depth 1 . "$SCRIPTS_DIR/internal" 2>/dev/null
    } | xargs -n1 basename | grep -Ev '^(help|lib)\.sh$' | sort -u
)

# Script names documented in the README — only backtick-wrapped `name.sh` tokens
# (optionally prefixed with internal/), normalized to basenames. This is how the
# README references real scripts, so URL/prose false positives (e.g. www.nushell.sh)
# are excluded.
mapfile -t documented < <(rg -o -r '$1' '`(?:internal/)?([a-z0-9][a-z0-9-]*\.sh)`' "$README" 2>/dev/null | grep -Ev '^(help|lib)\.sh$' | sort -u)

# Diffs.
mapfile -t missing < <(comm -23 <(printf '%s\n' "${actual[@]}") <(printf '%s\n' "${documented[@]}"))
mapfile -t extra   < <(comm -13 <(printf '%s\n' "${actual[@]}") <(printf '%s\n' "${documented[@]}"))

if [[ "$JSON" == true ]]; then
    missing_json=$(printf '%s\n' "${missing[@]}" | jq -R 'select(length>0)' | jq -s '.')
    extra_json=$(printf '%s\n' "${extra[@]}" | jq -R 'select(length>0)' | jq -s '.')
    jq -n \
        --argjson missing "$missing_json" \
        --argjson extra "$extra_json" \
        '{missing_from_readme: $missing, extra_in_readme: $extra,
          drift: (($missing | length) + ($extra | length) > 0)}'
    exit 0
fi

if [[ ${#missing[@]} -eq 0 && ${#extra[@]} -eq 0 ]]; then
    ok "scripts/README.md is in sync — no drift"
    exit 0
fi

if [[ ${#missing[@]} -gt 0 ]]; then
    warn "On disk but missing from README:"
    printf '  %s\n' "${missing[@]}"
fi
if [[ ${#extra[@]} -gt 0 ]]; then
    warn "In README but not on disk:"
    printf '  %s\n' "${extra[@]}"
fi
