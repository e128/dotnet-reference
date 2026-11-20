#!/usr/bin/env bash
# Ban non-documentation comments in src/. XML doc comments stay allowed.
# Usage: comment-scan.sh [--init] [--json] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
BASELINE="$ROOT/comment-baseline.txt"
JSON=false
INIT=false
LIST=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --init) INIT=true ;;
        --json) JSON=true ;;
        --list) LIST=true ;;
        --help)
            echo "Usage: comment-scan.sh [--init] [--list] [--json] [--help]"
            echo "  Fail when src/*.cs carries a // or /* */ comment in a file"
            echo "  above its count in comment-baseline.txt."
            echo "  An XML /// doc comment and a #pragma justification are allowed."
            echo "  --init   Write the current per-file counts to the baseline"
            echo "  --list   Print every offending file, line number, and text"
            echo "  --json   Structured JSON output"
            exit 0
            ;;
        -*) err "Unknown flag: $1"; exit 1 ;;
        *)  err "comment-scan.sh takes no file arguments"; exit 1 ;;
    esac
    shift
done

# One comment line per file. The pattern matches a line whose first non-blank
# characters are // but not ///. A multi-line block comment is rare enough that
# the line rule catches its opening line.
count_file() {
    awk '
        /^[[:space:]]*\/\// && !/^[[:space:]]*\/\/\// { n++ }
        /^[[:space:]]*\/\*/ { n++ }
        END { print n + 0 }
    ' "$1"
}

declare -A COUNTS=()
FILES=()
while IFS= read -r rel; do
    [[ -z "$rel" ]] && continue
    n="$(count_file "$ROOT/$rel")"
    (( n == 0 )) && continue
    COUNTS["$rel"]=$n
    FILES+=("$rel")
done < <(git -C "$ROOT" ls-files --cached --others --exclude-standard '*.cs' | awk '/^src\//')

if [[ "$LIST" == true ]]; then
    for rel in "${FILES[@]}"; do
        awk -v f="$rel" '
            /^[[:space:]]*\/\// && !/^[[:space:]]*\/\/\// { printf "%s:%d:%s\n", f, NR, $0 }
            /^[[:space:]]*\/\*/ { printf "%s:%d:%s\n", f, NR, $0 }
        ' "$ROOT/$rel"
    done
    exit 0
fi

if [[ "$INIT" == true ]]; then
    {
        echo "# Per-file non-doc comment line counts allowed by comment-scan.sh."
        echo "# Lower a count by deleting comments. Never raise one without a reason."
        for rel in "${FILES[@]}"; do
            printf '%s\t%s\n' "${COUNTS[$rel]}" "$rel"
        done
    } > "$BASELINE"
    ok "Wrote ${#FILES[@]} file entries to comment-baseline.txt"
    exit 0
fi

declare -A ALLOWED=()
if [[ -f "$BASELINE" ]]; then
    while IFS=$'\t' read -r count rel; do
        [[ -z "$count" || "$count" == \#* ]] && continue
        ALLOWED["$rel"]="$count"
    done < "$BASELINE"
fi

VIOLATIONS=()
TOTAL=0
for rel in "${FILES[@]}"; do
    allowed="${ALLOWED[$rel]:-0}"
    n="${COUNTS[$rel]}"
    (( n <= allowed )) && continue
    TOTAL=$((TOTAL + n - allowed))
    VIOLATIONS+=("$rel: $n comment line(s), baseline allows $allowed")
done

if [[ "$JSON" == true ]]; then
    if (( TOTAL == 0 )); then
        json_object status=ok files="${#FILES[@]}" violations=0
    else
        details=$(printf '%s\n' "${VIOLATIONS[@]}" | jq -R . | jq -s .)
        printf '{"status":"fail","files":%d,"violations":%d,"details":%s}\n' \
            "${#FILES[@]}" "$TOTAL" "$details"
    fi
else
    if (( TOTAL == 0 )); then
        ok "No non-doc comments in src/"
    else
        err "$TOTAL non-doc comment line(s) above baseline:"
        for v in "${VIOLATIONS[@]}"; do
            printf '  %s\n' "$v"
        done
        dim "Move the rationale to lode/decisions/ and delete the comment."
    fi
fi

(( TOTAL == 0 )) && exit 0 || exit 1
