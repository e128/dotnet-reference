#!/usr/bin/env bash
# Run tests with in-process MTP coverage and generate an HTML report.
# Usage: coverage.sh [--filter TRAIT] [--configuration CONFIG] [--no-open] [--json] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
SOLUTION="$(find_solution)"
RESULTS_DIR="$ROOT/test-results"
REPORT_DIR="$RESULTS_DIR/coverage-report"
CONFIG="$ROOT/coverage.config.xml"
AREAS="$ROOT/scripts/coverage-areas.sh"

FILTER="Category=CI"
CONFIGURATION="Debug"
NO_OPEN=false
JSON=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --filter)  FILTER="$2"; shift ;;
        --configuration) CONFIGURATION="$2"; shift ;;
        --no-open) NO_OPEN=true ;;
        --json)    JSON=true ;;
        --help)
            echo "Usage: coverage.sh [--filter TRAIT] [--configuration CONFIG] [--no-open] [--json] [--help]"
            echo "  Run the test suite with Cobertura coverage and build an HTML report."
            echo "  --filter TRAIT         Trait filter passed to --filter-trait (default: Category=CI)"
            echo "  --configuration CONFIG Build and test configuration (default: Debug)"
            echo "  --no-open              Generate the report without opening a browser"
            echo "  --json                 Print one JSON object instead of progress lines"
            exit 0
            ;;
        *) err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

fail_coverage() {
    local message="$1"
    if [[ "$JSON" == true ]]; then
        jq -nc --arg error "$message" \
            '{passed: false, output_path: null, error: $error}'
    else
        err "$message"
    fi
    exit 1
}

command -v reportgenerator >/dev/null 2>&1 || fail_coverage \
    "reportgenerator not found. Install with: dotnet tool install --global dotnet-reportgenerator-globaltool"

[[ -f "$CONFIG" ]] || fail_coverage "Coverage settings not found: $CONFIG"

# Clean previous results so a stale module report cannot satisfy the gate.
rm -rf "$RESULTS_DIR"
mkdir -p "$RESULTS_DIR"

if [[ "$JSON" == false ]]; then
    info "Building $SOLUTION ($CONFIGURATION)"
fi
if ! dotnet build "$SOLUTION" --configuration "$CONFIGURATION" --nologo -clp:ErrorsOnly; then
    fail_coverage "Build failed"
fi

TEST_ARGS=(--solution "$SOLUTION" --configuration "$CONFIGURATION" --no-build)
[[ -n "$FILTER" ]] && TEST_ARGS+=(--filter-trait "$FILTER")
TEST_ARGS+=(--coverage --coverage-output-format cobertura --coverage-settings "$CONFIG" --results-directory "$RESULTS_DIR")

if [[ "$JSON" == false ]]; then
    info "Running tests with coverage (filter: ${FILTER:-none})"
fi
if ! dotnet test "${TEST_ARGS[@]}"; then
    fail_coverage "Test run failed"
fi

mapfile -t REPORTS < <(find "$RESULTS_DIR" -type f -name '*.cobertura.xml' 2>/dev/null | sort)
if [[ ${#REPORTS[@]} -eq 0 ]]; then
    fail_coverage "No coverage report produced"
fi

# The gate script merges the module reports and returns the totals as JSON.
TOTALS="$("$AREAS" --json 2>/dev/null)" || fail_coverage "Coverage report could not be read, or a measured module is missing"
LINES_HIT="$(jq -r '.lines_hit' <<<"$TOTALS")"
LINES_TOTAL="$(jq -r '.lines_total' <<<"$TOTALS")"
LINE_PCT="$(jq -r '.line_pct' <<<"$TOTALS")"

if [[ "$LINES_HIT" -eq 0 || "$LINES_TOTAL" -eq 0 ]]; then
    fail_coverage "Coverage report holds no covered lines ($LINES_HIT/$LINES_TOTAL)"
fi

if [[ "$JSON" == false ]]; then
    info "Merging ${#REPORTS[@]} module reports: $LINES_HIT/$LINES_TOTAL lines ($LINE_PCT%)"
fi

REPORT_LIST="$(IFS=';'; echo "${REPORTS[*]}")"
if ! reportgenerator "-reports:$REPORT_LIST" "-targetdir:$REPORT_DIR" "-reporttypes:Html"; then
    fail_coverage "Report generation failed"
fi

INDEX="$REPORT_DIR/index.html"
[[ -f "$INDEX" ]] || fail_coverage "Report index not found: $INDEX"

if [[ "$JSON" == true ]]; then
    jq -nc \
        --argjson reports "${#REPORTS[@]}" \
        --argjson covered "$LINES_HIT" \
        --argjson valid "$LINES_TOTAL" \
        --argjson pct "$LINE_PCT" \
        --arg path "$INDEX" \
        '{passed: true, output_path: $path, reports: $reports,
          lines_covered: $covered, lines_valid: $valid, line_pct: $pct, error: null}'
else
    ok "Report: $INDEX"
    if [[ "$NO_OPEN" == false ]]; then
        open "$INDEX" 2>/dev/null || true
    fi
fi
