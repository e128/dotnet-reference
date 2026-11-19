#!/usr/bin/env bash
# Track test provenance and attribute Red/Green TDD failures to production changes.
# Usage: tdd-track.sh <register|analyze|report|classify> [options]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

SCRIPTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(find_repo_root)"
TDD_DIR="$ROOT/.tdd"
REGISTRY="$TDD_DIR/test-registry.json"
LEDGER="$TDD_DIR/regression-catches.jsonl"
STATE="$TDD_DIR/state.json"

usage() {
    cat <<'EOF'
Usage: tdd-track.sh <command> [options]

Commands:
  register   Record a test file or test case, optionally approving its baseline.
  analyze    Run tests (or read TRX), classify failures, and update the ledger.
  report     Show regression statistics and a prompt-ready markdown summary.
  classify   Classify a prepared JSON input. Used by the test suite.

register:
  --file PATH            Register a test file.
  --test KEY             Register a test case. KEY is PATH::NAME or a bare name.
  --changed              Register every changed test file.
  --origin ORIGIN        llm-generated or human-written.
  --approve              Mark the file or test as a verified baseline.
  --list                 Print the registry.
  --json                 Print structured output.

analyze:
  --trx PATH             Read an existing TRX file or directory. Do not run tests.
  --no-run               Require --trx and only analyze.
  --origin ORIGIN        Origin for newly observed tests and files.
  --dry-run              Classify and report without writing the registry or ledger.
  --json                 Print structured output.
  --markdown             Print a markdown summary for a CI job summary.
  --quiet                Print nothing except errors.
  -- ARGS...             Forward ARGS to scripts/test.sh.

report:
  --json                 Print structured statistics.
  --markdown             Print the prompt-injection markdown summary.
  --files "a,b"          Restrict the markdown summary to these files.
  --limit N              Cap the markdown list. Default: 10.
EOF
}

valid_origin() {
    case "$1" in
        llm-generated|human-written) return 0 ;;
        *) err "Invalid origin: $1 (use llm-generated or human-written)"; return 1 ;;
    esac
}

