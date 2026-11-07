#!/usr/bin/env bash
# Report Cobertura coverage by feature area, and gate on a line floor.
# Usage: coverage-areas.sh [--file PATH|GLOB] [--json] [--markdown]
#        [--namespace PREFIX] [--source-file NAME] [--min-line PCT]
#        [--self-test] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
SELF="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/$(basename "${BASH_SOURCE[0]}")"
AREAS_PATH="$ROOT/coverage-areas.json"
RESULTS_DIR="$ROOT/test-results"

# The assemblies coverage.config.xml leaves measured. A missing module means that
# assembly stopped producing coverage, which the gate must report.
readonly EXPECTED_MODULES=(
    "E128.Analyzers"
    "E128.Analyzers.CodeFixers"
    "E128.Reference.Core"
    "E128.Reference.Cli"
    "E128.Reference.Web"
)

FILE=""
JSON=false
MARKDOWN=false
NAMESPACE=""
SOURCE_FILE=""
MIN_LINE="0"
MIN_ASSEMBLY="0"
SELF_TEST=false
EXPECTED_JSON=""

REPORTS=()
TMP_DIR=""

usage() {
    cat <<'EOF'
Usage: coverage-areas.sh [OPTIONS]

Parse Cobertura coverage reports and report coverage by feature area.

Options:
  --file PATH|GLOB     Read one report or a glob (default: test-results)
  --json               Print machine-readable JSON
  --markdown           Print Markdown tables for a GitHub job summary
  --namespace PREFIX   Fail on any line or branch gap under one namespace
  --source-file NAME   Fail when one source file sits below --min-line
  --min-line PCT       Fail when total line coverage sits below PCT (0 disables)
  --min-assembly PCT   Fail when any measured assembly sits below PCT (0 disables)
  --self-test          Run the built-in gate fixtures
  --help               Show this help

A run with --namespace or --source-file uses that check only. The total-line
floor in --min-line does not apply to those two modes.
EOF
}

cleanup() {
    [[ -n "$TMP_DIR" && -d "$TMP_DIR" ]] && rm -rf "$TMP_DIR"
}
trap cleanup EXIT

require_tool() {
    command -v "$1" >/dev/null 2>&1 || {
        err "Missing required tool: $1"
        exit 1
    }
}

# Read one shell argument pair into a validated float string.
parse_float() {
    local value="$1"
    [[ "$value" =~ ^[0-9]+([.][0-9]+)?$ ]] || {
        err "Not a percentage: $value"
        exit 1
    }
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --file)        FILE="$2"; shift ;;
        --json)        JSON=true ;;
        --markdown)    MARKDOWN=true ;;
        --namespace)   NAMESPACE="$2"; shift ;;
        --source-file) SOURCE_FILE="$2"; shift ;;
        --min-line)    MIN_LINE="$2"; shift ;;
        --min-assembly) MIN_ASSEMBLY="$2"; shift ;;
        --self-test)   SELF_TEST=true ;;
        --help)        usage; exit 0 ;;
        -*)            err "Unknown flag: $1"; exit 1 ;;
        *)             err "Unexpected argument: $1"; exit 1 ;;
    esac
    shift
done

parse_float "$MIN_LINE"
parse_float "$MIN_ASSEMBLY"
require_tool jq
require_tool xmllint
EXPECTED_JSON="$(printf '%s\n' "${EXPECTED_MODULES[@]}" | jq -R -s 'split("\n") | map(select(length > 0))')"

