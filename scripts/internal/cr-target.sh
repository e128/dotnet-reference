#!/usr/bin/env bash
# Resolve a code-review target (PR, URL, branch, days, commits, path, working tree) into base, files, and reviewers.
# Usage: cr-target.sh [--json] [PR|URL|branch|"days N"|"commits N"|path ...]
source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

usage() {
    cat <<'EOF'
Usage: cr-target.sh [--json] [ARG...]

Resolve one mr-sparkle target argument into a single record:
  {kind, target, base, files[], file_count, empty, agents[], skills[],
   dotnet, ui, shallow, perf, test_quality, no_plan, critical_only, diff_command}

Argument forms, first match wins:
  <number> | <PR URL>   A pull request, resolved through gh
  "days N"              Commits newer than N days
  "commits N"           The last N commits
  <existing path>       Component mode: every file under the path, no git diff
  <branch name>         That branch, diffed against the default branch
  (none)                The working tree, against the merge base with the default branch
                        Falls back to the last commit when that diff is empty

Reviewer routing:
  The three vendored agents and the two plugin skills always run.
  The skill mr-sparkle-dotnet is added when a file is .cs, .csproj, .props,
  .targets, .sln, .slnx, or packages.lock.json.
  The skill interfaces:better-interface is added when a file is .tsx, .jsx,
  .vue, .svelte, .css, or .html.

Flags recorded in the output and never treated as a target:
  --perf  --test-quality  --no-plan  critical only

  --json        Structured JSON output
  --self-test   Build a scratch repo and assert the argument forms
EOF
}

JSON=false
SELF_TEST=false
ARGS=()
while [[ $# -gt 0 ]]; do
    case "$1" in
        --json) JSON=true ;;
        --self-test) SELF_TEST=true ;;
        --help | -h) usage; exit 0 ;;
        *) ARGS+=("$1") ;;
    esac
    shift
done

SELF="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/$(basename -- "${BASH_SOURCE[0]}")"

# Self-check. Build a scratch repo and assert the forms a gate cannot see.
self_test() {
    local scratch fails=0 out
    scratch="$(mktemp -d "$(find_repo_root)/.claude/tmp/cr-target-test.XXXXXX")" || return 1
    (
        cd "$scratch" || exit 1
        git init -q . && git config user.email "test@example.com" && git config user.name "test"
        printf 'a\n' >a.txt
        git add a.txt && git commit -qm first
        printf 'c\n' >c.txt
        git add c.txt && git commit -qm second
        printf 'd\n' >untracked.txt
    ) || { err "self-test: cannot build the scratch repo"; rm -rf "$scratch"; return 1; }

    out="$(cd "$scratch" && "$SELF" --json 2>/dev/null)"
    jq -e '.files | index("untracked.txt")' <<<"$out" >/dev/null || { err "self-test: untracked file missing"; fails=$((fails + 1)); }

    out="$(cd "$scratch" && "$SELF" --json a.txt 2>/dev/null)"
    [[ "$(jq -r '.file_count' <<<"$out")" == "1" ]] || { err "self-test: single-file target wrong"; fails=$((fails + 1)); }

    if (cd "$scratch" && "$SELF" --json days 0 >/dev/null 2>&1); then
        err "self-test: days 0 must fail"; fails=$((fails + 1))
    fi

    (cd "$scratch" && "$SELF" a.txt >/dev/null 2>&1) || { err "self-test: text mode must exit 0"; fails=$((fails + 1)); }

    rm -rf "$scratch"
    if (( fails == 0 )); then
        printf '✓ cr-target self-test passed\n'
        return 0
    fi
    err "✗ cr-target self-test: $fails failure(s)"
    return 1
}

if $SELF_TEST; then
    self_test
    exit $?
fi

# "$ARGUMENTS" can arrive as one quoted word. Split only a leading "days N" or
# "commits N", so a path or a branch that contains a space survives.
WORDS=()
for raw in "${ARGS[@]+"${ARGS[@]}"}"; do
    if [[ "$raw" =~ ^(days|commits)[[:space:]]+[0-9]+$ || "$raw" == "critical only" ]]; then
        read -r -a parts <<<"$raw"
        WORDS+=("${parts[@]}")
    else
        WORDS+=("$raw")
    fi
done

PERF=false
TEST_QUALITY=false
NO_PLAN=false
CRITICAL_ONLY=false
TARGET_WORDS=()
for word in "${WORDS[@]+"${WORDS[@]}"}"; do
    case "$word" in
        --perf) PERF=true ;;
        --test-quality) TEST_QUALITY=true ;;
        --no-plan) NO_PLAN=true ;;
        -*) err "Unknown flag: $word"; exit 1 ;;
        *) TARGET_WORDS+=("$word") ;;
    esac
done

