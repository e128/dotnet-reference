#!/usr/bin/env bash
# Decide code-review diff delivery mode by size: inline (<=30KB), write (30-40KB), or split (>40KB).
# Usage: cr-diff-deliver.sh [--json] <difffile>
source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

JSON=false
DIFFFILE=""
for arg in "$@"; do
    case "$arg" in
        --json) JSON=true ;;
        --help | -h)
            echo "Usage: cr-diff-deliver.sh [--json] <difffile>"
            echo "Decide delivery mode by size and, when writing/splitting, emit the produced path(s):"
            echo "  <=30KB    -> inline  (pass diff inline in the agent prompt)"
            echo "  30-40KB   -> write   (copy to .claude/tmp/cr-<name>.diff)"
            echo "  >40KB     -> split   (slice at file boundaries into cr-<name>-N.diff)"
            exit 0
            ;;
        -*)
            err "Unknown flag: $arg"
            exit 1
            ;;
        *) DIFFFILE="$arg" ;;
    esac
done

if [[ -z "$DIFFFILE" ]]; then
    err "No diff file given. Usage: cr-diff-deliver.sh [--json] <difffile>"
    exit 1
fi
if [[ ! -f "$DIFFFILE" ]]; then
    err "Diff file not found: $DIFFFILE"
    exit 1
fi

ROOT="$(find_repo_root)"
TMPDIR="$ROOT/.claude/tmp"
mkdir -p "$TMPDIR"

INLINE_MAX=$((30 * 1024)) # 30720
SPLIT_MAX=$((40 * 1024))  # 40960

name="$(basename "$DIFFFILE")"
name="${name%.diff}"
name="${name%.txt}"

size="$(wc -c <"$DIFFFILE" | tr -d ' ')"

emit() {
    local mode="$1"
    shift
    if $JSON; then
        local paths=""
        for p in "$@"; do
            [[ -n "$paths" ]] && paths="$paths,"
            paths="$paths\"$p\""
        done
        printf '{"mode":"%s","size":%d,"paths":[%s]}\n' "$mode" "$size" "$paths"
    else
        echo "$mode"
        for p in "$@"; do echo "$p"; done
    fi
}

# ── inline ───────────────────────────────────────
if ((size <= INLINE_MAX)); then
    emit inline
    exit 0
fi

# ── write ────────────────────────────────────────
if ((size <= SPLIT_MAX)); then
    out="$TMPDIR/cr-$name.diff"
    cp "$DIFFFILE" "$out"
    emit write "$out"
    exit 0
fi

# ── split at file boundaries ─────────────────────
WORK="$(mktemp -d "$TMPDIR/cr-split.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT

# Split into per-file hunks (f0000 = any preamble before the first "diff --git").
awk -v dir="$WORK" '
    /^diff --git / { n++; out=sprintf("%s/f%04d", dir, n) }
    { if (out == "") out=sprintf("%s/f%04d", dir, 0); print > out }
' "$DIFFFILE"

slice=1
cur=0
paths=()
first_slice_started=false

for f in "$WORK"/f*; do
    [[ -f "$f" ]] || continue
    fsize="$(wc -c <"$f" | tr -d ' ')"
    if [[ "$first_slice_started" == true ]] && ((cur > 0)) && ((cur + fsize > SPLIT_MAX)); then
        ((slice++))
        cur=0
    fi
    out="$TMPDIR/cr-$name-$slice.diff"
    if ((cur == 0)); then
        : >"$out"
        paths+=("$out")
    fi
    cat "$f" >>"$out"
    cur=$((cur + fsize))
    first_slice_started=true
done

emit split "${paths[@]}"
