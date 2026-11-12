#!/usr/bin/env bash
# Behavior tests for verify-and-ship.sh in throwaway fixture repos with fake gates.
# Usage: verify-and-ship-test.sh [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

if [[ "${1:-}" == "--help" ]]; then
    echo "Usage: verify-and-ship-test.sh"
    echo "  Run verify-and-ship.sh against temporary git repos with a local bare remote."
    echo "  The fixture replaces format, check, version-bump, and precommit with logging fakes."
    echo "  Nothing touches this repository, its history, or the network."
    exit 0
fi

SCRIPTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRATCH="$SCRIPTS_DIR/../.claude/tmp"
mkdir -p "$SCRATCH"
SANDBOX="$(mktemp -d "$SCRATCH/vas-test.XXXXXX")"
trap 'rm -rf "${SANDBOX:?}"' EXIT

# Isolate git from the caller's configuration and from any enclosing repository.
unset GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE GIT_PREFIX
export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null GIT_TERMINAL_PROMPT=0

FIX="" ORIGIN="" WORK="" LOG="" FAIL_STEP="" RC=0
TESTS=0 PASSED=0 FAILED=0 ASSERTS=0 CASE_FAILS=0

g() { git -C "$WORK" "$@"; }
sha() { g rev-parse "$1"; }
origin_sha() { git -C "$ORIGIN" rev-parse --verify -q "refs/heads/$1" || echo none; }
steps() { awk '{printf "%s%s", sep, $1; sep=" "}' "$LOG"; }
has_text() { rg -qF -- "$2" "$1"; }
lacks_text() { ! rg -qF -- "$2" "$1"; }
commit_file() { printf '%s\n' "$2" > "$WORK/$1"; g add "$1"; g commit -q -m "$3"; }

expect() {
    local desc="$1"
    shift
    ASSERTS=$((ASSERTS + 1))
    if ! "$@"; then
        CASE_FAILS=$((CASE_FAILS + 1))
        err "  FAIL: $desc"
    fi
}

