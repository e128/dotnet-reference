#!/usr/bin/env bash
# Lode check: run the style lint and the size guard over lode files.
# Usage: lode-check.sh [--changed] [--json] [--help] [FILE...]
source "$(dirname "${BASH_SOURCE[0]}")/lode-lib.sh"

ROOT="$(find_repo_root)"
LINT="$ROOT/.claude/skills/tech-writing/scripts/tech-writing-lint.sh"
LIMIT="$LODE_FILE_LIMIT"
CHANGED=false; JSON=false; STRICT=false
FILES=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --changed) CHANGED=true ;;
        --json)    JSON=true ;;
        --strict)  STRICT=true ;;
        --help)
            echo "Usage: lode-check.sh [--changed] [--strict] [--json] [--help] [FILE...]"
            echo "  Run the STE style lint and the 250-line guard over lode files."
            echo ""
            echo "  --changed  Use the lode files modified in the working tree"
            echo "  --strict   Also fail on a style hit"
            echo "  --json     Print one JSON array with per-file results"
            echo ""
            echo "  A style hit is advisory by default. The size guard always fails."
            exit 0
            ;;
        -*)  err "Unknown flag: $1"; exit 1 ;;
        *)   FILES+=("$1") ;;
    esac
    shift
done

if [[ ! -x "$LINT" ]]; then
    err "Style lint not found: $LINT"
    exit 1
fi

if [[ "$CHANGED" == true ]]; then
    # A new lode file is untracked, so the diff output needs the ls-files union
    # to list it alongside the modified tracked files.
    while IFS= read -r f; do
        [[ -n "$f" ]] && FILES+=("$f")
    done < <(
        {
            git -C "$ROOT" diff --name-only --diff-filter=d HEAD -- 'lode' 2>/dev/null
            git -C "$ROOT" ls-files --others --exclude-standard -- 'lode' 2>/dev/null
        } | sort -u
    )
    if [[ ${#FILES[@]} -eq 0 ]]; then
        if [[ "$JSON" == true ]]; then
            printf '[]\n'
        else
            ok "No changed lode files"
        fi
        exit 0
    fi
fi

if [[ ${#FILES[@]} -eq 0 ]]; then
    err "No files specified. Use --changed or pass file paths."
    exit 1
fi

status=ok
first=true
[[ "$JSON" == true ]] && printf '['

for file in "${FILES[@]}"; do
    [[ "$file" != /* ]] && file="$ROOT/$file"
    rel="${file#"$ROOT"/}"

    if [[ ! -f "$file" ]]; then
        warn "File not found: $rel"
        status=fail
        continue
    fi

    # The lint exits non-zero on a hit, so the assignment needs its own guard.
    style_output="$("$LINT" "$file" 2>&1)" || true
    style_hits=$(grep -c '^[^:]*:[0-9]*:' <<<"$style_output" || true)
    lines=$(wc -l < "$file" | tr -d ' ')

    # Style hits predate this run on most lode files, so they stay advisory
    # unless --strict asks for them.
    file_status=ok
    if (( lines > LIMIT )) || { [[ "$STRICT" == true ]] && (( style_hits > 0 )); }; then
        file_status=fail
        status=fail
    fi

    if [[ "$JSON" == true ]]; then
        $first || printf ','
        first=false
        # jq does the string escaping, so a path with a quote stays valid JSON.
        printf '%s' "$(
            jq -n --arg file "$rel" --argjson lines "$lines" --argjson style_hits "$style_hits" \
                --arg status "$file_status" \
                '{file:$file,lines:$lines,style_hits:$style_hits,status:$status}'
        )"
    else
        case "$file_status" in
            ok)   ok "$rel: $lines lines, $style_hits style hit(s)" ;;
            fail) err "$rel: $lines lines, $style_hits style hit(s)" ;;
        esac
        if [[ -n "$style_output" ]]; then
            echo "$style_output"
        fi
    fi
done

[[ "$JSON" == true ]] && printf ']\n'

if [[ "$status" == ok ]]; then
    exit 0
fi

exit 1
