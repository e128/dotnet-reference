#!/usr/bin/env bash
# Classify each changed file in the working diff as MECHANICAL (namespace rename) or SUBSTANTIVE.
# Usage: mechanical-diff.sh [--json]
source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

JSON=false
for arg in "$@"; do
    case "$arg" in
        --json) JSON=true ;;
        --help | -h)
            echo "Usage: mechanical-diff.sh [--json]"
            echo "Classify each changed file in the working diff (vs HEAD) as MECHANICAL or SUBSTANTIVE."
            echo "MECHANICAL: every changed token is a pure namespace-prefix substitution (dotted identifier)."
            echo "SUBSTANTIVE: anything else."
            exit 0
            ;;
        *)
            err "Unknown flag: $arg"
            exit 1
            ;;
    esac
done

ROOT="$(find_repo_root)"

# A namespace path is a dotted identifier: Foo.Bar or Foo.Bar.Baz (>=2 segments).
NS_RE='^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)+$'

# Classify a single file's working diff. Echoes MECHANICAL or SUBSTANTIVE.
classify_file() {
    local file="$1"
    local wd changed=false line tok
    # Word-diff isolates exactly the changed tokens; regex defines a word as an
    # identifier path so punctuation/whitespace are separators, not tokens.
    wd="$(git -C "$ROOT" diff --word-diff=porcelain \
        --word-diff-regex='[A-Za-z_][A-Za-z0-9_.]*' --unified=0 -- "$file" 2>/dev/null)"

    while IFS= read -r line; do
        case "$line" in
            'diff '* | 'index '* | '--- '* | '+++ '* | '@@'* | '~') continue ;;
            -*) tok="${line#-}" ;;
            +*) tok="${line#+}" ;;
            *) continue ;;
        esac
        [[ -z "$tok" ]] && continue
        changed=true
        # Any changed token that is not a dotted namespace path => substantive.
        if [[ ! "$tok" =~ $NS_RE ]]; then
            echo "SUBSTANTIVE"
            return
        fi
    done <<<"$wd"

    if [[ "$changed" == true ]]; then
        echo "MECHANICAL"
    else
        # No changed identifier tokens (e.g. whitespace-only) — not a rename.
        echo "SUBSTANTIVE"
    fi
}

mapfile -t FILES < <(git -C "$ROOT" diff --name-only --diff-filter=d HEAD 2>/dev/null | sort -u)

mech=0
subs=0
json_entries=""

for file in "${FILES[@]}"; do
    [[ -z "$file" ]] && continue
    cls="$(classify_file "$file")"
    if [[ "$cls" == "MECHANICAL" ]]; then
        ((mech++)) || true
    else
        ((subs++)) || true
    fi

    if $JSON; then
        entry="$(printf '{"path":"%s","class":"%s"}' "$file" "$cls")"
        if [[ -n "$json_entries" ]]; then
            json_entries="$json_entries,$entry"
        else
            json_entries="$entry"
        fi
    else
        printf "%-12s %s\n" "$cls" "$file"
    fi
done

if $JSON; then
    printf '{"files":[%s],"mechanical":%d,"substantive":%d}\n' "$json_entries" "$mech" "$subs"
else
    ok "$mech mechanical, $subs substantive"
fi
