#!/usr/bin/env bash
# Parse .NET build diagnostics into structured records.
# Usage: diagnostics.sh [--json] [--group] [--code ID] [--diff OLD.json NEW.json]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

JSON=false
MODE=default
CODE_FILTER=""
DIFF_OLD=""
DIFF_NEW=""

show_help() {
    cat <<'EOF'
diagnostics.sh — parse .NET build diagnostics into structured records.

Usage:
  diagnostics.sh [--json]                 Emit {file,line,col,severity,code,message}
  diagnostics.sh --group [--json]         Group by code prefix (CS>E128>IDE>MA>CA>RCS>SS)
  diagnostics.sh --code ID [--json]       Filter to one diagnostic id
                                          (IDE1006 also extracts + dedupes by symbol)
  diagnostics.sh --diff OLD.json NEW.json [--json]   Net-new records vs a prior capture

Runs the underlying `dotnet build` (warnings included) and parses raw MSBuild
lines of the form `File.cs(line,col): error CODE: message`. --diff reads two
prior JSON captures and does not build.
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --help|-h) show_help; exit 0 ;;
        --json)    JSON=true ;;
        --group)   MODE="group" ;;
        --code)    MODE="code"; CODE_FILTER="${2:-}"; shift ;;
        --diff)    MODE="diff"; DIFF_OLD="${2:-}"; DIFF_NEW="${3:-}"; shift 2 ;;
        *)         err "Unknown argument: $1"; exit 1 ;;
    esac
    shift
done

# ── Build + parse ────────────────────────────────
# Emit deduped TSV: file<TAB>line<TAB>col<TAB>severity<TAB>code<TAB>message
collect_records() {
    local root target output line
    root="$(find_repo_root)"
    target="$(find_solution)"
    # Warnings included (no -clp:ErrorsOnly). Build failure is expected — swallow it.
    output="$(dotnet build "$target" --nologo 2>&1 || true)"
    printf '%s\n' "$output" | while IFS= read -r line; do
        # Trim leading whitespace MSBuild adds to diagnostic lines.
        line="${line#"${line%%[![:space:]]*}"}"
        if [[ "$line" =~ ^(.+)\(([0-9]+),([0-9]+)\):[[:space:]]+(error|warning)[[:space:]]+([A-Za-z0-9]+):[[:space:]]+(.*)$ ]]; then
            local file="${BASH_REMATCH[1]}"
            local ln="${BASH_REMATCH[2]}"
            local col="${BASH_REMATCH[3]}"
            local sev="${BASH_REMATCH[4]}"
            local code="${BASH_REMATCH[5]}"
            local msg="${BASH_REMATCH[6]}"
            msg="${msg% \[*\]}"          # strip trailing " [project.csproj]"
            file="${file#"$root"/}"      # repo-relative path
            printf '%s\t%s\t%s\t%s\t%s\t%s\n' "$file" "$ln" "$col" "$sev" "$code" "$msg"
        fi
    done | sort -u
}

records_json() {
    collect_records | jq -R -s '
        split("\n") | map(select(length > 0)) | map(split("\t")) |
        map({file: .[0], line: (.[1] | tonumber), col: (.[2] | tonumber),
             severity: .[3], code: .[4], message: .[5]})'
}

# ── Renderers ────────────────────────────────────
render_table() {
    local json="$1"
    if [[ "$(jq 'length' <<<"$json")" -eq 0 ]]; then
        ok "No diagnostics."
        return
    fi
    {
        printf 'FILE\tLINE\tCOL\tSEV\tCODE\tMESSAGE\n'
        jq -r '.[] | [.file, (.line | tostring), (.col | tostring), .severity, .code, .message] | @tsv' <<<"$json"
    } | column -t -s "$(printf '\t')"
}

# ── Modes ────────────────────────────────────────
case "$MODE" in
    default)
        records="$(records_json)"
        if [[ "$JSON" == true ]]; then jq . <<<"$records"; else render_table "$records"; fi
        ;;

    group)
        records="$(records_json)"
        group="$(jq '
            def rank: {"CS":1,"E128":2,"IDE":3,"MA":4,"CA":5,"RCS":6,"SS":7}[.] // 99;
            def prefix: if startswith("E128") then "E128"
                        else capture("^(?<p>[A-Za-z]+)").p end;
            [ .[] | .code ] | group_by(.) |
            map({code: .[0], count: length}) |
            group_by(.code | prefix) |
            map({prefix: (.[0].code | prefix),
                 count: (map(.count) | add),
                 codes: sort_by(.code)}) |
            sort_by([(.prefix | rank), .prefix])' <<<"$records")"
        if [[ "$JSON" == true ]]; then
            jq . <<<"$group"
        else
            jq -r '.[] | "\(.prefix) (\(.count))", (.codes[] | "  \(.code)  \(.count)")' <<<"$group"
        fi
        ;;

    code)
        if [[ -z "$CODE_FILTER" ]]; then err "--code requires a diagnostic id"; exit 1; fi
        records="$(records_json)"
        if [[ "$CODE_FILTER" == "IDE1006" ]]; then
            filtered="$(jq --arg c "$CODE_FILTER" --arg op "('" --arg cp "')" '
                [ .[] | select(.code == $c)
                  | . + {symbol: ((.message | split($op)) as $p
                                  | if ($p | length) > 1 then ($p[1] | split($cp)[0]) else null end)} ]
                | unique_by(.symbol // .message)' <<<"$records")"
            if [[ "$JSON" == true ]]; then
                jq . <<<"$filtered"
            elif [[ "$(jq 'length' <<<"$filtered")" -eq 0 ]]; then
                ok "No $CODE_FILTER diagnostics."
            else
                {
                    printf 'SYMBOL\tLOCATION\n'
                    jq -r '.[] | "\(.symbol // "?")\t\(.file):\(.line)"' <<<"$filtered"
                } | column -t -s "$(printf '\t')"
            fi
        else
            filtered="$(jq --arg c "$CODE_FILTER" '[ .[] | select(.code == $c) ]' <<<"$records")"
            if [[ "$JSON" == true ]]; then jq . <<<"$filtered"; else render_table "$filtered"; fi
        fi
        ;;

    diff)
        if [[ ! -f "$DIFF_OLD" || ! -f "$DIFF_NEW" ]]; then
            err "--diff requires two readable JSON files: OLD.json NEW.json"
            exit 1
        fi
        netnew="$(jq -n --slurpfile old "$DIFF_OLD" --slurpfile new "$DIFF_NEW" '$new[0] - $old[0]')"
        if [[ "$JSON" == true ]]; then jq . <<<"$netnew"; else render_table "$netnew"; fi
        ;;
esac