# Resolve --file into a list of report paths, keeping one path per file name.
resolve_reports() {
    local -a found=()
    local path base

    if [[ -n "$FILE" ]]; then
        if [[ "$FILE" == *'*'* ]]; then
            local pattern="$FILE"
            [[ "$pattern" != /* ]] && pattern="$ROOT/$pattern"
            shopt -s globstar
            while IFS= read -r path; do
                [[ -n "$path" ]] && found+=("$path")
            done < <(compgen -G "$pattern" || true)
            shopt -u globstar
        else
            path="$FILE"
            [[ ! -f "$path" && -f "$ROOT/$path" ]] && path="$ROOT/$path"
            [[ -f "$path" ]] && found+=("$path")
        fi
    elif [[ -d "$RESULTS_DIR" ]]; then
        while IFS= read -r path; do
            found+=("$path")
        done < <(find "$RESULTS_DIR" -type f -name '*.cobertura.xml' 2>/dev/null | sort)
    fi

    declare -A seen=()
    REPORTS=()
    for path in "${found[@]}"; do
        base="${path##*/}"
        [[ -n "${seen[$base]:-}" ]] && continue
        seen[$base]=1
        REPORTS+=("$path")
    done
}

# Print one TSV record per class-level line: package, class, file, line, hits,
# branch hits, branch total. Method lines sit inside <methods> and are skipped,
# so a line is never counted twice.
parse_report() {
    local report="$1"
    xmllint --format "$report" 2>/dev/null | awk '
        BEGIN { OFS = "\t" }

        function attr(line, name,    s) {
            if (!match(line, name "=\"[^\"]*\"")) return ""
            s = substr(line, RSTART, RLENGTH)
            sub("^" name "=\"", "", s)
            sub(/\"$/, "", s)
            return s
        }

        /<package / { pkg = attr($0, "name"); next }
        /<class /   { cls = attr($0, "name"); file = attr($0, "filename"); next }
        /<methods>/ { in_methods = 1; next }
        /<\/methods>/ { in_methods = 0; next }

        /<line / {
            if (in_methods || cls == "") next
            number = attr($0, "number")
            hits = attr($0, "hits")
            cond = attr($0, "condition-coverage")
            bh = 0
            bt = 0
            if (match(cond, /\([0-9]+\/[0-9]+\)/)) {
                pair = substr(cond, RSTART + 1, RLENGTH - 2)
                split(pair, parts, "/")
                bh = parts[1] + 0
                bt = parts[2] + 0
            }
            if (number != "") print pkg, cls, file, number, hits + 0, bh, bt
        }
    '
}

# Merge records for the same line across module reports. The highest hit count
# wins, so a class exercised by any test module counts once.
merge_records() {
    awk -F'\t' '
        BEGIN { OFS = "\t" }
        {
            key = $1 SUBSEP $2 SUBSEP $3 SUBSEP $4
            if (!(key in best) || ($5 + 0) > best[key]) {
                best[key] = $5 + 0
                record[key] = $1 "\t" $2 "\t" $3 "\t" $4 "\t" ($5 + 0) "\t" $6 "\t" $7
            }
        }
        END { for (key in record) print record[key] }
    ' | sort -t $'\t' -k1,1 -k2,2 -k3,3 -k4,4n
}

# Build the merged line records once, with each class mapped to a feature area.
build_records() {
    local raw="$TMP_DIR/raw.tsv"
    local merged="$TMP_DIR/merged.tsv"
    local report

    : > "$raw"
    for report in "${REPORTS[@]}"; do
        parse_report "$report" >> "$raw"
    done

    merge_records < "$raw" > "$merged"

    jq -R -s --slurpfile areas "$AREAS_PATH" '
        def area_map($areas):
            [ $areas | to_entries[] | .key as $area | .value[] | {key: ., value: $area} ]
            | from_entries;

        def area_for($rec; $map):
            ([ $map | to_entries[]
               | . as $entry
               | select($rec.assembly == $entry.key
                        or $rec.class == $entry.key
                        or ($rec.class | startswith($entry.key + ".")))
             ] | sort_by(.key | length) | last | .value) // "Unmapped";

        ($areas[0] | area_map(.)) as $map
        | split("\n")
        | map(select(length > 0) | split("\t")
            | {
                assembly: .[0],
                class: .[1],
                file: .[2],
                line: (.[3] | tonumber),
                hits: (.[4] | tonumber),
                branch_hit: (.[5] | tonumber),
                branch_total: (.[6] | tonumber)
              })
        | map(. + {area: area_for(.; $map)})
    ' < "$merged" > "$TMP_DIR/records.json"
}

# Print the package names in the reports, one per line. This list drives the
# missing-module check, so a measured assembly with no lines still counts.
list_packages() {
    local report
    for report in "${REPORTS[@]}"; do
        xmllint --format "$report" 2>/dev/null | awk '
            function attr(line, name,    s) {
                if (!match(line, name "=\"[^\"]*\"")) return ""
                s = substr(line, RSTART, RLENGTH)
                sub("^" name "=\"", "", s)
                sub(/\"$/, "", s)
                return s
            }
            /<package / {
                name = attr($0, "name")
                if (name != "") print name
            }
        '
    done | sort -u
}

# Print the feature-area report, and apply the total-line floor in area mode.
report_areas() {
    local present_json
    present_json="$(list_packages | jq -R -s 'split("\n") | map(select(length > 0))')"

    local summary
    summary="$(jq -c \
        --argjson expected "$EXPECTED_JSON" \
        --argjson present "$present_json" \
        --argjson min_line "$MIN_LINE" \
        --argjson min_assembly "$MIN_ASSEMBLY" \
        --argjson gate_enabled "$([[ "$MARKDOWN" == true ]] && echo false || echo true)" \
        --argjson report_count "${#REPORTS[@]}" '
        def pct($hit; $total):
            if $total == 0 then 0 else (($hit / $total) * 10000 | round) / 100 end;

        . as $recs
        | ($recs | length) as $lines_total
        | ($recs | map(select(.hits > 0)) | length) as $lines_hit
        | ([ $recs | group_by(.assembly)[]
             | {
                 assembly: .[0].assembly,
                 lines_hit: ([.[] | select(.hits > 0)] | length),
                 lines_total: length
               }
             | . + {line_pct: pct(.lines_hit; .lines_total)}
           ] | sort_by(.assembly)) as $assemblies
        | ([ $recs | group_by(.area)[]
             | {
                 area: .[0].area,
                 lines_hit: ([.[] | select(.hits > 0)] | length),
                 lines_total: length
               }
             | . + {line_pct: pct(.lines_hit; .lines_total)}
           ] | map(select(.lines_total > 0)) | sort_by(.line_pct) | reverse) as $areas
        | ([$recs[] | select(.area == "Unmapped")]) as $unmapped
        | ($lines_total == 0) as $no_lines
        | pct($lines_hit; $lines_total) as $line_pct
        | ($expected - $present) as $missing
        | ($gate_enabled and ($line_pct < $min_line)) as $below_floor
        | ($gate_enabled and ($min_assembly > 0)) as $assembly_gate
        | ([ $assemblies[] | select($assembly_gate and .line_pct < $min_assembly) ]) as $assemblies_below
        | {
            classes: ([$recs[].class] | unique | length),
            lines_hit: $lines_hit,
            lines_total: $lines_total,
            line_pct: $line_pct,
            report_count: $report_count,
            present_modules: $present,
            assemblies: $assemblies,
            areas: $areas,
            unmapped: {
              lines_hit: ($unmapped | map(select(.hits > 0)) | length),
              lines_total: ($unmapped | length),
              classes: ([$unmapped[].class] | unique | length)
            },
            gate: {
              min_line: $min_line,
              min_assembly: $min_assembly,
              missing_modules: $missing,
              assemblies_below: $assemblies_below,
              passed: (($no_lines | not) and ($below_floor | not)
                       and ($missing | length == 0) and ($assemblies_below | length == 0))
            }
          }
    ' "$TMP_DIR/records.json")"

    if [[ "$JSON" == true ]]; then
        printf '%s\n' "$summary"
    elif [[ "$MARKDOWN" == true ]]; then
        printf '## Coverage\n\n'
        printf '%s module reports, merged.\n\n' "${#REPORTS[@]}"
        printf '| Assembly | Lines | Line |\n'
        printf '|:---|---:|---:|\n'
        while IFS=$'\t' read -r name hit total pct; do
            printf '| %s | %s/%s | %s%% |\n' "$name" "$hit" "$total" "$pct"
        done < <(jq -r '.assemblies[] | [.assembly, .lines_hit, .lines_total, .line_pct] | @tsv' <<<"$summary")
        printf '\n**Total: %s%%, %s of %s lines**\n' \
            "$(jq -r '.line_pct' <<<"$summary")" \
            "$(jq -r '.lines_hit' <<<"$summary")" \
            "$(jq -r '.lines_total' <<<"$summary")"
        printf '\n### By Feature Area\n\n'
        printf '| Feature Area | Line | Lines |\n'
        printf '|:---|---:|---:|\n'
        while IFS=$'\t' read -r name pct hit total; do
            printf '| %s | %s%% | %s/%s |\n' "$name" "$pct" "$hit" "$total"
        done < <(jq -r '.areas[] | [.area, .line_pct, .lines_hit, .lines_total] | @tsv' <<<"$summary")
        if [[ "$(jq -r '.unmapped.lines_total' <<<"$summary")" -gt 0 ]]; then
            printf '\nUnmapped: %s lines across %s classes.\n' \
                "$(jq -r '.unmapped.lines_total' <<<"$summary")" \
                "$(jq -r '.unmapped.classes' <<<"$summary")"
        fi
        return 0
    else
        printf '## Assembly Coverage\n\n'
        printf '%-28s %8s %8s %8s\n' "Assembly" "Lines" "Hit" "Line"
        while IFS=$'\t' read -r name pct hit total; do
            printf '%-28s %8s %8s %7s%%\n' "$name" "$total" "$hit" "$pct"
        done < <(jq -r '.assemblies[] | [.assembly, .line_pct, .lines_hit, .lines_total] | @tsv' <<<"$summary")
        printf '\n## Feature Area Coverage\n\n'
        printf '%-22s %8s %8s %8s\n' "Area" "Lines" "Hit" "Line"
        while IFS=$'\t' read -r name pct hit total; do
            printf '%-22s %8s %8s %7s%%\n' "$name" "$total" "$hit" "$pct"
        done < <(jq -r '.areas[] | [.area, .line_pct, .lines_hit, .lines_total] | @tsv' <<<"$summary")
        if [[ "$(jq -r '.unmapped.lines_total' <<<"$summary")" -gt 0 ]]; then
            printf '\nUnmapped: %s lines across %s classes.\n' \
                "$(jq -r '.unmapped.lines_total' <<<"$summary")" \
                "$(jq -r '.unmapped.classes' <<<"$summary")"
        fi
        printf '\nLine coverage %s%%: %s of %s lines across %s reports\n' \
            "$(jq -r '.line_pct' <<<"$summary")" \
            "$(jq -r '.lines_hit' <<<"$summary")" \
            "$(jq -r '.lines_total' <<<"$summary")" \
            "$(jq -r '.report_count' <<<"$summary")"

        local missing
        missing="$(jq -r '.gate.missing_modules | join(", ")' <<<"$summary")"
        if [[ -n "$missing" ]]; then
            printf 'Missing measured modules: %s. The reports measured: %s.\n' \
                "$missing" "$(jq -r '.present_modules | join(", ")' <<<"$summary")"
        fi
        if [[ "$(jq -r '.lines_total' <<<"$summary")" -eq 0 ]]; then
            printf 'Coverage report holds no line data.\n'
        fi
        if [[ "$(jq -r '.gate.min_line' <<<"$summary")" != "0" ]] \
            && jq -e '.line_pct < .gate.min_line' <<<"$summary" >/dev/null; then
            printf 'Line coverage %s%% is below the %s%% floor: %s lines covered of %s.\n' \
                "$(jq -r '.line_pct' <<<"$summary")" \
                "$(jq -r '.gate.min_line' <<<"$summary")" \
                "$(jq -r '.lines_hit' <<<"$summary")" \
                "$(jq -r '.lines_total' <<<"$summary")"
        fi
        if [[ "$(jq -r '.gate.min_assembly' <<<"$summary")" != "0" ]] \
            && jq -e '.gate.assemblies_below | length > 0' <<<"$summary" >/dev/null; then
            printf 'Assemblies below the %s%% floor: %s.\n' \
                "$(jq -r '.gate.min_assembly' <<<"$summary")" \
                "$(jq -r '.gate.assemblies_below | map("\(.assembly) \(.line_pct)%") | join(", ")' <<<"$summary")"
        fi
    fi

    if [[ "$MARKDOWN" == true ]]; then
        return 0
    fi
    jq -e '.gate.passed' <<<"$summary" >/dev/null
}

# Report line and branch gaps for one namespace prefix. Any gap exits 1.
report_namespace() {
    local result
    result="$(jq --arg prefix "$NAMESPACE" '
        [ .[] | select(.class == $prefix or (.class | startswith($prefix + "."))) ]
        | group_by(.class)
        | map({
            class: .[0].class,
            file: (.[0].file | split("/") | last),
            lines_hit: ([.[] | select(.hits > 0)] | length),
            lines_total: length,
            branches_hit: (([.[] | select(.branch_total > 0) | .branch_hit] | add) // 0),
            branches_total: (([.[] | select(.branch_total > 0) | .branch_total] | add) // 0),
            uncovered_lines: [.[] | select(.hits == 0) | .line],
            partial_branch_lines: [.[] | select(.branch_total > 0 and .branch_hit < .branch_total) | .line]
          })
        | . as $rows
        | [ $rows[] | select(.lines_hit < .lines_total or .branches_hit < .branches_total) ] as $gaps
        | {
            namespace: $prefix,
            classes: ($rows | length),
            lines_hit: (($rows | map(.lines_hit) | add) // 0),
            lines_total: (($rows | map(.lines_total) | add) // 0),
            branches_hit: (($rows | map(.branches_hit) | add) // 0),
            branches_total: (($rows | map(.branches_total) | add) // 0),
            passed: ($gaps | length == 0),
            gaps: $gaps
          }
    ' "$TMP_DIR/records.json")"

    local classes
    classes="$(jq -r '.classes' <<<"$result")"
    if [[ "$classes" -eq 0 ]]; then
        if [[ "$JSON" == true ]]; then
            printf '%s\n' "$result"
        else
            printf 'No classes found under namespace: %s\n' "$NAMESPACE"
        fi
        exit 1
    fi

    if [[ "$JSON" == true ]]; then
        printf '%s\n' "$result"
    else
        local line_pct branch_pct
        line_pct="$(jq -r 'if .lines_total == 0 then 100 else ((.lines_hit / .lines_total) * 10000 | round) / 100 end' <<<"$result")"
        branch_pct="$(jq -r 'if .branches_total == 0 then 100 else ((.branches_hit / .branches_total) * 10000 | round) / 100 end' <<<"$result")"
        printf '## %s: lines %s/%s (%s%%), branches %s/%s (%s%%), %s classes\n' \
            "$NAMESPACE" \
            "$(jq -r '.lines_hit' <<<"$result")" \
            "$(jq -r '.lines_total' <<<"$result")" \
            "$line_pct" \
            "$(jq -r '.branches_hit' <<<"$result")" \
            "$(jq -r '.branches_total' <<<"$result")" \
            "$branch_pct" \
            "$classes"
        while IFS=$'\t' read -r cls file hit total bhit btotal; do
            printf -- '- %s [%s]: lines %s/%s, branches %s/%s\n' "$cls" "$file" "$hit" "$total" "$bhit" "$btotal"
        done < <(jq -r '.gaps[] | [.class, .file, .lines_hit, .lines_total, .branches_hit, .branches_total] | @tsv' <<<"$result")
    fi

    jq -e '.passed' <<<"$result" >/dev/null
}

# Report line coverage for every class whose report file matches one source file.
report_source_file() {
    local base result
    base="$(basename "$SOURCE_FILE")"
    result="$(jq --arg base "$base" --argjson min_line "$MIN_LINE" '
        [ .[] | select((.file | split("/") | last) == $base) ]
        | group_by(.class)
        | map({
            class: .[0].class,
            file: .[0].file,
            lines_hit: ([.[] | select(.hits > 0)] | length),
            lines_total: length,
            uncovered_lines: [.[] | select(.hits == 0) | .line]
          })
        | . as $rows
        | (($rows | map(.lines_hit) | add) // 0) as $hit
        | (($rows | map(.lines_total) | add) // 0) as $total
        | (if $total == 0 then 100 else (($hit / $total) * 10000 | round) / 100 end) as $pct
        | {
            source_file: $base,
            classes: ($rows | length),
            lines_hit: $hit,
            lines_total: $total,
            line_pct: $pct,
            min_line: $min_line,
            passed: (($rows | length) > 0 and (($min_line == 0) or ($pct >= $min_line))),
            rows: $rows
          }
    ' "$TMP_DIR/records.json")"

    if [[ "$(jq -r '.classes' <<<"$result")" -eq 0 ]]; then
        if [[ "$JSON" == true ]]; then
            printf '%s\n' "$result"
        else
            printf 'No covered class found for source file: %s\n' "$SOURCE_FILE"
        fi
        exit 1
    fi

    if [[ "$JSON" == true ]]; then
        printf '%s\n' "$result"
    else
        printf '## %s: lines %s/%s (%s%%), %s classes\n' \
            "$base" \
            "$(jq -r '.lines_hit' <<<"$result")" \
            "$(jq -r '.lines_total' <<<"$result")" \
            "$(jq -r '.line_pct' <<<"$result")" \
            "$(jq -r '.classes' <<<"$result")"
        while IFS=$'\t' read -r cls hit total; do
            printf -- '- %s: lines %s/%s\n' "$cls" "$hit" "$total"
        done < <(jq -r '.rows[] | [.class, .lines_hit, .lines_total] | @tsv' <<<"$result")
        if ! jq -e '.passed' <<<"$result" >/dev/null; then
            printf 'Line coverage %s%% is below the %s%% floor.\n' \
                "$(jq -r '.line_pct' <<<"$result")" "$MIN_LINE"
        fi
    fi

    jq -e '.passed' <<<"$result" >/dev/null
}

# Write a fixture report with the given class names under the first expected module.
write_fixture() {
    local path="$1"
    shift
    local -a classes=("$@")
    local cls module

    {
        printf '%s\n' '<?xml version="1.0" encoding="utf-8"?>'
        printf '%s\n' '<coverage line-rate="0.5" lines-covered="1" lines-valid="2">'
        printf '%s\n' '  <packages>'
        printf '    <package name="%s" line-rate="0.5">\n' "${EXPECTED_MODULES[0]}"
        printf '%s\n' '      <classes>'
        for cls in "${classes[@]}"; do
            printf '        <class name="%s" filename="Fixture.cs">\n' "$cls"
            printf '%s\n' '          <methods>'
            printf '%s\n' '            <method name="M">'
            printf '%s\n' '              <lines>'
            printf '%s\n' '                <line number="1" hits="1" />'
            printf '%s\n' '              </lines>'
            printf '%s\n' '            </method>'
            printf '%s\n' '          </methods>'
            printf '%s\n' '          <lines>'
            printf '%s\n' '            <line number="1" hits="1" />'
            printf '%s\n' '            <line number="2" hits="0" />'
            printf '%s\n' '          </lines>'
            printf '%s\n' '        </class>'
        done
        printf '%s\n' '      </classes>'
        printf '%s\n' '    </package>'
        for module in "${EXPECTED_MODULES[@]:1}"; do
            printf '    <package name="%s" line-rate="0"><classes /></package>\n' "$module"
        done
        printf '%s\n' '  </packages>'
        printf '%s\n' '</coverage>'
    } > "$path"
}

# Write a report that holds no packages.
write_empty_fixture() {
    local path="$1"
    printf '%s\n' '<?xml version="1.0" encoding="utf-8"?>' \
        '<coverage line-rate="0" lines-covered="0" lines-valid="0"><packages /></coverage>' \
        > "$path"
}

# Run the gate fixtures. Exit 1 when any case fails.
run_self_test() {
    local dir="$TMP_DIR/self-test"
    mkdir -p "$dir"

    local a="$dir/a.cobertura.xml"
    local b="$dir/b.cobertura.xml"
    local empty="$dir/empty.cobertura.xml"
    local glob="$dir/*.cobertura.xml"

    write_fixture "$a" "E128.Analyzers.Alpha"
    write_fixture "$b" "E128.Analyzers.Beta"
    write_empty_fixture "$empty"

    local failures=0

    run_case() {
        local name="$1"
        local want="$2"
        local want_classes="$3"
        shift 3
        local output code classes
        output="$("$SELF" "$@" 2>&1)" && code=0 || code=$?
        classes="$(jq -r '.classes // -1' <<<"$output" 2>/dev/null || echo -1)"
        if [[ "$code" -eq "$want" ]] && { [[ "$want_classes" -lt 0 ]] || [[ "$classes" -eq "$want_classes" ]]; }; then
            printf 'PASS %s\n' "$name"
        else
            failures=$((failures + 1))
            printf 'FAIL %s: expected exit %s classes %s, got exit %s classes %s\n' \
                "$name" "$want" "$want_classes" "$code" "$classes"
        fi
    }

    run_case "below-floor" 1 -1 --file "$a" --min-line 90 --json
    run_case "above-floor" 0 -1 --file "$a" --min-line 10 --json
    run_case "empty-report" 1 -1 --file "$empty" --json
    run_case "two-file-union" 0 2 --file "$glob" --min-line 10 --json
    run_case "assembly-below-floor" 1 -1 --file "$a" --min-assembly 60 --json
    run_case "assembly-above-floor" 0 -1 --file "$a" --min-assembly 40 --json
    run_case "source-file-below-floor" 1 -1 --file "$a" --source-file "Fixture.cs" --min-line 80 --json
    run_case "source-file-above-floor" 0 1 --file "$a" --source-file "Fixture.cs" --min-line 40 --json
    run_case "source-file-no-match" 1 -1 --file "$a" --source-file "Missing.cs" --json

    if [[ "$failures" -gt 0 ]]; then
        printf '%s gate cases failed.\n' "$failures"
        exit 1
    fi
    printf 'All gate cases passed.\n'
}

if [[ "$SELF_TEST" == true ]]; then
    TMP_DIR="$(mktemp -d)"
    run_self_test
    exit 0
fi

[[ -f "$AREAS_PATH" ]] || {
    err "Area mapping not found: $AREAS_PATH"
    exit 1
}

resolve_reports
if [[ ${#REPORTS[@]} -eq 0 ]]; then
    if [[ "$MARKDOWN" == true ]]; then
        printf '## Coverage\n\nNo coverage report was produced.\n'
        exit 0
    fi
    err "Coverage file not found. Run 'scripts/coverage.sh' first to generate coverage data."
    exit 1
fi

TMP_DIR="$(mktemp -d)"
build_records

if [[ -n "$NAMESPACE" ]]; then
    report_namespace
    exit 0
fi

if [[ -n "$SOURCE_FILE" ]]; then
    report_source_file
    exit 0
fi

report_areas
