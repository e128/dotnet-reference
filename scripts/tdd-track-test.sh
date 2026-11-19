#!/usr/bin/env bash
# Test tdd-track.sh classification, registry, ledger, and report behavior.
# Usage: tdd-track-test.sh [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

if [[ "${1:-}" == "--help" ]]; then
    echo "Usage: tdd-track-test.sh"
    echo "  Run tdd-track.sh against fixture git repositories and synthetic TRX."
    echo "  Nothing touches this repository, its history, or the network."
    exit 0
fi

SCRIPTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRATCH="$SCRIPTS_DIR/../.claude/tmp"
mkdir -p "$SCRATCH"
SANDBOX="$(mktemp -d "$SCRATCH/tdd-track-test.XXXXXX")"
if [[ "${TDD_KEEP:-}" == "1" ]]; then
    warn "Keeping sandbox: $SANDBOX"
else
    trap 'rm -rf "${SANDBOX:?}"' EXIT
fi

# Isolate git from the caller's configuration and from any enclosing repository.
unset GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE GIT_PREFIX
export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null GIT_TERMINAL_PROMPT=0

FIXTURES="$SANDBOX/fixtures"
mkdir -p "$FIXTURES"

TESTS=0 PASSED=0 FAILED=0 ASSERTS=0 CASE_FAILS=0
FIX="" WORK="" RC=0

g() { git -C "$WORK" "$@"; }
ttd() { (cd "$WORK" && ./scripts/tdd-track.sh "$@"); }

expect() {
    local desc="$1"
    shift
    ASSERTS=$((ASSERTS + 1))
    if ! "$@"; then
        CASE_FAILS=$((CASE_FAILS + 1))
        err "  FAIL: $desc"
    fi
}

run_analyze() {
    RC=0
    (cd "$WORK" && ./scripts/tdd-track.sh analyze "$@") \
        > "$FIX/out.json" 2> "$FIX/err.txt" || RC=$?
}

run_register() {
    RC=0
    (cd "$WORK" && ./scripts/tdd-track.sh register "$@") \
        > "$FIX/register.json" 2> "$FIX/register.err" || RC=$?
}

classify_field() {
    jq -r --arg test "$1" --arg field "$2" \
        '.details[] | select(.test_name == $test) | .[$field]' "$FIX/out.json"
}

write_registry() {
    cat > "$WORK/.tdd/test-registry.json" <<'JSON'
{
  "schema_version": 1,
  "files": {
    "tests/Demo/CalcTests.cs": {
      "origin": "llm-generated",
      "verified_baseline": false,
      "registered_at": "2026-01-01T00:00:00Z",
      "registered_commit": "base"
    }
  },
  "tests": {
    "tests/Demo/CalcTests.cs::Demo.Tests.CalcTests.Add_ReturnsSum":
      {"test_file": "tests/Demo/CalcTests.cs", "test_name": "Demo.Tests.CalcTests.Add_ReturnsSum", "method_name": "Add_ReturnsSum", "class_name": "Demo.Tests.CalcTests", "origin": "llm-generated", "verified_baseline": true, "first_green_commit": "base"},
    "tests/Demo/CalcTests.cs::Demo.Tests.CalcTests.Multiply_ReturnsWrong":
      {"test_file": "tests/Demo/CalcTests.cs", "test_name": "Demo.Tests.CalcTests.Multiply_ReturnsWrong", "method_name": "Multiply_ReturnsWrong", "class_name": "Demo.Tests.CalcTests", "origin": "llm-generated", "verified_baseline": true, "first_green_commit": "base"},
    "tests/Demo/CalcTests.cs::Demo.Tests.CalcTests.Wait_Completes":
      {"test_file": "tests/Demo/CalcTests.cs", "test_name": "Demo.Tests.CalcTests.Wait_Completes", "method_name": "Wait_Completes", "class_name": "Demo.Tests.CalcTests", "origin": "llm-generated", "verified_baseline": true, "first_green_commit": "base"},
    "tests/Demo/CalcTests.cs::Demo.Tests.CalcTests.Mock_IsConfigured":
      {"test_file": "tests/Demo/CalcTests.cs", "test_name": "Demo.Tests.CalcTests.Mock_IsConfigured", "method_name": "Mock_IsConfigured", "class_name": "Demo.Tests.CalcTests", "origin": "llm-generated", "verified_baseline": true, "first_green_commit": "base"},
    "tests/Demo/CalcTests.cs::Demo.Tests.CalcTests.Throws_Null":
      {"test_file": "tests/Demo/CalcTests.cs", "test_name": "Demo.Tests.CalcTests.Throws_Null", "method_name": "Throws_Null", "class_name": "Demo.Tests.CalcTests", "origin": "llm-generated", "verified_baseline": true, "first_green_commit": "base"},
    "tests/Demo/CalcTests.cs::Demo.Tests.CalcTests.Quote_Message":
      {"test_file": "tests/Demo/CalcTests.cs", "test_name": "Demo.Tests.CalcTests.Quote_Message", "method_name": "Quote_Message", "class_name": "Demo.Tests.CalcTests", "origin": "llm-generated", "verified_baseline": true, "first_green_commit": "base"}
  }
}
JSON
}