require_tools() {
    local -a missing=()
    local tool
    for tool in git jq awk rg; do
        command -v "$tool" >/dev/null 2>&1 || missing+=("$tool")
    done
    if ((${#missing[@]} > 0)); then
        err "Missing required tools: ${missing[*]}"
        exit 1
    fi
}

ensure_layout() {
    mkdir -p "$TDD_DIR/tmp"
    if [[ ! -f "$REGISTRY" ]]; then
        printf '%s\n' '{"schema_version":1,"files":{},"tests":{}}' > "$REGISTRY"
    fi
    if [[ ! -f "$LEDGER" ]]; then
        : > "$LEDGER"
    fi
    if [[ ! -f "$STATE" ]]; then
        printf '%s\n' '{"schema_version":1,"open_events":{}}' > "$STATE"
    fi
}

head_sha() {
    git -C "$ROOT" rev-parse HEAD
}

base_sha() {
    local head
    head="$(head_sha)"
    if git -C "$ROOT" rev-parse -q --verify "HEAD~1" >/dev/null 2>&1; then
        git -C "$ROOT" rev-parse "HEAD~1"
    else
        printf '%s\n' "$head"
    fi
}

# Print every changed path from the last commit and the working tree.
collect_changed_files() {
    local base head
    base="$(base_sha)"
    head="$(head_sha)"
    {
        git -C "$ROOT" diff --name-only "${base}..${head}" 2>/dev/null || true
        git -C "$ROOT" diff --name-only "$head" 2>/dev/null || true
        git -C "$ROOT" diff --name-only --cached "$head" 2>/dev/null || true
        git -C "$ROOT" ls-files --others --exclude-standard 2>/dev/null || true
    } | sed '/^[[:space:]]*$/d' | sort -u
}

is_test_path() {
    case "$1" in
        tests/*|*/tests/*|*Tests.cs|*Test.cs) return 0 ;;
        *) return 1 ;;
    esac
}

# Emit "tests<TAB>path" or "prod<TAB>path" for each changed path.
partition_changes() {
    local path
    while IFS= read -r path; do
        if is_test_path "$path"; then
            printf 'tests\t%s\n' "$path"
        else
            case "$path" in
                src/*|benchmarks/*) printf 'prod\t%s\n' "$path" ;;
            esac
        fi
    done < <(collect_changed_files)
}

list_file_to_json() {
    jq -R -s 'split("\n") | map(select(length > 0))' "$1"
}

# Convert MTP TRX into one tab-separated record per test result:
# trx_file, test_name, outcome, class_name, method_name, message, stack.
parse_trx_to_tsv() {
    local trx_file="$1"
    awk -v file="$(basename "$trx_file")" '
        function attr(line, name,    pattern, value) {
            pattern = name "=\"[^\"]*\""
            if (match(line, pattern)) {
                value = substr(line, RSTART + length(name) + 2, RLENGTH - length(name) - 3)
                return value
            }
            return ""
        }
        function decode(value) {
            gsub(/&#13;/, " ", value)
            gsub(/&#10;/, " ", value)
            gsub(/&#9;/, " ", value)
            gsub(/&lt;/, "<", value)
            gsub(/&gt;/, ">", value)
            gsub(/&quot;/, "\"", value)
            gsub(/&apos;/, "\047", value)
            gsub(/&amp;/, "\\&", value)
            return value
        }
        function squash(value) {
            gsub(/\t/, " ", value)
            gsub(/[\r\n]+/, " ", value)
            return value
        }
        /^[[:space:]]*<UnitTestResult / {
            current = attr($0, "testId")
            order[++count] = current
            names[current] = attr($0, "testName")
            outcomes[current] = attr($0, "outcome")
            in_message = 0
            in_stack = 0
            next
        }
        /^[[:space:]]*<UnitTest / {
            definition = attr($0, "id")
            next
        }
        /^[[:space:]]*<TestMethod / {
            classes[definition] = attr($0, "className")
            methods[definition] = attr($0, "name")
            next
        }
        /^[[:space:]]*<Message>/ {
            line = $0
            sub(/^[[:space:]]*<Message>/, "", line)
            if (line ~ /<\/Message>[[:space:]]*$/) {
                sub(/<\/Message>[[:space:]]*$/, "", line)
                messages[current] = line
                in_message = 0
            } else {
                messages[current] = line
                in_message = 1
            }
            next
        }
        in_message {
            if ($0 ~ /<\/Message>/) {
                line = $0
                sub(/<\/Message>.*/, "", line)
                messages[current] = messages[current] " " line
                in_message = 0
            } else {
                messages[current] = messages[current] " " $0
            }
            next
        }
        /^[[:space:]]*<StackTrace>/ {
            line = $0
            sub(/^[[:space:]]*<StackTrace>/, "", line)
            if (line ~ /<\/StackTrace>[[:space:]]*$/) {
                sub(/<\/StackTrace>[[:space:]]*$/, "", line)
                stacks[current] = line
                in_stack = 0
            } else {
                stacks[current] = line
                in_stack = 1
            }
            next
        }
        in_stack {
            if ($0 ~ /<\/StackTrace>/) {
                line = $0
                sub(/<\/StackTrace>.*/, "", line)
                stacks[current] = stacks[current] " " line
                in_stack = 0
            } else {
                stacks[current] = stacks[current] " " $0
            }
            next
        }
        END {
            for (position = 1; position <= count; position++) {
                id = order[position]
                printf "%s\t%s\t%s\t%s\t%s\t%s\t%s\n", file, squash(decode(names[id])), \
                    outcomes[id], classes[id], methods[id], \
                    squash(decode(messages[id])), squash(decode(stacks[id]))
            }
        }
    ' "$trx_file"
}

collect_trx_files() {
    local target="$1"
    if [[ -f "$target" ]]; then
        printf '%s\n' "$target"
        return 0
    fi
    if [[ -d "$target" ]]; then
        # dotnet test writes a merged TRX beside the per-project files. Reading
        # both would count every test twice, so skip the merged tree.
        find "$target" -type f -name '*.trx' ! -path '*/merged/*' 2>/dev/null | sort
    fi
}

# Map a TRX class name to its test source file. The file index is built once.
declare -A TEST_FILE_BY_NAME=()
index_test_files() {
    local file
    while IFS= read -r file; do
        if [[ -z "${TEST_FILE_BY_NAME[${file##*/}]:-}" ]]; then
            TEST_FILE_BY_NAME["${file##*/}"]="$file"
        fi
    done < <(rg --files -g '*.cs' "$ROOT/tests" 2>/dev/null | sort)
}

