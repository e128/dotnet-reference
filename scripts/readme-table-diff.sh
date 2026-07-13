#!/usr/bin/env bash
# Detect drift between a README table and its source of truth.
#   (default)    scripts/README.md vs the scripts actually on disk.
#   --analyzer   src/E128.Analyzers/README.md rule table vs analyzer source.
# Usage: readme-table-diff.sh [--analyzer] [--json] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
SCRIPTS_DIR="$ROOT/scripts"
README="$SCRIPTS_DIR/README.md"
ANALYZER_DIR="$ROOT/src/E128.Analyzers"
ANALYZER_README="$ANALYZER_DIR/README.md"
JSON=false
ANALYZER=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --analyzer) ANALYZER=true ;;
        --json) JSON=true ;;
        --help)
            echo "Usage: readme-table-diff.sh [--analyzer] [--json] [--help]"
            echo "  Default: compares script names documented in scripts/README.md"
            echo "  against the scripts actually present on disk (public + internal)."
            echo "  --analyzer  Compares the analyzer rule table in"
            echo "              src/E128.Analyzers/README.md against analyzer source:"
            echo "              rule-id set-diff plus Code-Fix column verification"
            echo "              (README Yes/No vs whether a CodeFixProvider exists)."
            echo "  Pure set-diff — no LLM judgment needed."
            echo "  --json   Structured output"
            echo "           default:    {missing_from_readme, extra_in_readme, drift}"
            echo "           --analyzer: {missing_from_readme, extra_in_readme,"
            echo "                        code_fix_mismatches, drift}"
            exit 0
            ;;
        *) err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