make_fixture() {
    FIX="$(mktemp -d "$SANDBOX/case.XXXXXX")"
    WORK="$FIX/work"
    mkdir -p "$WORK/scripts" "$WORK/src/Demo" "$WORK/tests/Demo" "$WORK/.tdd"
    git init -q -b main "$WORK"
    g config user.name "Fixture User"
    g config user.email "fixture@example.com"
    g config commit.gpgsign false
    cp "$SCRIPTS_DIR/lib.sh" "$SCRIPTS_DIR/tdd-track.sh" "$WORK/scripts/"
    chmod +x "$WORK/scripts/tdd-track.sh"
    printf 'namespace Demo;\npublic sealed class Calc { public int Add(int a, int b) => a + b; }\n' \
        > "$WORK/src/Demo/Calc.cs"
    printf 'namespace Demo.Tests;\npublic sealed class CalcTests { }\n' \
        > "$WORK/tests/Demo/CalcTests.cs"
    printf '.tdd/tmp/\n.tdd/state.json\n' > "$WORK/.gitignore"
    write_registry
    : > "$WORK/.tdd/regression-catches.jsonl"
    printf '{"schema_version":1,"open_events":{}}\n' > "$WORK/.tdd/state.json"
    g add -A
    g commit -q -m "base"
}