write_fakes() {
    local s="$WORK/scripts"
    cat > "$s/format.sh" <<'EOF'
#!/usr/bin/env bash
echo "format $*" >> "$VAS_LOG"
[[ "${VAS_FAIL:-}" == format ]] && exit 1
exit 0
EOF
    cat > "$s/check.sh" <<'EOF'
#!/usr/bin/env bash
echo "check $* version=$(wc -l < version.txt | tr -d ' ')" >> "$VAS_LOG"
[[ "${VAS_FAIL:-}" == check ]] && exit 1
exit 0
EOF
    cat > "$s/internal/version-bump.sh" <<'EOF'
#!/usr/bin/env bash
echo "bump $*" >> "$VAS_LOG"
[[ "${VAS_FAIL:-}" == bump ]] && exit 1
echo bumped >> version.txt
exit 0
EOF
    cat > "$s/internal/precommit.sh" <<'EOF'
#!/usr/bin/env bash
echo "precommit staged=$(git diff --cached --name-only | sort | tr '\n' ',')" >> "$VAS_LOG"
[[ "${VAS_FAIL:-}" == precommit ]] && exit 1
exit 0
EOF
    chmod +x "$s"/*.sh "$s"/internal/*.sh
}

make_fixture() {
    FIX="$(mktemp -d "$SANDBOX/case.XXXXXX")"
    ORIGIN="$FIX/origin.git"
    WORK="$FIX/work"
    LOG="$FIX/gate.log"
    FAIL_STEP=""
    : > "$LOG"
    git init -q --bare -b main "$ORIGIN"
    git init -q -b main "$WORK"
    g config user.name "Fixture User"
    g config user.email "fixture@example.com"
    g config commit.gpgsign false
    g remote add origin "$ORIGIN"
    mkdir -p "$WORK/scripts/internal"
    cp "$SCRIPTS_DIR/lib.sh" "$SCRIPTS_DIR/verify-and-ship.sh" "$WORK/scripts/"
    cp "$SCRIPTS_DIR/internal/commit.sh" "$SCRIPTS_DIR/internal/stage.sh" "$WORK/scripts/internal/"
    write_fakes
    printf 'one\n' > "$WORK/version.txt"
    printf 'base\n' > "$WORK/README.txt"
    g add -A
    g commit -q -m "base"
    g push -q origin main
    g symbolic-ref refs/remotes/origin/HEAD refs/remotes/origin/main
}

run_ship() {
    RC=0
    (cd "$WORK" && VAS_LOG="$LOG" VAS_FAIL="$FAIL_STEP" ./scripts/verify-and-ship.sh "$@") \
        > "$FIX/out.txt" 2>&1 || RC=$?
}

# A rejected run must stop before any gate, any history change, and any push.
expect_untouched_rejection() {
    local head_before="$1" origin_before="$2" branch="$3" needle="$4"
    expect "exit code is nonzero" test "$RC" -ne 0
    expect "no gate ran" test -z "$(steps)"
    expect "HEAD unchanged" test "$(sha HEAD)" = "$head_before"
    expect "origin branch unchanged" test "$(origin_sha "$branch")" = "$origin_before"
    expect "output explains the rejection" has_text "$FIX/out.txt" "$needle"
}

case_rejects_default_branch() {
    make_fixture
    echo change >> "$WORK/README.txt"
    local head
    head="$(sha HEAD)"
    run_ship -m "feat: x"
    expect_untouched_rejection "$head" "$head" main "default branch"
}

case_rejects_detached_head() {
    make_fixture
    g checkout -q --detach
    echo change >> "$WORK/README.txt"
    local head
    head="$(sha HEAD)"
    run_ship -m "feat: x"
    expect_untouched_rejection "$head" none feature/none "Detached HEAD"
}

case_rejects_missing_base() {
    make_fixture
    g checkout -q -b feature/x
    commit_file a.txt a "a"
    g update-ref -d refs/remotes/origin/main
    local head
    head="$(sha HEAD)"
    run_ship -m "feat: x"
    expect_untouched_rejection "$head" none feature/x "origin/main"
}

case_rejects_diverged_upstream() {
    make_fixture
    g checkout -q -b feature/x
    commit_file a.txt a "a"
    g push -q -u origin feature/x
    g commit -q --amend -m "amended after push"
    local head origin_tip
    head="$(sha HEAD)"
    origin_tip="$(origin_sha feature/x)"
    run_ship -m "feat: x"
    expect_untouched_rejection "$head" "$origin_tip" feature/x "diverged"
}

case_rejects_unresolvable_upstream() {
    make_fixture
    g checkout -q -b feature/x
    commit_file a.txt a "a"
    g config branch.feature/x.remote origin
    g config branch.feature/x.merge refs/heads/feature/x
    local head
    head="$(sha HEAD)"
    run_ship -m "feat: x"
    expect_untouched_rejection "$head" none feature/x "cannot be resolved"
}

# The upstream must be origin and the same branch name, even when origin/<branch> exists.
setup_pushed_branch() {
    make_fixture
    g checkout -q -b feature/x
    commit_file a.txt a "a"
    g push -q -u origin feature/x
    commit_file u1.txt u1 "u1"
}

case_rejects_upstream_to_default_branch() {
    setup_pushed_branch
    g branch -q --set-upstream-to=origin/main
    local head origin_tip
    head="$(sha HEAD)"
    origin_tip="$(origin_sha feature/x)"
    run_ship -m "feat: x"
    expect_untouched_rejection "$head" "$origin_tip" feature/x "must be origin"
}

case_rejects_upstream_on_another_remote() {
    setup_pushed_branch
    git init -q --bare -b main "$FIX/other.git"
    g remote add other "$FIX/other.git"
    g push -q -u other feature/x
    local head origin_tip
    head="$(sha HEAD)"
    origin_tip="$(origin_sha feature/x)"
    run_ship -m "feat: x"
    expect_untouched_rejection "$head" "$origin_tip" feature/x "must be origin"
}

case_rejects_upstream_to_local_branch() {
    setup_pushed_branch
    g branch -q --set-upstream-to=main
    local head origin_tip
    head="$(sha HEAD)"
    origin_tip="$(origin_sha feature/x)"
    run_ship -m "feat: x"
    expect_untouched_rejection "$head" "$origin_tip" feature/x "must be origin"
}

case_rejects_unrecorded_remote_branch() {
    make_fixture
    g checkout -q -b feature/x
    commit_file a.txt a "a"
    g push -q -u origin feature/x
    g branch -q --unset-upstream
    local head
    head="$(sha HEAD)"
    run_ship -m "feat: x"
    expect_untouched_rejection "$head" "$head" feature/x "no upstream"
}

case_requires_message() {
    make_fixture
    g checkout -q -b feature/x
    commit_file a.txt a "a"
    local head
    head="$(sha HEAD)"
    run_ship
    expect_untouched_rejection "$head" none feature/x "message"
}

case_rejects_email_in_message() {
    make_fixture
    g checkout -q -b feature/x
    commit_file a.txt a "a"
    local head
    head="$(sha HEAD)"
    # Build the address at run time so this file carries no address.
    local at="@"
    run_ship -m "feat: ping dev${at}corp.invalid"
    expect_untouched_rejection "$head" none feature/x "email"
}

case_rejects_placeholder_email_in_message() {
    make_fixture
    g checkout -q -b feature/x
    commit_file a.txt a "a"
    local head
    head="$(sha HEAD)"
    run_ship -m "feat: ping user@example.com"
    expect_untouched_rejection "$head" none feature/x "email"
}

case_rejects_obsolete_squash_flag() {
    make_fixture
    g checkout -q -b feature/x
    commit_file a.txt a "a"
    local head
    head="$(sha HEAD)"
    run_ship --squash -m "feat: x"
    expect_untouched_rejection "$head" none feature/x "Unknown flag"
}

case_gate_failure() {
    local step="$1"
    make_fixture
    g checkout -q -b feature/x
    commit_file a.txt a "a"
    echo change >> "$WORK/README.txt"
    local head
    head="$(sha HEAD)"
    FAIL_STEP="$step"
    run_ship -m "feat: x"
    expect "exit code is nonzero" test "$RC" -ne 0
    expect "run stops at the failing step" test "$(steps | awk '{print $NF}')" = "$step"
    expect "nothing pushed" test "$(origin_sha feature/x)" = none
    expect "HEAD unchanged" test "$(sha HEAD)" = "$head"
    if [[ "$step" != precommit ]]; then
        expect "nothing staged before the failure" test -z "$(g diff --cached --name-only)"
    fi
}

case_commit_failure_restores_history() {
    make_fixture
    g checkout -q -b feature/x
    commit_file p1.txt p1 "p1"
    g push -q -u origin feature/x
    commit_file u1.txt u1 "u1"
    echo n > "$WORK/new.txt"
    printf '#!/bin/sh\nexit 1\n' > "$WORK/.git/hooks/pre-commit"
    chmod +x "$WORK/.git/hooks/pre-commit"
    local head origin_tip
    head="$(sha HEAD)"
    origin_tip="$(origin_sha feature/x)"
    run_ship -m "feat: x"
    expect "exit code is nonzero" test "$RC" -ne 0
    expect "nothing pushed" test "$(origin_sha feature/x)" = "$origin_tip"
    expect "HEAD restored after the failed commit" test "$(sha HEAD)" = "$head"
    expect "new and bumped files stay staged" test "$(g diff --cached --name-only | tr '\n' ' ')" = "new.txt version.txt "
}

# The real precommit scan must see content from commits that are not pushed yet.
case_real_precommit_blocks_earlier_unpushed_content() {
    local kind="$1" needle="$2"
    make_fixture
    cp "$SCRIPTS_DIR/internal/precommit.sh" "$WORK/scripts/internal/precommit.sh"
    g checkout -q -b feature/x
    commit_file p1.txt p1 "p1"
    g push -q -u origin feature/x
    # Build the bad value at run time so this file carries no address and no home path.
    local at="@"
    case "$kind" in
        email) commit_file u1.txt "contact dev${at}corp.invalid" "u1" ;;
        *)     commit_file u1.txt "$(printf '/%s/%s/notes' Users alice)" "u1" ;;
    esac
    echo clean >> "$WORK/README.txt"
    local head origin_tip
    head="$(sha HEAD)"
    origin_tip="$(origin_sha feature/x)"
    run_ship -m "feat: x"
    expect "exit code is nonzero" test "$RC" -ne 0
    expect "scan names the finding" has_text "$FIX/out.txt" "$needle"
    expect "nothing pushed" test "$(origin_sha feature/x)" = "$origin_tip"
    expect "HEAD restored after the blocked commit" test "$(sha HEAD)" = "$head"
}

case_push_failure_keeps_commit_and_explains() {
    make_fixture
    g checkout -q -b feature/x
    commit_file p1.txt p1 "p1"
    g push -q -u origin feature/x
    commit_file u1.txt u1 "u1"
    echo n > "$WORK/new.txt"
    printf '#!/bin/sh\nexit 1\n' > "$ORIGIN/hooks/pre-receive"
    chmod +x "$ORIGIN/hooks/pre-receive"
    local head origin_tip
    head="$(sha HEAD)"
    origin_tip="$(origin_sha feature/x)"
    run_ship -m "feat: x"
    expect "exit code is nonzero" test "$RC" -ne 0
    expect "origin unchanged" test "$(origin_sha feature/x)" = "$origin_tip"
    expect "squashed commit is kept" test "$(g log -1 --format=%s)" = "feat: x"
    expect "squashed commit sits on the pushed tip" test "$(sha HEAD^)" = "$origin_tip"
    expect "output prints the original HEAD" has_text "$FIX/out.txt" "$head"
    expect "output says the push result is unknown" has_text "$FIX/out.txt" "may be unknown"
    expect "output gives the restore command" has_text "$FIX/out.txt" "git reset --soft $head"
}

case_ships_one_commit_over_existing_upstream() {
    make_fixture
    g checkout -q -b feature/x
    commit_file p1.txt p1 "p1"
    g push -q -u origin feature/x
    local pushed
    pushed="$(sha HEAD)"
    commit_file u1.txt u1 "u1"
    commit_file u2.txt u2 "u2"
    echo more >> "$WORK/README.txt"
    echo n > "$WORK/new.txt"
    run_ship -m "feat: ship it"
    expect "exit code is zero" test "$RC" -eq 0
    expect "gates run in order, bump before check" test "$(steps)" = "format bump check precommit"
    expect "bump receives the final message" has_text "$LOG" "bump E128.Analyzers --message feat: ship it"
    expect "check runs the full suite" has_text "$LOG" "check --all"
    expect "check keeps the format step" lacks_text "$LOG" "--no-format"
    expect "check sees the bumped version" has_text "$LOG" "version=2"
    expect "precommit sees the bumped file" has_text "$LOG" "version.txt"
    expect "precommit sees the new file" has_text "$LOG" "new.txt"
    expect "precommit sees the first earlier unpushed file" has_text "$LOG" "u1.txt"
    expect "precommit sees the second earlier unpushed file" has_text "$LOG" "u2.txt"
    expect "origin matches HEAD" test "$(origin_sha feature/x)" = "$(sha HEAD)"
    expect "pushed commit is the parent" test "$(sha HEAD^)" = "$pushed"
    expect "one commit above the pushed tip" test "$(g rev-list --count "$pushed..HEAD")" = 1
    expect "push fast-forwards the old tip" git -C "$ORIGIN" merge-base --is-ancestor "$pushed" refs/heads/feature/x
    expect "first unpushed file is kept" g cat-file -e HEAD:u1.txt
    expect "second unpushed file is kept" g cat-file -e HEAD:u2.txt
    expect "new file is kept" g cat-file -e HEAD:new.txt
    expect "commit subject is the message" test "$(g log -1 --format=%s)" = "feat: ship it"
    expect "one name-only trailer" test "$(g log -1 --format=%B | rg -c '^Co-Authored-By: Claude$')" = 1
    expect "working tree is clean" test -z "$(g status --porcelain)"
}

case_ships_one_commit_without_upstream() {
    make_fixture
    g checkout -q -b feature/y
    commit_file a.txt a "a"
    commit_file b.txt b "b"
    echo more >> "$WORK/README.txt"
    local base
    base="$(sha main)"
    run_ship -m "feat: first push"
    expect "exit code is zero" test "$RC" -eq 0
    expect "origin has the branch" test "$(origin_sha feature/y)" = "$(sha HEAD)"
    expect "one commit above main" test "$(g rev-list --count "$base..HEAD")" = 1
    expect "base is the parent" test "$(sha HEAD^)" = "$base"
    expect "upstream is set" test "$(sha '@{u}')" = "$(sha HEAD)"
    expect "both earlier files are kept" g cat-file -e HEAD:b.txt
}

case_no_version_bump_skips_bump() {
    make_fixture
    g checkout -q -b feature/x
    echo change >> "$WORK/README.txt"
    run_ship --no-version-bump -m "docs: x"
    expect "exit code is zero" test "$RC" -eq 0
    expect "bump step is absent" test "$(steps)" = "format check precommit"
    expect "check sees the unbumped version" has_text "$LOG" "version=1"
}

case_json_output() {
    make_fixture
    g checkout -q -b feature/j
    echo change >> "$WORK/README.txt"
    run_ship --json -m "feat: j"
    expect "exit code is zero" test "$RC" -eq 0
    expect "output is one JSON object with the result" \
        jq -e '.status == "ok" and .branch == "feature/j" and .version_bumped == true' "$FIX/out.txt" > /dev/null
}

case_rejects_nothing_to_ship() {
    make_fixture
    g checkout -q -b feature/x
    commit_file p1.txt p1 "p1"
    g push -q -u origin feature/x
    local head
    head="$(sha HEAD)"
    run_ship -m "feat: x"
    expect_untouched_rejection "$head" "$head" feature/x "Nothing to ship"
}

# The real version-bump.sh runs against a project that sits at 1.0.0 on main.
# The only earlier commit is a chore, so the final message alone sets the level.
case_message_sets_version_bump() {
    local message="$1" expected="$2"
    make_fixture
    cp "$SCRIPTS_DIR/internal/version-bump.sh" "$WORK/scripts/internal/version-bump.sh"
    mkdir -p "$WORK/src/E128.Analyzers"
    printf '<Project><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>\n' \
        > "$WORK/src/E128.Analyzers/E128.Analyzers.csproj"
    g add -A
    g commit -q -m "add analyzer project"
    g push -q origin main
    g checkout -q -b feature/x
    commit_file src/E128.Analyzers/Rule.cs "class Rule { }" "chore: touch the rule"
    run_ship -m "$message"
    expect "exit code is zero" test "$RC" -eq 0
    expect "shipped version follows the message" \
        test "$(g show HEAD:src/E128.Analyzers/E128.Analyzers.csproj | rg -o -r '$1' '<Version>([^<]+)')" = "$expected"
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
        if [[ -s "$FIX/out.txt" ]]; then
            awk '{print "    | " $0}' "$FIX/out.txt" >&2
        fi
    fi
}

run_case "rejects the default branch" case_rejects_default_branch
run_case "rejects a detached HEAD" case_rejects_detached_head
run_case "rejects a missing base ref" case_rejects_missing_base
run_case "rejects a diverged upstream" case_rejects_diverged_upstream
run_case "rejects an unresolvable upstream" case_rejects_unresolvable_upstream
run_case "rejects a remote branch with no upstream" case_rejects_unrecorded_remote_branch
run_case "requires a message" case_requires_message
run_case "rejects an upstream on the default branch" case_rejects_upstream_to_default_branch
run_case "rejects an upstream on another remote" case_rejects_upstream_on_another_remote
run_case "rejects an upstream on a local branch" case_rejects_upstream_to_local_branch
run_case "rejects an email in the message" case_rejects_email_in_message
run_case "rejects a placeholder email in the message" case_rejects_placeholder_email_in_message
run_case "rejects the removed squash flag" case_rejects_obsolete_squash_flag
run_case "stops at a format failure" case_gate_failure format
run_case "stops at a version bump failure" case_gate_failure bump
run_case "stops at a check failure" case_gate_failure check
run_case "stops at a precommit failure" case_gate_failure precommit
run_case "restores history when the commit fails" case_commit_failure_restores_history
run_case "real precommit blocks an email from an earlier unpushed commit" \
    case_real_precommit_blocks_earlier_unpushed_content email "email address detected"
run_case "real precommit blocks a home path from an earlier unpushed commit" \
    case_real_precommit_blocks_earlier_unpushed_content homepath "home path detected"
run_case "keeps the commit and explains a rejected push" case_push_failure_keeps_commit_and_explains
run_case "ships one commit over an existing upstream" case_ships_one_commit_over_existing_upstream
run_case "ships one commit without an upstream" case_ships_one_commit_without_upstream
run_case "skips the bump on request" case_no_version_bump_skips_bump
run_case "prints one JSON object" case_json_output
run_case "rejects a branch with nothing to ship" case_rejects_nothing_to_ship
run_case "bumps minor for a feat message" case_message_sets_version_bump "feat: add a rule" 1.1.0
run_case "bumps patch for a fix message" case_message_sets_version_bump "fix: repair a rule" 1.0.1
run_case "bumps major for a breaking marker" case_message_sets_version_bump "feat!: drop a rule" 2.0.0
run_case "bumps major for a breaking footer" \
    case_message_sets_version_bump $'fix: change a rule\n\nBREAKING CHANGE: the id moved' 2.0.0

echo
echo "tests=$TESTS passed=$PASSED failed=$FAILED assertions=$ASSERTS"
[[ "$FAILED" -eq 0 ]]