resolve_test_file() {
    local class_name="$1"
    local method_name="$2"
    local short candidate=""
    short="${class_name##*.}"
    short="${short%%+*}"
    short="${short%%\`*}"
    if [[ -n "$short" && -n "${TEST_FILE_BY_NAME[$short.cs]:-}" ]]; then
        candidate="${TEST_FILE_BY_NAME[$short.cs]}"
    fi
    if [[ -z "$candidate" && -n "$short" ]]; then
        candidate="$(rg -l --glob '*.cs' -- \
            "class[[:space:]]+${short}([^A-Za-z0-9_]|$)" "$ROOT/tests" 2>/dev/null | head -1 || true)"
    fi
    if [[ -z "$candidate" && -n "$method_name" ]]; then
        candidate="$(rg -l --glob '*.cs' -- \
            "\\b${method_name}[[:space:]]*\\(" "$ROOT/tests" 2>/dev/null | head -1 || true)"
    fi
    printf '%s' "${candidate#"$ROOT/"}"
}

attach_test_files() {
    local results_file="$1"
    local map_tsv="$TDD_DIR/tmp/class-files.tsv"
    local cls method file map_json
    : > "$map_tsv"
    while IFS=$'\t' read -r cls method; do
        file="$(resolve_test_file "$cls" "$method")"
        printf '%s\t%s\t%s\n' "$cls" "$method" "$file" >> "$map_tsv"
    done < <(jq -r 'unique_by([.class_name, .method_name])[]
        | [(.class_name // ""), (.method_name // "")] | @tsv' "$results_file")
    map_json="$(jq -R -s '
        split("\n") | map(select(length > 0) | split("\t"))
        | map({key: (.[0] + "\u001f" + .[1]), value: (.[2] // "")})
        | from_entries' "$map_tsv")"
    jq --argjson map "$map_json" '
        map(.test_file = ($map[((.class_name // "") + "\u001f" + (.method_name // ""))] // ""))' \
        "$results_file"
}

# The attribution classifier. Input: {results, registry, changes}.
# Output: one classification object per result.
CLASSIFY_JQ="$(cat <<'JQ'
def failure_kind($text):
  if ($text | test("Assert\\.[A-Za-z0-9.]+\\(\\) Failure|Xunit\\.Sdk\\.[A-Za-z0-9_]+Exception|^Assert\\.|\\bExpected:|\\bActual:"))
  then "assertion"
  elif ($text | test("TimeoutException|TaskCanceledException|OperationCanceledException|timed? ?out|SocketException|HttpRequestException|connection refused|no such host|Could not load file or assembly|FileNotFoundException|TypeLoadException|MissingMethodException|MissingFieldException|DllNotFoundException|BadImageFormatException|OutOfMemoryException|StackOverflowException|MockException|Moq\\.|NSubstitute|AutoFixture|fixture|threw in its constructor|constructor parameters did not have matching|IAsyncLifetime|Exception during test discovery|test host|active test run was aborted|CS[0-9]{4}|MSB[0-9]{4}|build failed|compilation error"; "i"))
  then "environment"
  else "behavior" end;

. as $in
| ($in.registry.tests // {}) as $tests
| ($in.registry.files // {}) as $files
| ($in.changes.prod // []) as $prod
| ($in.changes.tests // []) as $changed_tests
| ($in.results // [])
| map(
    . as $r
    | ($r.test_file // "") as $tf
    | ($tf + "::" + ($r.test_name // "")) as $key
    | ($tests[$key]
       // (($tests | to_entries)
           | map(select(.key | endswith("::" + ($r.test_name // ""))))
           | .[0].value)
       // null) as $entry
    | (($r.message // "") + "\n" + ($r.stack // "")) as $text
    | failure_kind($text) as $kind
    | (($entry.origin) // ($files[$tf].origin) // "human-written") as $origin
    | (($entry.verified_baseline // false) == true) as $verified
    | (if $r.outcome != "Failed" then "passed"
       elif $kind == "environment" then "harness_or_env_error"
       elif ($verified | not) then "unverified_red"
       elif (($prod | length) == 0) and (($changed_tests | length) > 0)
         then "harness_or_env_error"
       elif ($prod | length) > 0 then "confirmed_regression"
       else "harness_or_env_error" end) as $class
    | (if $class == "passed" then "passed"
       elif $kind == "environment" then "environment_failure"
       elif ($verified | not) then "no_verified_baseline"
       elif (($prod | length) == 0) and (($changed_tests | length) > 0)
         then "test_only_change"
       elif ($prod | length) > 0 then "production_change_with_behavior_failure"
       else "no_production_change" end) as $reason
    | {
        test_file: (if $tf == "" then null else $tf end),
        test_name: ($r.test_name // ""),
        method_name: ($r.method_name // ""),
        class_name: ($r.class_name // ""),
        outcome: ($r.outcome // ""),
        classification: $class,
        reason: $reason,
        failure_kind: (if $r.outcome == "Failed" then $kind else "none" end),
        origin: $origin,
        verified_baseline: $verified,
        first_green_commit: ($entry.first_green_commit // null),
        failure_summary: {
          kind: (if $r.outcome == "Failed" then $kind else "none" end),
          message: (($r.message // "") | split("\n")[0] | .[0:240])
        }
      }
  )
JQ
)"

# Build caught and resolved events from classifications, the ledger, and state.
# Input: {timestamp, head, base, run_id, changes, classifications, ledger, state}.
EVENTS_JQ="$(cat <<'JQ'
def test_key: ((.test_file // "") + "::" + (.test_name // ""));

. as $in
| ($in.state.open_events // {}) as $state_open
| (reduce $in.ledger[] as $e ({};
     if $e.event_type == "caught" then .[($e | test_key)] = $e
     elif $e.event_type == "resolved" then del(.[($e | test_key)])
     else . end)) as $ledger_open
| (reduce ($ledger_open | keys_unsorted[]) as $k ($state_open;
     if has($k) then .
     else .[$k] = {
       event_id: $ledger_open[$k].event_id,
       caught_at: $ledger_open[$k].timestamp,
       base_commit: $ledger_open[$k].base_commit,
       test_file: $ledger_open[$k].test_file,
       test_name: $ledger_open[$k].test_name,
       test_origin: $ledger_open[$k].test_origin,
       failure_summary: $ledger_open[$k].failure_summary,
       changed_prod_files: $ledger_open[$k].changed_prod_files,
       iterations: 1
     } end)) as $open
| reduce ($in.classifications | to_entries[]) as $item (
    { open: $open, events: [] };
    ($item.value) as $c
    | ($c.test_file // "") as $tf
    | ($tf + "::" + $c.test_name) as $key
    | ($item.key) as $index
    | if $c.classification == "confirmed_regression" then
        if .open[$key] != null then
          .open[$key] = (.open[$key] + {
            iterations: ((.open[$key].iterations // 1) + 1),
            last_seen_commit: $in.head,
            last_seen_at: $in.timestamp
          })
        else
          ($in.run_id + "-" + ($index | tostring)) as $event_id
          | .open[$key] = {
              event_id: $event_id,
              caught_at: $in.timestamp,
              base_commit: $in.base,
              test_file: $tf,
              test_name: $c.test_name,
              test_origin: $c.origin,
              failure_summary: $c.failure_summary,
              changed_prod_files: $in.changes.prod,
              iterations: 1
            }
          | .events += [{
              event_id: $event_id,
              timestamp: $in.timestamp,
              event_type: "caught",
              base_commit: $in.base,
              current_commit: $in.head,
              test_file: $tf,
              test_name: $c.test_name,
              test_origin: $c.origin,
              changed_prod_files: $in.changes.prod,
              failure_summary: $c.failure_summary,
              resolution_details: null
            }]
        end
      elif ($c.classification == "passed" and .open[$key] != null) then
        (.open[$key]) as $event
        | .events += [{
            event_id: ($in.run_id + "-r-" + ($index | tostring)),
            timestamp: $in.timestamp,
            event_type: "resolved",
            base_commit: $event.base_commit,
            current_commit: $in.head,
            test_file: $tf,
            test_name: $c.test_name,
            test_origin: $event.test_origin,
            changed_prod_files: $in.changes.prod,
            failure_summary: $event.failure_summary,
            resolution_details: {
              caught_event_id: $event.event_id,
              fix_commit: $in.head,
              iterations_to_green: ($event.iterations // 1),
              changed_prod_files: $in.changes.prod
            }
          }]
        | del(.open[$key])
      else . end
  )
| { events: .events, open_events: .open }
JQ
)"

cmd_register() {
    local file=""
    local test_key=""
    local origin=""
    local changed=false
    local approve=false
    local list=false
    local json=false
    local head ts registry_tmp
    local -a changed_tests=()
    local path kind

    while [[ $# -gt 0 ]]; do
        case "$1" in
            --file) file="$2"; shift ;;
            --test) test_key="$2"; shift ;;
            --origin) origin="$2"; shift ;;
            --changed) changed=true ;;
            --approve) approve=true ;;
            --list) list=true ;;
            --json) json=true ;;
            -h|--help) usage; exit 0 ;;
            *) err "Unknown argument: $1"; exit 1 ;;
        esac
        shift
    done

    if [[ -n "$origin" ]]; then
        valid_origin "$origin" || exit 1
    fi

    ensure_layout
    require_tools
    head="$(head_sha)"
    ts="$(iso_timestamp)"

    if [[ "$list" == true ]]; then
        jq '.' "$REGISTRY"
        return 0
    fi

    if [[ "$changed" == true ]]; then
        if [[ -z "$origin" ]]; then
            err "--changed requires --origin so new tests are not mislabelled"
            exit 1
        fi
        while IFS=$'\t' read -r kind path; do
            [[ "$kind" == "tests" ]] && changed_tests+=("$path")
        done < <(partition_changes)
        if ((${#changed_tests[@]} == 0)); then
            warn "No changed test files found"
            return 0
        fi
        registry_tmp="$(mktemp "$REGISTRY.XXXXXX")"
        jq --arg origin "$origin" --arg ts "$ts" --arg head "$head" \
            --argjson files "$(printf '%s\n' "${changed_tests[@]}" | jq -R -s 'split("\n") | map(select(length > 0))')" '
            reduce $files[] as $path (.;
                .files[$path] = (.files[$path] // {
                    origin: $origin,
                    verified_baseline: false,
                    registered_at: $ts,
                    registered_commit: $head
                })
                | .files[$path].origin = $origin) | .schema_version = 1
        ' "$REGISTRY" > "$registry_tmp"
        mv "$registry_tmp" "$REGISTRY"
    fi

    if [[ -n "$file" ]]; then
        registry_tmp="$(mktemp "$REGISTRY.XXXXXX")"
        jq --arg file "$file" --arg origin "$origin" --arg ts "$ts" --arg head "$head" \
            --argjson approve "$approve" '
            .files = (.files // {}) | .tests = (.tests // {})
            | .files[$file] = (.files[$file] // {
                origin: ($origin | if . == "" then "human-written" else . end),
                verified_baseline: false,
                registered_at: $ts,
                registered_commit: $head
            })
            | if $origin != "" then .files[$file].origin = $origin else . end
            | if $approve then
                .files[$file].verified_baseline = true
                | .tests |= with_entries(
                    if .value.test_file == $file then
                      .value.verified_baseline = true
                      | .value.first_green_commit = (.value.first_green_commit // $head)
                      | .value.first_green_at = (.value.first_green_at // $ts)
                    else . end)
              else . end
            | .schema_version = 1
        ' "$REGISTRY" > "$registry_tmp"
        mv "$registry_tmp" "$REGISTRY"
        if [[ "$json" == true ]]; then
            jq --arg file "$file" '{file: $file, entry: .files[$file]}' "$REGISTRY"
        else
            ok "Registered test file: $file"
        fi
    fi

    if [[ -n "$test_key" ]]; then
        registry_tmp="$(mktemp "$REGISTRY.XXXXXX")"
        jq --arg key "$test_key" --arg origin "$origin" --arg ts "$ts" --arg head "$head" \
            --argjson approve "$approve" '
            .files = (.files // {}) | .tests = (.tests // {})
            | ($key | contains("::")) as $qualified
            | ($key | split("::")) as $parts
            | if $qualified then
                .files[$parts[0]] = (.files[$parts[0]] // {
                    origin: ($origin | if . == "" then "human-written" else . end),
                    verified_baseline: false,
                    registered_at: $ts,
                    registered_commit: $head
                })
                | .tests[$key] = (.tests[$key] // {
                    test_file: $parts[0],
                    test_name: $parts[1],
                    method_name: "",
                    class_name: "",
                    origin: ($origin | if . == "" then "human-written" else . end),
                    verified_baseline: false,
                    first_green_commit: null,
                    first_green_at: null
                })
                | if $origin != "" then .tests[$key].origin = $origin else . end
                | if $approve then
                    .tests[$key].verified_baseline = true
                    | .tests[$key].first_green_commit = (.tests[$key].first_green_commit // $head)
                    | .tests[$key].first_green_at = (.tests[$key].first_green_at // $ts)
                  else . end
              else
                .tests |= with_entries(
                    if .key | endswith("::" + $key) then
                      .value.origin = (if $origin == "" then .value.origin else $origin end)
                      | if $approve then
                          .value.verified_baseline = true
                          | .value.first_green_commit = (.value.first_green_commit // $head)
                          | .value.first_green_at = (.value.first_green_at // $ts)
                        else . end
                    else . end)
              end
            | .schema_version = 1
        ' "$REGISTRY" > "$registry_tmp"
        mv "$registry_tmp" "$REGISTRY"
        if [[ "$json" == true ]]; then
            jq --arg key "$test_key" '{test: $key, matches: [.tests | to_entries[] | select(.key | endswith($key)) | .value]}' "$REGISTRY"
        else
            ok "Registered test: $test_key"
        fi
    fi

    if [[ -z "$file" && -z "$test_key" && "$changed" == false && "$list" == false ]]; then
        usage
        exit 1
    fi
}

cmd_analyze() {
    local no_run=false
    local trx_path=""
    local origin=""
    local json=false
    local quiet=false
    local dry_run=false
    local markdown=false
    local -a forwarded=()
    local head base ts run_id rc=0
    local trx_target results_tsv results_file input_file classified_file
    local events_input events_json prod_file tests_file prod_json tests_json
    local registry_tmp state_tmp
    local test_counts class_counts event_counts
    local -a changed_tests=()
    local kind path trx_file
    local trx_found=false
    local run_output=""

    while [[ $# -gt 0 ]]; do
        case "$1" in
            --no-run) no_run=true ;;
            --trx) trx_path="$2"; shift ;;
            --origin) origin="$2"; shift ;;
            --dry-run) dry_run=true ;;
            --json) json=true ;;
            --markdown) markdown=true ;;
            --quiet) quiet=true ;;
            --) shift; forwarded=("$@"); break ;;
            -h|--help) usage; exit 0 ;;
            -*) err "Unknown flag: $1"; exit 1 ;;
            *) forwarded+=("$1") ;;
        esac
        shift
    done

    if [[ -n "$origin" ]]; then
        valid_origin "$origin" || exit 1
    fi
    if [[ "$no_run" == true && -z "$trx_path" ]]; then
        err "--no-run requires --trx PATH"
        exit 1
    fi

    ensure_layout
    require_tools
    index_test_files
    head="$(head_sha)"
    base="$(base_sha)"
    ts="$(iso_timestamp)"
    run_id="$(uuidgen 2>/dev/null | tr '[:upper:]' '[:lower:]' || true)"
    if [[ -z "$run_id" ]]; then
        run_id="${ts}-${RANDOM}"
    fi

    trx_target="$trx_path"
    if [[ -z "$trx_path" ]]; then
        trx_target="$TDD_DIR/tmp/trx/$(printf '%s' "$ts" | tr ':' '-')"
        mkdir -p "$trx_target"
        run_output="$TDD_DIR/tmp/last-test-output.txt"
        if [[ ${#forwarded[@]} -gt 0 ]]; then
            "$SCRIPTS_DIR/test.sh" --trx-dir "$trx_target" "${forwarded[@]}" \
                > "$run_output" 2>&1 || rc=$?
        else
            "$SCRIPTS_DIR/test.sh" --trx-dir "$trx_target" \
                > "$run_output" 2>&1 || rc=$?
        fi
    fi

    results_tsv="$TDD_DIR/tmp/results.tsv"
    : > "$results_tsv"
    while IFS= read -r trx_file; do
        trx_found=true
        parse_trx_to_tsv "$trx_file" >> "$results_tsv"
    done < <(collect_trx_files "$trx_target")

    if [[ "$trx_found" == false || ! -s "$results_tsv" ]]; then
        if [[ "$json" == true ]]; then
            jq -nc --arg status "$([[ $rc -ne 0 ]] && echo harness_or_env_error || echo no-results)" \
                --argjson exit_code "$rc" \
                --arg output "${run_output}" \
                '{status: $status, exit_code: $exit_code, test_output: $output}'
        else
            err "No TRX results found. The build or test host failed before discovery."
            [[ -n "$run_output" ]] && tail -20 "$run_output" >&2
        fi
        exit 2
    fi

    results_file="$TDD_DIR/tmp/results.json"
    jq -R -s '
        split("\n") | map(select(length > 0) | split("\t"))
        | map({
            trx_file: .[0],
            test_name: (.[1] // ""),
            outcome: (.[2] // ""),
            class_name: (.[3] // ""),
            method_name: (.[4] // ""),
            message: (.[5] // ""),
            stack: (.[6] // "")
          })' "$results_tsv" > "$results_file"
    attach_test_files "$results_file" > "$results_file.tmp"
    mv "$results_file.tmp" "$results_file"

    prod_file="$TDD_DIR/tmp/prod-files.txt"
    tests_file="$TDD_DIR/tmp/test-files.txt"
    : > "$prod_file"
    : > "$tests_file"
    while IFS=$'\t' read -r kind path; do
        case "$kind" in
            prod) printf '%s\n' "$path" >> "$prod_file" ;;
            tests) printf '%s\n' "$path" >> "$tests_file" ;;
        esac
    done < <(partition_changes)
    prod_json="$(list_file_to_json "$prod_file")"
    tests_json="$(list_file_to_json "$tests_file")"

    input_file="$TDD_DIR/tmp/analysis-input.json"
    jq -n --slurpfile results "$results_file" --slurpfile reg "$REGISTRY" \
        --argjson prod "$prod_json" --argjson tests "$tests_json" '
        {results: $results[0], registry: $reg[0],
         changes: {prod: $prod, tests: $tests}}' > "$input_file"

    classified_file="$TDD_DIR/tmp/classified.json"
    jq "$CLASSIFY_JQ" "$input_file" > "$classified_file"

    registry_tmp="$(mktemp "$REGISTRY.XXXXXX")"
    jq --slurpfile results "$results_file" --arg head "$head" --arg ts "$ts" \
        --arg origin "$origin" --argjson changed_tests "$tests_json" '
        (if $origin == "" then null else $origin end) as $o
        | .files = (.files // {}) | .tests = (.tests // {})
        | reduce ($results[0][] | select((.test_file // "") != "")) as $r (
            .;
            .files[$r.test_file] = (.files[$r.test_file] // {
                origin: ($o // "human-written"),
                verified_baseline: false,
                registered_at: $ts,
                registered_commit: $head
            })
            | ($r.test_file + "::" + $r.test_name) as $key
            | (.tests[$key] // {}) as $old
            | .tests[$key] = {
                test_file: $r.test_file,
                test_name: $r.test_name,
                method_name: ($r.method_name // ""),
                class_name: ($r.class_name // ""),
                origin: ($old.origin // .files[$r.test_file].origin // $o
                         // "human-written"),
                verified_baseline: (if $r.outcome == "Passed" then true
                                    else ($old.verified_baseline // false) end),
                first_green_commit: (if $r.outcome == "Passed"
                                     then ($old.first_green_commit // $head)
                                     else ($old.first_green_commit // null) end),
                first_green_at: (if $r.outcome == "Passed"
                                 then ($old.first_green_at // $ts)
                                 else ($old.first_green_at // null) end)
              }
          )
        | reduce ($changed_tests[]) as $file (.;
            .files[$file] = (.files[$file] // {
                origin: ($o // "human-written"),
                verified_baseline: false,
                registered_at: $ts,
                registered_commit: $head
            }))
        | .schema_version = 1
    ' "$REGISTRY" > "$registry_tmp"

    events_input="$TDD_DIR/tmp/events-input.json"
    jq -n --slurpfile c "$classified_file" --slurpfile ledger "$LEDGER" \
        --slurpfile state "$STATE" --arg ts "$ts" --arg head "$head" \
        --arg base "$base" --arg run_id "$run_id" --argjson prod "$prod_json" '
        {timestamp: $ts, head: $head, base: $base, run_id: $run_id,
         changes: {prod: $prod}, classifications: $c[0],
         ledger: $ledger, state: $state[0]}' > "$events_input"
    events_json="$(jq "$EVENTS_JQ" "$events_input")"

    if [[ "$dry_run" == false ]]; then
        if [[ "$(jq '.events | length' <<<"$events_json")" -gt 0 ]]; then
            jq -c '.events[]' <<<"$events_json" >> "$LEDGER"
        fi
        state_tmp="$(mktemp "$STATE.XXXXXX")"
        jq --arg ts "$ts" --arg head "$head" --arg base "$base" '
            {schema_version: 1,
             open_events: (.open_events // {}),
             last_run: {timestamp: $ts, head: $head, base: $base}}' \
            <<<"$events_json" > "$state_tmp"
        mv "$registry_tmp" "$REGISTRY"
        mv "$state_tmp" "$STATE"
    else
        rm -f "$registry_tmp"
    fi

    test_counts="$(jq '
        group_by(.outcome) | map({key: .[0].outcome, value: length})
        | from_entries' "$classified_file")"
    class_counts="$(jq '
        group_by(.classification) | map({key: .[0].classification, value: length})
        | from_entries' "$classified_file")"
    event_counts="$(jq '
        [.events[].event_type] | group_by(.) | map({key: .[0], value: length})
        | from_entries' <<<"$events_json")"

    local total failed_count
    total="$(jq 'length' "$classified_file")"
    failed_count="$(jq '[.[] | select(.outcome == "Failed")] | length' "$classified_file")"

    if [[ "$markdown" == true ]]; then
        local caught resolved
        caught="$(jq '[.events[] | select(.event_type == "caught")] | length' <<<"$events_json")"
        resolved="$(jq '[.events[] | select(.event_type == "resolved")] | length' <<<"$events_json")"
        jq -r --argjson total "$total" --argjson failed "$failed_count" \
            --argjson prod "$prod_json" --argjson caught "$caught" \
            --argjson resolved "$resolved" '
            def count($class): [.[] | select(.classification == $class)] | length;
            "## TDD Attribution",
            "",
            "Tests: \($total). Failed: \($failed).",
            "",
            "| Classification | Count |",
            "| --- | ---: |",
            "| confirmed_regression | \(count("confirmed_regression")) |",
            "| unverified_red | \(count("unverified_red")) |",
            "| harness_or_env_error | \(count("harness_or_env_error")) |",
            "",
            "Events: \($caught) caught, \($resolved) resolved.",
            "",
            "### Confirmed regressions",
            "",
            ([.[] | select(.classification == "confirmed_regression")] as $found
             | if ($found | length) == 0 then "_No confirmed regressions._"
               else $found[]
                 | "- `\(.test_name)` in `\(.test_file // "unknown")` "
                   + "(\(.origin)). \(.failure_summary.kind): "
                   + (.failure_summary.message // "")
                   + (if ($prod | length) > 0
                      then " Changes: " + ($prod | join(", ")) else "" end)
               end),
            ""
        ' "$classified_file"
    elif [[ "$json" == true ]]; then
        jq -n --slurpfile classified "$classified_file" \
            --argjson tests "$test_counts" --argjson classifications "$class_counts" \
            --argjson events "$event_counts" --argjson prod "$prod_json" \
            --argjson changed_tests "$tests_json" --argjson dry "$dry_run" '
            {status: "ok", dry_run: $dry, tests: $tests,
             classifications: $classifications,
             events: $events, changed_prod_files: $prod,
             changed_test_files: $changed_tests,
             details: [$classified[0][] | {
               test_file, test_name, classification, reason, origin, failure_kind
             }]}' | jq .
    elif [[ "$quiet" == false ]]; then
        info "TDD analysis: ${total} tests, ${failed_count} failed"
        jq -r '
            .[] | select(.classification != "passed")
            | "  \(.classification)\t\(.test_name)\t\(.origin)\t\(.reason)"' \
            "$classified_file" | sort
        jq -r '
            .events[] | "  \(.event_type)\t\(.test_name)\t\(.failure_summary.kind)"' \
            <<<"$events_json"
        dim "Ledger: ${LEDGER#"$ROOT/"}"
    fi

    if [[ "$rc" -ne 0 ]]; then
        exit "$rc"
    fi
    if [[ "$failed_count" -gt 0 ]]; then
        exit 1
    fi
}

cmd_report() {
    local json=false
    local markdown=false
    local limit=10
    local files_arg=""
    local ledger_json files_json
    local -a requested_files=()
    local path

    while [[ $# -gt 0 ]]; do
        case "$1" in
            --json) json=true ;;
            --markdown) markdown=true ;;
            --files) files_arg="$2"; shift ;;
            --limit) limit="$2"; shift ;;
            -h|--help) usage; exit 0 ;;
            *) err "Unknown flag: $1"; exit 1 ;;
        esac
        shift
    done

    ensure_layout
    require_tools
    ledger_json="$(jq -s '.' "$LEDGER")"

    if [[ -n "$files_arg" ]]; then
        IFS=',' read -r -a requested_files <<<"$files_arg"
    else
        while IFS= read -r path; do
            requested_files+=("$path")
        done < <(collect_changed_files)
    fi
    if ((${#requested_files[@]} > 0)); then
        files_json="$(printf '%s\n' "${requested_files[@]}" \
            | jq -R -s 'split("\n") | map(select(length > 0))')"
    else
        files_json='[]'
    fi

    if [[ "$markdown" == true ]]; then
        local open_json lines
        open_json="$(jq -c '.open_events // {}' "$STATE")"
        lines="$(jq -r --argjson files "$files_json" --argjson open "$open_json" \
            --argjson limit "$limit" '
            def touches($files):
              . as $event
              | if ($files | length) == 0 then true
                else (($files | index($event.test_file // "")) != null)
                  or ([($event.changed_prod_files // [])[]
                       | . as $changed
                       | select(($files | index($changed)) != null)]
                      | length > 0)
                end;
            [.[] | select(.event_type == "caught")]
            | map(select(touches($files)))
            | .[-$limit:]
            | .[] as $event
            | "- \($event.timestamp[0:10]) `\($event.test_name)` (\($event.test_origin)), "
              + (if $open[($event.test_file // "") + "::" + $event.test_name] != null
                 then "open" else "resolved" end)
              + ". \($event.failure_summary.kind // "failure"): "
              + ($event.failure_summary.message // "")
              + (if (($event.changed_prod_files // []) | length) > 0
                 then ". Files: " + ($event.changed_prod_files | join(", "))
                 else "" end)
        ' <<<"$ledger_json")"
        printf '## Past TDD regressions in touched files\n\n'
        if [[ -n "$lines" ]]; then
            printf '%s\n' "$lines"
        else
            printf '_No recorded regressions touch the current changes._\n'
        fi
        return 0
    fi

    local stats
    stats="$(jq -n --slurpfile events "$LEDGER" --slurpfile state "$STATE" '
        ($state[0].open_events // {}) as $open
        | ($events) as $all
        | {
            caught_total: ([$all[] | select(.event_type == "caught")] | length),
            resolved_total: ([$all[] | select(.event_type == "resolved")] | length),
            open_total: ($open | length),
            by_origin: ([$all[] | select(.event_type == "caught")]
              | group_by(.test_origin)
              | map({key: (.[0].test_origin // "unknown"), value: length})
              | from_entries),
            hotspots: ([$all[] | select(.event_type == "caught")
                        | .changed_prod_files[]?]
              | group_by(.) | map({file: .[0], count: length})
              | sort_by(-.count) | .[0:10]),
            avg_iterations_to_green: ([$all[]
              | select(.event_type == "resolved")
              | .resolution_details.iterations_to_green]
              | if length == 0 then 0 else (add / length) end)
          }')"

    if [[ "$json" == true ]]; then
        jq . <<<"$stats"
        return 0
    fi

    jq -r '
        "Confirmed regressions caught: \(.caught_total)",
        "Resolved: \(.resolved_total), open: \(.open_total)",
        "Average iterations to green: \(.avg_iterations_to_green)",
        "By origin: " + (.by_origin
          | if length == 0 then "none"
            else to_entries | map("\(.key)=\(.value)") | join(", ") end),
        "Hotspots:",
        (.hotspots[] | "  \(.file): \(.count)")' <<<"$stats"
}

cmd_classify() {
    local input=""
    if [[ $# -gt 0 ]]; then
        input="$1"
    fi
    if [[ -z "$input" || ! -f "$input" ]]; then
        err "Usage: tdd-track.sh classify INPUT.json"
        exit 1
    fi
    jq "$CLASSIFY_JQ" "$input"
}

main() {
    local command="${1:-}"
    if [[ -z "$command" || "$command" == "-h" || "$command" == "--help" ]]; then
        usage
        exit 0
    fi
    shift
    case "$command" in
        register) cmd_register "$@" ;;
        analyze) cmd_analyze "$@" ;;
        report) cmd_report "$@" ;;
        classify) cmd_classify "$@" ;;
        *) err "Unknown command: $command"; usage; exit 1 ;;
    esac
}

main "$@"