write_trx_fixtures() {
    cat > "$FIXTURES/fail.trx" <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<TestRun id="00000000-0000-0000-0000-000000000001" name="fixture" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult testId="t1" testName="Demo.Tests.CalcTests.Add_ReturnsSum" outcome="Failed">
      <Output>
        <ErrorInfo>
          <Message>Assert.Equal() Failure: Values differ
Expected: 3
Actual:   4</Message>
          <StackTrace>   at Demo.Tests.CalcTests.Add_ReturnsSum() in tests/Demo/CalcTests.cs:line 10</StackTrace>
        </ErrorInfo>
      </Output>
    </UnitTestResult>
    <UnitTestResult testId="t2" testName="Demo.Tests.CalcTests.Multiply_ReturnsWrong" outcome="Failed">
      <Output>
        <ErrorInfo>
          <Message>Assert.Equal() Failure: Values differ</Message>
          <StackTrace>   at Demo.Tests.CalcTests.Multiply_ReturnsWrong() in tests/Demo/CalcTests.cs:line 14</StackTrace>
        </ErrorInfo>
      </Output>
    </UnitTestResult>
    <UnitTestResult testId="t3" testName="Demo.Tests.CalcTests.Wait_Completes" outcome="Failed">
      <Output>
        <ErrorInfo>
          <Message>System.TimeoutException : The operation timed out</Message>
          <StackTrace>   at Demo.Tests.CalcTests.Wait_Completes() in tests/Demo/CalcTests.cs:line 18</StackTrace>
        </ErrorInfo>
      </Output>
    </UnitTestResult>
    <UnitTestResult testId="t4" testName="Demo.Tests.CalcTests.Mock_IsConfigured" outcome="Failed">
      <Output>
        <ErrorInfo>
          <Message>Moq.MockException : Expected invocation on the mock failed</Message>
          <StackTrace>   at Demo.Tests.CalcTests.Mock_IsConfigured() in tests/Demo/CalcTests.cs:line 22</StackTrace>
        </ErrorInfo>
      </Output>
    </UnitTestResult>
    <UnitTestResult testId="t5" testName="Demo.Tests.CalcTests.Throws_Null" outcome="Failed">
      <Output>
        <ErrorInfo>
          <Message>System.NullReferenceException : Object reference not set</Message>
          <StackTrace>   at Demo.Tests.CalcTests.Throws_Null() in tests/Demo/CalcTests.cs:line 26</StackTrace>
        </ErrorInfo>
      </Output>
    </UnitTestResult>
    <UnitTestResult testId="t6" testName="Demo.Tests.CalcTests.Brand_New" outcome="Failed">
      <Output>
        <ErrorInfo>
          <Message>Assert.True() Failure</Message>
          <StackTrace>   at Demo.Tests.CalcTests.Brand_New() in tests/Demo/CalcTests.cs:line 30</StackTrace>
        </ErrorInfo>
      </Output>
    </UnitTestResult>
    <UnitTestResult testId="t7" testName="Demo.Tests.CalcTests.Quote_Message" outcome="Failed">
      <Output>
        <ErrorInfo>
          <Message>Assert.Equal() Failure: &quot;expected&quot; != &quot;actual&quot;</Message>
          <StackTrace>   at Demo.Tests.CalcTests.Quote_Message() in tests/Demo/CalcTests.cs:line 34</StackTrace>
        </ErrorInfo>
      </Output>
    </UnitTestResult>
  </Results>
  <TestDefinitions>
    <UnitTest name="Demo.Tests.CalcTests.Add_ReturnsSum" id="t1">
      <TestMethod codeBase="fixture.dll" className="Demo.Tests.CalcTests" name="Add_ReturnsSum" />
    </UnitTest>
    <UnitTest name="Demo.Tests.CalcTests.Multiply_ReturnsWrong" id="t2">
      <TestMethod codeBase="fixture.dll" className="Demo.Tests.CalcTests" name="Multiply_ReturnsWrong" />
    </UnitTest>
    <UnitTest name="Demo.Tests.CalcTests.Wait_Completes" id="t3">
      <TestMethod codeBase="fixture.dll" className="Demo.Tests.CalcTests" name="Wait_Completes" />
    </UnitTest>
    <UnitTest name="Demo.Tests.CalcTests.Mock_IsConfigured" id="t4">
      <TestMethod codeBase="fixture.dll" className="Demo.Tests.CalcTests" name="Mock_IsConfigured" />
    </UnitTest>
    <UnitTest name="Demo.Tests.CalcTests.Throws_Null" id="t5">
      <TestMethod codeBase="fixture.dll" className="Demo.Tests.CalcTests" name="Throws_Null" />
    </UnitTest>
    <UnitTest name="Demo.Tests.CalcTests.Brand_New" id="t6">
      <TestMethod codeBase="fixture.dll" className="Demo.Tests.CalcTests" name="Brand_New" />
    </UnitTest>
    <UnitTest name="Demo.Tests.CalcTests.Quote_Message" id="t7">
      <TestMethod codeBase="fixture.dll" className="Demo.Tests.CalcTests" name="Quote_Message" />
    </UnitTest>
  </TestDefinitions>
</TestRun>
XML

    cat > "$FIXTURES/pass.trx" <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<TestRun id="00000000-0000-0000-0000-000000000002" name="fixture" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult testId="t1" testName="Demo.Tests.CalcTests.Add_ReturnsSum" outcome="Passed" />
    <UnitTestResult testId="t2" testName="Demo.Tests.CalcTests.Multiply_ReturnsWrong" outcome="Passed" />
    <UnitTestResult testId="t3" testName="Demo.Tests.CalcTests.Wait_Completes" outcome="Passed" />
    <UnitTestResult testId="t4" testName="Demo.Tests.CalcTests.Mock_IsConfigured" outcome="Passed" />
    <UnitTestResult testId="t5" testName="Demo.Tests.CalcTests.Throws_Null" outcome="Passed" />
    <UnitTestResult testId="t6" testName="Demo.Tests.CalcTests.Brand_New" outcome="Passed" />
    <UnitTestResult testId="t7" testName="Demo.Tests.CalcTests.Quote_Message" outcome="Passed" />
    <UnitTestResult testId="t8" testName="Demo.Tests.CalcTests.Fresh_Passes" outcome="Passed" />
  </Results>
  <TestDefinitions>
    <UnitTest name="Demo.Tests.CalcTests.Add_ReturnsSum" id="t1">
      <TestMethod codeBase="fixture.dll" className="Demo.Tests.CalcTests" name="Add_ReturnsSum" />
    </UnitTest>
    <UnitTest name="Demo.Tests.CalcTests.Multiply_ReturnsWrong" id="t2">
      <TestMethod codeBase="fixture.dll" className="Demo.Tests.CalcTests" name="Multiply_ReturnsWrong" />
    </UnitTest>
    <UnitTest name="Demo.Tests.CalcTests.Wait_Completes" id="t3">
      <TestMethod codeBase="fixture.dll" className="Demo.Tests.CalcTests" name="Wait_Completes" />
    </UnitTest>
    <UnitTest name="Demo.Tests.CalcTests.Mock_IsConfigured" id="t4">
      <TestMethod codeBase="fixture.dll" className="Demo.Tests.CalcTests" name="Mock_IsConfigured" />
    </UnitTest>
    <UnitTest name="Demo.Tests.CalcTests.Throws_Null" id="t5">
      <TestMethod codeBase="fixture.dll" className="Demo.Tests.CalcTests" name="Throws_Null" />
    </UnitTest>
    <UnitTest name="Demo.Tests.CalcTests.Brand_New" id="t6">
      <TestMethod codeBase="fixture.dll" className="Demo.Tests.CalcTests" name="Brand_New" />
    </UnitTest>
    <UnitTest name="Demo.Tests.CalcTests.Quote_Message" id="t7">
      <TestMethod codeBase="fixture.dll" className="Demo.Tests.CalcTests" name="Quote_Message" />
    </UnitTest>
    <UnitTest name="Demo.Tests.CalcTests.Fresh_Passes" id="t8">
      <TestMethod codeBase="fixture.dll" className="Demo.Tests.CalcTests" name="Fresh_Passes" />
    </UnitTest>
  </TestDefinitions>
</TestRun>
XML
}

