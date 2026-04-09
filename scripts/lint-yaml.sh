#!/usr/bin/env bash
# Validate YAML files for syntax errors and common issues.
# Usage: lint-yaml.sh [--fix] [--json] [FILE...]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
FIX=false; JSON=false
FILES=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --fix)  FIX=true ;;
        --json) JSON=true ;;
        -*)     err "Unknown flag: $1"; exit 1 ;;
        *)      FILES+=("$1") ;;
    esac
    shift
done

# Auto-discover YAML files if none specified
if [[ ${#FILES[@]} -eq 0 ]]; then
    while IFS= read -r f; do FILES+=("$f"); done < <(fd -e yml -e yaml . "$ROOT" --hidden --exclude node_modules --exclude artifacts --exclude .git 2>/dev/null)
fi

if [[ ${#FILES[@]} -eq 0 ]]; then
    [[ "$JSON" == true ]] && json_object status=ok files=0 errors=0 || ok "No YAML files found"
    exit 0
fi

TOTAL=0; ERRORS=0; ERROR_DETAILS=()

for file in "${FILES[@]}"; do
    [[ "$file" != /* ]] && file="$ROOT/$file"
    [[ ! -f "$file" ]] && continue
    TOTAL=$((TOTAL + 1))
    rel="${file#$ROOT/}"

    # Phase 1: Basic syntax check with python yaml module (if available)
    if command -v python3 &>/dev/null && python3 -c "import yaml" &>/dev/null; then
        result=$(python3 -c "
import yaml, sys
try:
    with open('$file') as f:
        list(yaml.safe_load_all(f))
    sys.exit(0)
except yaml.YAMLError as e:
    print(str(e)[:200])
    sys.exit(1)
" 2>&1) || {
            ERRORS=$((ERRORS + 1))
            ERROR_DETAILS+=("$rel: $result")
            continue
        }
    fi

    # Phase 2: yamllint if available (stricter)
    if command -v yamllint &>/dev/null; then
        result=$(yamllint -d relaxed "$file" 2>&1) || {
            # Only count as error if yamllint finds actual errors (not warnings)
            if echo "$result" | grep -q "error"; then
                ERRORS=$((ERRORS + 1))
                first_error=$(echo "$result" | grep "error" | head -1)
                ERROR_DETAILS+=("$rel: $first_error")
            fi
        }
    fi

    # Phase 3: GitHub Actions-specific validation
    if [[ "$rel" == .github/workflows/* ]]; then
        # Check for required top-level keys
        for key in "on" "jobs"; do
            if ! grep -q "^${key}:" "$file" 2>/dev/null; then
                ERRORS=$((ERRORS + 1))
                ERROR_DETAILS+=("$rel: missing required key '$key'")
            fi
        done

        # Check for uses: without version pinning
        unpinned=$(grep -n 'uses:.*@' "$file" 2>/dev/null | grep -v '@v[0-9]\|@[a-f0-9]\{40\}\|@main\|@master' || true)
        if [[ -n "$unpinned" ]]; then
            while IFS= read -r line; do
                ERROR_DETAILS+=("$rel:$line (unpinned action version)")
            done <<< "$unpinned"
        fi
    fi

    # Phase 4: docker-compose validation
    if [[ "$rel" == *docker-compose* || "$rel" == *compose.yml || "$rel" == *compose.yaml ]]; then
        if ! grep -q "^services:" "$file" 2>/dev/null; then
            ERRORS=$((ERRORS + 1))
            ERROR_DETAILS+=("$rel: missing 'services:' key")
        fi
    fi
done

if [[ "$JSON" == true ]]; then
    printf '{"status":"%s","files":%d,"errors":%d}\n' \
        "$([ $ERRORS -eq 0 ] && echo ok || echo fail)" "$TOTAL" "$ERRORS"
else
    if [[ $ERRORS -eq 0 ]]; then
        ok "All $TOTAL YAML files valid"
    else
        err "$ERRORS error(s) in $TOTAL YAML files:"
        for detail in "${ERROR_DETAILS[@]}"; do
            printf "  %s\n" "$detail"
        done
    fi
fi

[[ $ERRORS -eq 0 ]] && exit 0 || exit 1
