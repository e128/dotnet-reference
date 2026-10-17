#!/usr/bin/env bash
# Lint shell scripts with shellcheck. Fails on any warning or error.
# Usage: lint-shell.sh [--json] [--help] [FILE...]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
JSON=false
FILES=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json) JSON=true ;;
        --help)
            echo "Usage: lint-shell.sh [--json] [--help] [FILE...]"
            echo "  Lint shell scripts with shellcheck at warning severity."
            echo "  The default scope is every tracked .sh file in the repository."
            echo "  --json   Structured JSON output"
            exit 0
            ;;
        -*) err "Unknown flag: $1"; exit 1 ;;
        *)  FILES+=("$1") ;;
    esac
    shift
done

if ! command -v shellcheck >/dev/null 2>&1; then
    err "shellcheck is required but not installed. Run: brew install shellcheck"
    exit 1
fi

# Auto-discover shell files if none specified. `git ls-files` covers the tracked
# tree and the new files you have not staged yet. It skips the ignored scratch
# directories and it needs no `fd` on a bare CI runner.
if [[ ${#FILES[@]} -eq 0 ]]; then
    while IFS= read -r f; do
        [[ -n "$f" ]] && FILES+=("$ROOT/$f")
    done < <(git -C "$ROOT" ls-files --cached --others --exclude-standard '*.sh' 2>/dev/null)
fi

if [[ ${#FILES[@]} -eq 0 ]]; then
    [[ "$JSON" == true ]] && json_object status=ok files=0 findings=0 || ok "No shell scripts found"
    exit 0
fi

# One shellcheck process for the whole list. `-S warning` hides the note-level
# findings, so SC1091 needs no suppression here.
RAW="$(shellcheck -S warning -f gcc "${FILES[@]}" 2>&1)" || true

FINDING_COUNT=0
DETAILS=()
if [[ -n "$RAW" ]]; then
    while IFS= read -r line; do
        [[ -z "$line" ]] && continue
        FINDING_COUNT=$((FINDING_COUNT + 1))
        DETAILS+=("${line#"$ROOT"/}")
    done <<< "$RAW"
fi

if [[ "$JSON" == true ]]; then
    if [[ $FINDING_COUNT -eq 0 ]]; then
        json_object status=ok files="${#FILES[@]}" findings=0
    else
        details_json=$(printf '%s\n' "${DETAILS[@]}" | jq -R . | jq -s .)
        printf '{"status":"fail","files":%d,"findings":%d,"details":%s}\n' \
            "${#FILES[@]}" "$FINDING_COUNT" "$details_json"
    fi
else
    if [[ $FINDING_COUNT -eq 0 ]]; then
        ok "All ${#FILES[@]} shell scripts pass shellcheck"
    else
        err "shellcheck found $FINDING_COUNT finding(s) in ${#FILES[@]} shell scripts:"
        for detail in "${DETAILS[@]}"; do
            printf "  %s\n" "$detail"
        done
    fi
fi

[[ $FINDING_COUNT -eq 0 ]] && exit 0 || exit 1