make_classify_fixture() {
    local output="$1"
    local message="$2"
    local prod_json="$3"
    local tests_json="$4"
    local verified="$5"
    jq -n --arg message "$message" --argjson prod "$prod_json" \
        --argjson test_files "$tests_json" --argjson verified "$verified" '
        "Demo.Tests.CalcTests.Example" as $test_name
        | {
            results: [{
              test_file: "tests/Demo/CalcTests.cs",
              test_name: $test_name,
              outcome: "Failed",
              class_name: "Demo.Tests.CalcTests",
              method_name: "Example",
              message: $message,
              stack: ""
            }],
            registry: {
              files: {},
              tests: (if $verified then {
                ("tests/Demo/CalcTests.cs::" + $test_name): {
                  origin: "llm-generated", verified_baseline: true
                }
              } else {} end)
            },
            changes: {prod: $prod, tests: $test_files}
          }' > "$output"
}

write_classify_fixtures() {
    local assertion="Assert.Equal() Failure: Values differ"
    make_classify_fixture "$FIXTURES/confirmed.json" "$assertion" \
        '["src/Demo/Calc.cs"]' '[]' true
    make_classify_fixture "$FIXTURES/unverified.json" "$assertion" \
        '["src/Demo/Calc.cs"]' '[]' false
    make_classify_fixture "$FIXTURES/environment.json" \
        "System.TimeoutException : The operation timed out" \
        '["src/Demo/Calc.cs"]' '[]' true
    make_classify_fixture "$FIXTURES/canceled.json" \
        "System.Threading.Tasks.TaskCanceledException : A task was canceled" \
        '["src/Demo/Calc.cs"]' '[]' true
    make_classify_fixture "$FIXTURES/test-only.json" "$assertion" \
        '[]' '["tests/Demo/CalcTests.cs"]' true
    make_classify_fixture "$FIXTURES/unverified-test-only.json" "$assertion" \
        '[]' '["tests/Demo/CalcTests.cs"]' false
    make_classify_fixture "$FIXTURES/no-change.json" "$assertion" \
        '[]' '[]' true
    make_classify_fixture "$FIXTURES/behavior.json" \
        "System.NullReferenceException : Object reference not set" \
        '["src/Demo/Calc.cs"]' '[]' true
}

