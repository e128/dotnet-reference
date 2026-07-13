#!/usr/bin/env bash
# Locate the latest review plan and parse its checklist findings, grouped by file with severity.
# Usage: review-findings.sh [--include-low] [--json]
source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

JSON=false
INCLUDE_LOW=false
for arg in "$@"; do
    case "$arg" in
        --json) JSON=true ;;
        --include-low) INCLUDE_LOW=true ;;
        --help | -h)
            echo "Usage: review-findings.sh [--include-low] [--json]"
            echo "Locate the latest plans/review-* dir and parse '- [ ] \`file:line\` ...' findings,"
            echo "grouped by file with severity. LOW findings are skipped unless --include-low."
            exit 0
            ;;
        *)
            err "Unknown flag: $arg"
            exit 1
            ;;
    esac
done

ROOT="$(find_repo_root)"
cd "$ROOT" || exit 1

if [[ ! -d plans ]]; then
    if $JSON; then
        printf '{"plan":null,"findings":[],"count":0}\n'
    else
        warn "No saved review found. Run 'review --local' first."
    fi
    exit 0
fi

PLAN_DIR="$(fd -t d 'review-' plans/ 2>/dev/null | sort -r | head -1)"
if [[ -z "$PLAN_DIR" ]]; then
    if $JSON; then
        printf '{"plan":null,"findings":[],"count":0}\n'
    else
        warn "No saved review found. Run 'review --local' first."
    fi
    exit 0
fi

# Prefer tasks.md; fall back to the plan's context.md.
SRC="$(fd -e md 'tasks' "$PLAN_DIR" 2>/dev/null | head -1)"
[[ -z "$SRC" ]] && SRC="$(fd -e md 'context' "$PLAN_DIR" 2>/dev/null | head -1)"
if [[ -z "$SRC" || ! -f "$SRC" ]]; then
    if $JSON; then
        printf '{"plan":"%s","findings":[],"count":0}\n' "$PLAN_DIR"
    else
        warn "No tasks.md or context.md in $PLAN_DIR"
    fi
    exit 0
fi

# Matches: - [ ] `path/to/file.cs:123` rest-of-line
# shellcheck disable=SC2016  # literal regex for [[ =~ ]]; must not expand
FINDING_RE='^- \[ \] `([^:`]+):([0-9]+)`(.*)$'

# Collected finding rows: "file<TAB>line<TAB>severity<TAB>text"
rows=()
count=0

while IFS= read -r line; do
    [[ "$line" =~ $FINDING_RE ]] || continue
    file="${BASH_REMATCH[1]}"
    lineno="${BASH_REMATCH[2]}"
    rest="${BASH_REMATCH[3]}"

    sev="MEDIUM"
    for s in CRITICAL HIGH MEDIUM LOW; do
        if [[ "$rest" == *"$s"* ]]; then
            sev="$s"
            break
        fi
    done

    if [[ "$sev" == "LOW" && "$INCLUDE_LOW" != true ]]; then
        continue
    fi

    # Trim leading separators/whitespace from the descriptive text.
    text="${rest#"${rest%%[![:space:]]*}"}"
    text="${text#— }"
    text="${text#- }"

    rows+=("$(printf '%s\t%s\t%s\t%s' "$file" "$lineno" "$sev" "$text")")
    ((count++)) || true
done <"$SRC"

# Sort rows by file (stable), so grouping prints contiguous files.
if ((${#rows[@]} > 0)); then
    mapfile -t rows < <(printf '%s\n' "${rows[@]}" | sort -t$'\t' -k1,1 -s)
fi

if $JSON; then
    entries=""
    for row in "${rows[@]}"; do
        IFS=$'\t' read -r file lineno sev text <<<"$row"
        esc_text="$(printf '%s' "$text" | sed 's/\\/\\\\/g; s/"/\\"/g')"
        e="$(printf '{"file":"%s","line":%s,"severity":"%s","text":"%s"}' "$file" "$lineno" "$sev" "$esc_text")"
        [[ -n "$entries" ]] && entries="$entries,"
        entries="$entries$e"
    done
    printf '{"plan":"%s","findings":[%s],"count":%d}\n' "$PLAN_DIR" "$entries" "$count"
else
    if ((count == 0)); then
        warn "No findings parsed in $SRC"
        exit 0
    fi
    info "Findings from $SRC"
    prev_file=""
    for row in "${rows[@]}"; do
        IFS=$'\t' read -r file lineno sev text <<<"$row"
        if [[ "$file" != "$prev_file" ]]; then
            printf "%s\n" "$file"
            prev_file="$file"
        fi
        printf "  %-8s %s:%s  %s\n" "$sev" "$file" "$lineno" "$text"
    done
    ok "$count finding(s)"
fi