# A standalone flag word filters out at the word level. Dropping it when one
# other word remains keeps a usable target for the match that follows.
ARGS=()
if (( ${#TARGET_WORDS[@]} > 1 )); then
    for word in "${TARGET_WORDS[@]}"; do
        case "$word" in
            critical | only) CRITICAL_ONLY=true ;;
            *) ARGS+=("$word") ;;
        esac
    done
elif (( ${#TARGET_WORDS[@]} == 1 )); then
    ARGS=("${TARGET_WORDS[0]}")
fi

ROOT="$(find_repo_root)"
first="${ARGS[0]:-}"

# Resolve a path argument against the caller's directory before the cd moves it.
PATH_ARG=""
if [[ -n "$first" ]]; then
    if [[ -e "$first" ]]; then
        PATH_ARG="$(cd -- "$(dirname -- "$first")" && pwd)/$(basename -- "$first")"
    elif [[ -e "$ROOT/$first" ]]; then
        PATH_ARG="$ROOT/$first"
    fi
fi

cd "$ROOT" || exit 1

EMPTY_TREE="4b825dc642cb6eb9a060e54bf8d69288fbee4904"

SHALLOW=false
if [[ "$(git rev-parse --is-shallow-repository 2>/dev/null)" == "true" ]]; then
    SHALLOW=true
fi

# Report a path relative to the repo root, so every mode names files the same way.
# Parameter expansion is literal, so a metacharacter in the checkout path stays safe.
rel() {
    local line
    while IFS= read -r line; do
        if [[ "$line" == "$ROOT"/* ]]; then
            printf '%s\n' "${line#"$ROOT"/}"
        else
            printf '%s\n' "$line"
        fi
    done
}

# Default branch: origin/HEAD first, then the usual names, local before remote.
# A stripped origin/HEAD name is kept only when the bare ref resolves.
default_branch() {
    local b
    b="$(git symbolic-ref --quiet --short refs/remotes/origin/HEAD 2>/dev/null || true)"
    b="${b#origin/}"
    if [[ -n "$b" ]]; then
        if git rev-parse --verify -q "$b" >/dev/null 2>&1; then
            printf '%s' "$b"
        else
            printf 'origin/%s' "$b"
        fi
        return 0
    fi
    for candidate in main master; do
        if git rev-parse --verify -q "$candidate" >/dev/null 2>&1; then
            printf '%s' "$candidate"
            return 0
        fi
        if git rev-parse --verify -q "origin/$candidate" >/dev/null 2>&1; then
            printf '%s' "origin/$candidate"
            return 0
        fi
    done
    return 0
}

DEFAULT_BRANCH="$(default_branch)"

KIND="working-tree"
TARGET="working tree"
BASE=""
FILES=""
DIFF_COMMAND=""

resolve_pr() {
    local ref="$1" pr_json base_ref head_ref
    if ! command -v gh >/dev/null 2>&1; then
        err "gh is not installed. Check the PR out as a branch and pass the branch name."
        exit 1
    fi
    if ! pr_json="$(gh pr view "$ref" --json number,baseRefName,headRefName 2>/dev/null)"; then
        err "gh pr view failed for the given reference."
        exit 1
    fi
    local PR_NUMBER
    PR_NUMBER="$(jq -r '.number' <<<"$pr_json")"
    base_ref="$(jq -r '.baseRefName' <<<"$pr_json")"
    head_ref="$(jq -r '.headRefName' <<<"$pr_json")"
    TARGET="PR #$PR_NUMBER ($head_ref -> $base_ref)"
    BASE="origin/$base_ref"
    if ! git rev-parse --verify -q "$BASE" >/dev/null 2>&1; then
        if git rev-parse --verify -q "$base_ref" >/dev/null 2>&1; then
            BASE="$base_ref"
        else
            warn "Neither $BASE nor $base_ref resolves. Fetch the base branch."
        fi
    fi
    # gh pr view --json files stops at 100 paths, so page the REST API instead.
    if ! FILES="$(gh api "repos/{owner}/{repo}/pulls/$PR_NUMBER/files" --paginate -q '.[].filename' 2>/dev/null)"; then
        err "gh api failed to list the files of PR #$PR_NUMBER."
        exit 1
    fi
    FILES="$(printf '%s\n' "$FILES" | sed '/^$/d' | sort -u)"
    DIFF_COMMAND="gh pr diff $PR_NUMBER"
}

if [[ -z "$first" ]]; then
    MERGE_BASE=""
    if [[ -n "$DEFAULT_BRANCH" ]]; then
        MERGE_BASE="$(git merge-base HEAD "$DEFAULT_BRANCH" 2>/dev/null || true)"
    fi
    BASE="${MERGE_BASE:-HEAD}"
    TARGET="working tree against $BASE"
    if ! DIFF="$(git diff --name-only "$BASE" 2>/dev/null)"; then
        err "git diff failed against base $BASE."
        exit 1
    fi
    if ! UNTRACKED="$(git ls-files --others --exclude-standard 2>/dev/null)"; then
        err "git ls-files failed."
        exit 1
    fi
    FILES="$(printf '%s\n%s\n' "$DIFF" "$UNTRACKED" | sed '/^$/d' | sort -u)"
    DIFF_COMMAND="git diff $BASE"
    # The last-commit fallback applies only when a default branch was found and
    # its merge base resolved. Otherwise the narrow scope hides a real problem.
    if [[ -z "$FILES" && -n "$MERGE_BASE" ]] && git rev-parse --verify -q 'HEAD~1' >/dev/null 2>&1; then
        KIND="commit"
        BASE="HEAD~1"
        TARGET="last commit ($BASE)"
        DIFF_COMMAND="git diff $BASE"
        if ! FILES="$(git diff --name-only "$BASE" | sort -u)"; then
            err "git diff failed for the last-commit fallback."
            exit 1
        fi
    fi
elif [[ "$first" =~ ^[0-9]+$ ]]; then
    KIND="pr"
    resolve_pr "$first"
elif [[ "$first" =~ ^https?:// ]]; then
    KIND="pr"
    resolve_pr "$first"
elif [[ "$first" =~ ^days$ && "${ARGS[1]:-}" =~ ^[1-9][0-9]*$ ]]; then
    KIND="days"
    DAYS="${ARGS[1]}"
    TARGET="commits newer than $DAYS days"
    BASE="$(git rev-list -1 --before="$DAYS days ago" HEAD 2>/dev/null || true)"
    BASE="${BASE:-$EMPTY_TREE}"
    DIFF_COMMAND="git diff $BASE"
    if ! FILES="$("$ROOT/scripts/diff.sh" --files --days "$DAYS" 2>/dev/null)"; then
        err "scripts/diff.sh failed for --days $DAYS."
        exit 1
    fi
    if ! UNTRACKED_RANGE="$(git ls-files --others --exclude-standard)"; then
        err "git ls-files failed for the days range."
        exit 1
    fi
    FILES="$(printf '%s\n%s\n' "$FILES" "$UNTRACKED_RANGE" | sed '/^$/d' | sort -u)"
elif [[ "$first" =~ ^commits$ && "${ARGS[1]:-}" =~ ^[1-9][0-9]*$ ]]; then
    KIND="commits"
    COMMITS="${ARGS[1]}"
    TARGET="last $COMMITS commits"
    BASE="$(git rev-parse --verify -q "HEAD~$COMMITS" 2>/dev/null || true)"
    BASE="${BASE:-$EMPTY_TREE}"
    DIFF_COMMAND="git diff $BASE"
    if ! FILES="$("$ROOT/scripts/diff.sh" --files --commits "$COMMITS" 2>/dev/null)"; then
        err "scripts/diff.sh failed for --commits $COMMITS."
        exit 1
    fi
    if ! UNTRACKED_RANGE="$(git ls-files --others --exclude-standard)"; then
        err "git ls-files failed for the commits range."
        exit 1
    fi
    FILES="$(printf '%s\n%s\n' "$FILES" "$UNTRACKED_RANGE" | sed '/^$/d' | sort -u)"
elif [[ "$first" == "days" || "$first" == "commits" ]]; then
    err "$first needs a positive integer, for example: $first 5"
    exit 1
elif [[ -n "$PATH_ARG" ]]; then
    KIND="path"
    TARGET="$first"
    BASE=""
    if [[ -f "$PATH_ARG" ]]; then
        FILES="$(printf '%s\n' "$PATH_ARG" | rel)"
    else
        FILES="$(fd -t f . -- "$PATH_ARG" 2>/dev/null | rel || true)"
    fi
    FILES="$(printf '%s\n' "$FILES" | sed '/^$/d' | sort -u)"
else
    KIND="branch"
    RESOLVED="$first"
    if ! git rev-parse --verify -q "$RESOLVED" >/dev/null 2>&1; then
        if git rev-parse --verify -q "origin/$RESOLVED" >/dev/null 2>&1; then
            RESOLVED="origin/$RESOLVED"
        else
            err "Not a PR number, days N, commits N, existing path, or known branch: $first"
            exit 1
        fi
    fi
    if [[ -z "$DEFAULT_BRANCH" ]]; then
        err "Cannot find the default branch. Pass days N, commits N, or a path instead."
        exit 1
    fi
    if [[ "$RESOLVED" == "$DEFAULT_BRANCH" ]]; then
        TARGET="$first, which is the default branch"
    else
        TARGET="$first against $DEFAULT_BRANCH"
    fi
    BASE="$DEFAULT_BRANCH"
    DIFF_COMMAND="git diff $DEFAULT_BRANCH...$RESOLVED"
    # shortcut: diff.sh has no --base flag, so this one range stays raw git.
    if ! FILES="$(git diff --name-only "$DEFAULT_BRANCH...$RESOLVED" 2>/dev/null)"; then
        err "git diff failed for $DEFAULT_BRANCH...$RESOLVED."
        exit 1
    fi
    FILES="$(printf '%s\n' "$FILES" | sed '/^$/d' | sort -u)"
fi

# The emitted diff has to carry the content of every listed file. git diff shows
# no content for an untracked file, so each listed untracked file gets a
# --no-index diff appended to the command the consumer runs.
if [[ -n "$FILES" ]]; then
    UNTRACKED="$(comm -13 <(git ls-files 2>/dev/null | sort -u) <(printf '%s\n' "$FILES" | sort -u))"
    while IFS= read -r uf; do
        [[ -n "$uf" ]] || continue
        DIFF_COMMAND+="; git diff --no-index -- /dev/null $(printf '%q' "$uf")"
    done <<<"$UNTRACKED"
else
    UNTRACKED=""
fi

# ── Reviewer routing ─────────────────────────────
AGENTS=(security-reviewer code-reviewer adversarial-review)
SKILLS=(code-review ponytail:ponytail-review)
DOTNET=false
UI=false
while IFS= read -r f; do
    [[ -z "$f" ]] && continue
    case "$f" in
        *.cs | *.csproj | *.props | *.targets | *.sln | *.slnx | packages.lock.json) DOTNET=true ;;
        *.tsx | *.jsx | *.vue | *.svelte | *.css | *.html) UI=true ;;
    esac
done <<<"$FILES"
$DOTNET && SKILLS+=(mr-sparkle-dotnet)
$UI && SKILLS+=("interfaces:better-interface")

FILE_COUNT=0
[[ -n "$FILES" ]] && FILE_COUNT="$(printf '%s\n' "$FILES" | wc -l | tr -d ' ')"
EMPTY=false
(( FILE_COUNT == 0 )) && EMPTY=true

if $JSON; then
    FILES_JSON="$(printf '%s\n' "$FILES" | jq -R -s 'split("\n") | map(select(length > 0))')"
    UNTRACKED_JSON="$(printf '%s\n' "$UNTRACKED" | jq -R -s 'split("\n") | map(select(length > 0))')"
    AGENTS_JSON="$(printf '%s\n' "${AGENTS[@]}" | jq -R -s 'split("\n") | map(select(length > 0))')"
    SKILLS_JSON="$(printf '%s\n' "${SKILLS[@]}" | jq -R -s 'split("\n") | map(select(length > 0))')"
    jq -n \
        --arg kind "$KIND" \
        --arg target "$TARGET" \
        --arg base "$BASE" \
        --argjson files "$FILES_JSON" \
        --argjson untracked "$UNTRACKED_JSON" \
        --argjson file_count "$FILE_COUNT" \
        --argjson empty "$EMPTY" \
        --argjson agents "$AGENTS_JSON" \
        --argjson skills "$SKILLS_JSON" \
        --argjson dotnet "$DOTNET" \
        --argjson ui "$UI" \
        --argjson shallow "$SHALLOW" \
        --argjson perf "$PERF" \
        --argjson test_quality "$TEST_QUALITY" \
        --argjson no_plan "$NO_PLAN" \
        --argjson critical_only "$CRITICAL_ONLY" \
        --arg diff_command "$DIFF_COMMAND" \
        '{kind: $kind, target: $target, base: $base, files: $files, untracked: $untracked,
          file_count: $file_count, empty: $empty, agents: $agents, skills: $skills,
          dotnet: $dotnet, ui: $ui, shallow: $shallow, perf: $perf,
          test_quality: $test_quality, no_plan: $no_plan,
          critical_only: $critical_only, diff_command: $diff_command}'
    exit 0
fi

printf "${BOLD}Target:${RESET} %s\n" "$TARGET"
printf "${BOLD}Kind:${RESET} %s\n" "$KIND"
[[ -n "$BASE" ]] && printf "${BOLD}Base:${RESET} %s\n" "$BASE"
printf "${BOLD}Files:${RESET} %d\n" "$FILE_COUNT"
printf "${BOLD}Agents:${RESET} %s\n" "${AGENTS[*]}"
printf "${BOLD}Skills:${RESET} %s\n" "${SKILLS[*]}"
if $EMPTY; then
    warn "Empty target: nothing to review"
fi
if $SHALLOW; then
    warn "Shallow clone: history is incomplete"
fi
[[ -n "$FILES" ]] && printf '%s\n' "$FILES"
exit 0