expect_classification() {
    local name="$1"
    local expected="$2"
    local reason="$3"
    local input="$4"
    RC=0
    (cd "$WORK" && ./scripts/tdd-track.sh classify "$input") \
        > "$FIX/classify.json" 2> "$FIX/classify.err" || RC=$?
    expect "$name exits zero" test "$RC" -eq 0
    expect "$name classifies as $expected" \
        test "$(jq -r '.[0].classification' "$FIX/classify.json")" = "$expected"
    expect "$name gives reason $reason" \
        test "$(jq -r '.[0].reason' "$FIX/classify.json")" = "$reason"
}

case_classifier() {
    make_fixture
    write_classify_fixtures
    expect_classification "verified assertion with production change" \
        confirmed_regression production_change_with_behavior_failure "$FIXTURES/confirmed.json"
    expect_classification "unverified assertion with production change" \
        unverified_red no_verified_baseline "$FIXTURES/unverified.json"
    expect_classification "timeout with production change" \
        harness_or_env_error environment_failure "$FIXTURES/environment.json"
    expect_classification "canceled test with production change" \
        harness_or_env_error environment_failure "$FIXTURES/canceled.json"
    expect_classification "verified assertion with test-only change" \
        harness_or_env_error test_only_change "$FIXTURES/test-only.json"
    expect_classification "unverified assertion in a new test file" \
        unverified_red no_verified_baseline "$FIXTURES/unverified-test-only.json"
    expect_classification "verified assertion with no change" \
        harness_or_env_error no_production_change "$FIXTURES/no-change.json"
    expect_classification "thrown behavior exception with production change" \
        confirmed_regression production_change_with_behavior_failure "$FIXTURES/behavior.json"
}