# ── Analyzer rule-table mode ─────────────────────
if [[ "$ANALYZER" == true ]]; then
    if [[ ! -f "$ANALYZER_README" ]]; then
        err "Analyzer README not found at $ANALYZER_README"
        exit 1
    fi
    if [[ ! -d "$ANALYZER_DIR" ]]; then
        err "Analyzer project not found at $ANALYZER_DIR"
        exit 1
    fi

    mkdir -p "$ROOT/.claude/tmp"
    WORK="$(mktemp -d "$ROOT/.claude/tmp/rtd.XXXXXX")"
    trap 'rm -rf "$WORK"' EXIT

    # Source of truth: every diagnostic id declared in the analyzer project.
    "$SCRIPTS_DIR/analyzer-stats.sh" --json | jq -r '.diagnostic_ids[]' | sort -u > "$WORK/src_ids.txt"

    # README rule-table rows: "| E128nnn | title | Yes/No |" → id<TAB>codefix.
    awk -F'|' '/^\|[[:space:]]*E128[0-9]{3}[[:space:]]*\|/ {
        id = $2; gsub(/[[:space:]]/, "", id);
        cf = $(NF - 1); gsub(/[[:space:]]/, "", cf);
        print id "\t" cf
    }' "$ANALYZER_README" | sort -u > "$WORK/readme.tsv"
    cut -f1 "$WORK/readme.tsv" | sort -u > "$WORK/readme_ids.txt"

    # Map each analyzer's DiagnosticId const → id, keyed by ClassName.ConstName.
    # Single rg + awk pass: track the enclosing analyzer class, emit on each const.
    rg -n --no-heading \
        -e 'class[[:space:]]+[A-Za-z0-9_]+Analyzer' \
        -e 'const string[[:space:]]+[A-Za-z0-9_]+[[:space:]]*=[[:space:]]*"E128[0-9]{3}"' \
        "$ANALYZER_DIR" --type cs 2>/dev/null \
    | awk '
        {
            p = index($0, ":"); rest = substr($0, p + 1);
            q = index(rest, ":"); content = substr(rest, q + 1);
            t = content; sub(/^[[:space:]]+/, "", t);
            if (t ~ /^(\/\/|\*)/) next;   # skip comment lines
            if (match(content, /class[[:space:]]+[A-Za-z0-9_]+Analyzer/)) {
                cls = substr(content, RSTART, RLENGTH); sub(/class[[:space:]]+/, "", cls);
            }
            if (match(content, /const string[[:space:]]+[A-Za-z0-9_]+[[:space:]]*=[[:space:]]*"E128[0-9]{3}"/)) {
                m = substr(content, RSTART, RLENGTH);
                match(m, /const string[[:space:]]+[A-Za-z0-9_]+/); cn = substr(m, RSTART, RLENGTH); sub(/const string[[:space:]]+/, "", cn);
                match(m, /E128[0-9]{3}/); id = substr(m, RSTART, RLENGTH);
                if (cls != "") print cls "." cn "\t" id;
            }
        }' | sort -u > "$WORK/map.tsv"

    # Fixable ids: extract every FixableDiagnosticIds block, pull ClassName.ConstName
    # references and literal "E128nnn" tokens, resolve refs through the map.
    rg -U -o --no-heading -e 'FixableDiagnosticIds[[:space:]]*=>[[:space:]]*\[[^]]*\]' "$ANALYZER_DIR" --type cs 2>/dev/null \
        | grep -oE '([A-Za-z0-9_]+\.[A-Za-z0-9_]*DiagnosticId|"E128[0-9]{3}")' \
        | sed -E 's/"//g' | sort -u > "$WORK/refs.txt" || true
    awk -F'\t' '
        NR == FNR { m[$1] = $2; next }
        { if ($0 ~ /^E128[0-9]{3}$/) print $0; else if ($0 in m) print m[$0] }
    ' "$WORK/map.tsv" "$WORK/refs.txt" | sort -u > "$WORK/fixed.txt"

    # Diffs.
    mapfile -t missing < <(comm -23 "$WORK/src_ids.txt" "$WORK/readme_ids.txt")
    mapfile -t extra   < <(comm -13 "$WORK/src_ids.txt" "$WORK/readme_ids.txt")

    # Expected Code-Fix value per source id (Yes if a fixer exists), then compare
    # against the README cell for ids present in both source and README.
    awk -F'\t' 'NR == FNR { f[$1]; next } { print $1 "\t" ($1 in f ? "Yes" : "No") }' \
        "$WORK/fixed.txt" "$WORK/src_ids.txt" > "$WORK/expected.tsv"
    awk -F'\t' '
        NR == FNR { e[$1] = $2; next }
        ($1 in e) && (e[$1] != $2) { print $1 "\t" $2 "\t" e[$1] }
    ' "$WORK/expected.tsv" "$WORK/readme.tsv" > "$WORK/mismatch.tsv"
    mapfile -t mismatches < <(awk -F'\t' '{ print $1 " (readme=" $2 ", source=" $3 ")" }' "$WORK/mismatch.tsv")

    if [[ "$JSON" == true ]]; then
        missing_json=$(printf '%s\n' "${missing[@]}" | jq -R 'select(length>0)' | jq -s '.')
        extra_json=$(printf '%s\n' "${extra[@]}" | jq -R 'select(length>0)' | jq -s '.')
        mism_json=$(jq -R 'select(length>0)|split("\t")|{rule:.[0],readme:.[1],source:.[2]}' "$WORK/mismatch.tsv" | jq -s '.')
        jq -n \
            --argjson missing "$missing_json" \
            --argjson extra "$extra_json" \
            --argjson mismatches "$mism_json" \
            '{missing_from_readme: $missing, extra_in_readme: $extra,
              code_fix_mismatches: $mismatches,
              drift: (($missing | length) + ($extra | length) + ($mismatches | length) > 0)}'
        exit 0
    fi

    if [[ ${#missing[@]} -eq 0 && ${#extra[@]} -eq 0 && ${#mismatches[@]} -eq 0 ]]; then
        ok "$ANALYZER_README rule table is in sync — no drift"
        exit 0
    fi
    if [[ ${#missing[@]} -gt 0 ]]; then
        warn "In analyzer source but missing from README rule table:"
        printf '  %s\n' "${missing[@]}"
    fi
    if [[ ${#extra[@]} -gt 0 ]]; then
        warn "In README rule table but not in analyzer source:"
        printf '  %s\n' "${extra[@]}"
    fi
    if [[ ${#mismatches[@]} -gt 0 ]]; then
        warn "Code-Fix column mismatches (README vs actual CodeFixProvider):"
        printf '  %s\n' "${mismatches[@]}"
    fi
    exit 0
fi

# ── Default: scripts/README.md vs scripts on disk ─
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