case_analyze_lifecycle() {
    make_fixture
    local ledger="$WORK/.tdd/regression-catches.jsonl"
    printf 'namespace Demo;\npublic sealed class Calc { public int Add(int a, int b) => a - b; }\n' \
        > "$WORK/src/Demo/Calc.cs"

    run_analyze --no-run --trx "$FIXTURES/fail.trx" --origin llm-generated --json
    expect "a failing run exits nonzero" test "$RC" -eq 1
    expect "the run reports ok status" test "$(jq -r '.status' "$FIX/out.json")" = "ok"
    expect "four regressions are confirmed" \
        test "$(jq -r '.classifications.confirmed_regression' "$FIX/out.json")" = "4"
    expect "one test is unverified red" \
        test "$(jq -r '.classifications.unverified_red' "$FIX/out.json")" = "1"
    expect "two failures are harness errors" \
        test "$(jq -r '.classifications.harness_or_env_error' "$FIX/out.json")" = "2"
    expect "the assertion failure is a regression" \
        test "$(classify_field "Demo.Tests.CalcTests.Add_ReturnsSum" classification)" = "confirmed_regression"
    expect "the timeout is a harness error" \
        test "$(classify_field "Demo.Tests.CalcTests.Wait_Completes" classification)" = "harness_or_env_error"
    expect "the mock failure is a harness error" \
        test "$(classify_field "Demo.Tests.CalcTests.Mock_IsConfigured" classification)" = "harness_or_env_error"
    expect "the null reference is a regression" \
        test "$(classify_field "Demo.Tests.CalcTests.Throws_Null" classification)" = "confirmed_regression"
    expect "the new test is unverified" \
        test "$(classify_field "Demo.Tests.CalcTests.Brand_New" classification)" = "unverified_red"
    expect "an XML-escaped assertion is still an assertion" \
        test "$(classify_field "Demo.Tests.CalcTests.Quote_Message" classification)" = "confirmed_regression"
    expect "four caught events are appended" test "$(wc -l < "$ledger" | tr -d ' ')" = "4"

    local registry_hash
    registry_hash="$(shasum -a 256 "$WORK/.tdd/test-registry.json" | awk '{print $1}')"
    run_analyze --no-run --trx "$FIXTURES/fail.trx" --origin llm-generated --json
    expect "a repeat failure still exits nonzero" test "$RC" -eq 1
    expect "a repeat failure does not duplicate the ledger" \
        test "$(wc -l < "$ledger" | tr -d ' ')" = "4"
    expect "a repeat run leaves the registry unchanged" \
        test "$(shasum -a 256 "$WORK/.tdd/test-registry.json" | awk '{print $1}')" = "$registry_hash"
    expect "the repeat failure increments the iteration count" \
        test "$(jq -r '.open_events["tests/Demo/CalcTests.cs::Demo.Tests.CalcTests.Add_ReturnsSum"].iterations' "$WORK/.tdd/state.json")" = "2"

    g checkout -- src/Demo/Calc.cs
    run_analyze --no-run --trx "$FIXTURES/pass.trx" --origin llm-generated --json
    expect "a passing run exits zero" test "$RC" -eq 0
    expect "four resolutions are appended" \
        test "$(jq -s '[.[] | select(.event_type == "resolved")] | length' "$ledger")" = "4"
    expect "the caught event is resolved with its iteration count" \
        test "$(jq -s -r '[.[] | select(.event_type == "resolved")][0].resolution_details.iterations_to_green' "$ledger")" = "2"
    expect "no event stays open" \
        test "$(jq -r '.open_events | length' "$WORK/.tdd/state.json")" = "0"
    expect "a first green test is registered as verified" \
        test "$(jq -r '.tests["tests/Demo/CalcTests.cs::Demo.Tests.CalcTests.Fresh_Passes"].verified_baseline' "$WORK/.tdd/test-registry.json")" = "true"
    expect "a first green commit is recorded" \
        test "$(jq -r '.tests["tests/Demo/CalcTests.cs::Demo.Tests.CalcTests.Fresh_Passes"].first_green_commit' "$WORK/.tdd/test-registry.json")" = "$(g rev-parse HEAD)"

    (cd "$WORK" && ./scripts/tdd-track.sh report --json) \
        > "$FIX/report.json" 2> "$FIX/report.err" || true
    expect "the report counts four catches" test "$(jq -r '.caught_total' "$FIX/report.json")" = "4"
    expect "the report counts four resolutions" test "$(jq -r '.resolved_total' "$FIX/report.json")" = "4"
    expect "the report shows no open events" test "$(jq -r '.open_total' "$FIX/report.json")" = "0"
    expect "the report attributes every catch to the LLM tests" \
        test "$(jq -r '.by_origin["llm-generated"]' "$FIX/report.json")" = "4"
    expect "the hotspot is the production file" \
        test "$(jq -r '.hotspots[0].file' "$FIX/report.json")" = "src/Demo/Calc.cs"

    (cd "$WORK" && ./scripts/tdd-track.sh report --markdown --files "src/Demo/Calc.cs") \
        > "$FIX/report.md" 2> "$FIX/report.err" || true
    expect "the markdown summary names a past regression" \
        test "$(rg -c 'Add_ReturnsSum' "$FIX/report.md" || true)" = "1"
    expect "the markdown summary marks every catch resolved" \
        test "$(rg -c 'resolved' "$FIX/report.md" || true)" = "4"
}

case_markdown_summary() {
    make_fixture
    printf 'namespace Demo;\npublic sealed class Calc { public int Add(int a, int b) => a - b; }\n' \
        > "$WORK/src/Demo/Calc.cs"
    run_analyze --no-run --trx "$FIXTURES/fail.trx" --origin llm-generated --markdown
    expect "a markdown run exits nonzero" test "$RC" -eq 1
    expect "the markdown summary counts confirmed regressions" \
        rg -q 'confirmed_regression' "$FIX/out.json"
    expect "the markdown summary names the regressed test" \
        rg -q 'Add_ReturnsSum' "$FIX/out.json"
    expect "the markdown summary lists the production file" \
        rg -q 'src/Demo/Calc.cs' "$FIX/out.json"
    expect "the markdown summary names the harness failures" \
        rg -q 'harness_or_env_error' "$FIX/out.json"
}

case_register() {
    make_fixture
    printf '// an LLM edited this file\n' >> "$WORK/tests/Demo/CalcTests.cs"

    run_register --changed
    expect "register --changed requires an origin" test "$RC" -ne 0

    run_register --changed --origin llm-generated
    expect "register --changed exits zero" test "$RC" -eq 0
    expect "register --changed records the file origin" \
        test "$(jq -r '.files["tests/Demo/CalcTests.cs"].origin' "$WORK/.tdd/test-registry.json")" = "llm-generated"

    run_register --file tests/Demo/CalcTests.cs --approve
    expect "register --approve exits zero" test "$RC" -eq 0
    expect "register --approve marks every test in the file verified" \
        test "$(jq -r '[.tests | to_entries[] | select(.value.test_file == "tests/Demo/CalcTests.cs") | .value.verified_baseline] | all' "$WORK/.tdd/test-registry.json")" = "true"

    run_register --test "tests/Demo/CalcTests.cs::Demo.Tests.CalcTests.Brand_New" --approve
    expect "a qualified test registration is created" \
        test "$(jq -r '.tests["tests/Demo/CalcTests.cs::Demo.Tests.CalcTests.Brand_New"].verified_baseline' "$WORK/.tdd/test-registry.json")" = "true"
}

run_case() {
    local name="$1"
    shift
    TESTS=$((TESTS + 1))
    CASE_FAILS=0
    "$@"
    if [[ "$CASE_FAILS" -eq 0 ]]; then
        PASSED=$((PASSED + 1))
        ok "$name"
    else
        FAILED=$((FAILED + 1))
        err "$name ($CASE_FAILS failed assertion(s))"
        for log in "$FIX/err.txt" "$FIX/register.err" "$FIX/classify.err" "$FIX/report.err"; do
            if [[ -s "$log" ]]; then
                awk '{print "    | " $0}' "$log" >&2
            fi
        done
    fi
}

write_trx_fixtures
run_case "classifier attributes every failure shape" case_classifier
run_case "analyze catches, deduplicates, resolves, and reports" case_analyze_lifecycle
run_case "analyze prints a CI markdown summary" case_markdown_summary
run_case "register records provenance and approvals" case_register

echo
echo "tests=$TESTS passed=$PASSED failed=$FAILED assertions=$ASSERTS"
[[ "$FAILED" -eq 0 ]]
