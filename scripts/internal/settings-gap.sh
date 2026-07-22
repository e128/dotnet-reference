#!/usr/bin/env bash
# Settings.json allow-list gap analysis.
# Extracts every shell command referenced in fenced ```bash blocks across
# .claude/agents/ and .claude/skills/, diffs them against the permissions.allow
# globs in .claude/settings.json, and classifies each uncovered command by a
# fixed safety table. Pure set-difference + lookup — no LLM judgment.
#
# Usage: settings-gap.sh [--json] [--help]
#
# Replaces error-audit Step 5b/5c (manual catalog scan + hand-classification).
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

JSON=false
for arg in "$@"; do
    case "$arg" in
        --json) JSON=true ;;
        -h|--help)
            cat <<'EOF'
settings-gap.sh — settings.json allow-list gap analysis

Scans .claude/agents/ and .claude/skills/ for commands used in fenced bash
blocks, diffs against permissions.allow in .claude/settings.json, and classifies
each uncovered command by safety tier.

Options:
  --json    Emit JSON { gaps: [ { pattern, used_in, files, safety, recommendation } ] }
  --help    Show this help

Safety tiers:
  safe      grep/find/cat/head/tail/ls/wc/git diff/git log ...   Auto-add
  low       scripts/*.sh, fd, rg, jq, bash -n, shellcheck         Propose add
  review    dotnet test, git add, git commit, curl, gh, podman    Present
  manual    rm, rmdir, git reset, git push, git clean, mv         Keep manual
EOF
            exit 0 ;;
        *) err "Unknown flag: $arg"; exit 1 ;;
    esac
done

ROOT="$(find_repo_root)"
SETTINGS="$ROOT/.claude/settings.json"
AGENTS_DIR="$ROOT/.claude/agents"
SKILLS_DIR="$ROOT/.claude/skills"

[[ -f "$SETTINGS" ]] || { err "No settings.json at $SETTINGS"; exit 1; }

# ── Parse allow-list Bash(...) globs ─────────────
# Bash(git *) -> "git *"   Bash(scripts/*.sh*) -> "scripts/*.sh*"
mapfile -t ALLOW_GLOBS < <(jq -r '.permissions.allow[]? | select(startswith("Bash(")) | sub("^Bash\\(";"") | sub("\\)$";"")' "$SETTINGS")

# True if command line $1 is covered by any allow glob.
is_covered() {
    local cmd="$1" g
    for g in "${ALLOW_GLOBS[@]}"; do
        # shellcheck disable=SC2053  # intentional glob match
        [[ "$cmd" == $g ]] && return 0
    done
    return 1
}

# Shell keywords / builtins that are not permission-relevant invocations.
SKIP_WORDS=" if then else elif fi for while until do done case esac function return \
exit shift local readonly declare typeset export unset set eval trap continue break \
true false echo printf read cd pwd source test command type alias mapfile wait let \
time select in "

# ── Derive a dedup/display key from a command line ─
# Returns a validated command key, or empty if the line is not a real invocation.
# git/dotnet -> two tokens; scripts paths -> the script path; else first token.
derive_key() {
    local cmd="$1" t1 t2
    cmd="${cmd#bash }"; cmd="${cmd#./}"
    read -r t1 t2 _ <<< "$cmd"
    # First token must be a plausible command name (no shell metacharacters).
    [[ "$t1" =~ ^(scripts/[A-Za-z0-9._/-]+\.(sh|nu)|[a-z][a-z0-9._-]*)$ ]] || return 0
    # Reject shell keywords/builtins.
    [[ "$SKIP_WORDS" == *" $t1 "* ]] && return 0
    case "$t1" in
        git|dotnet)
            [[ "$t2" =~ ^[a-z][a-z-]*$ ]] && echo "$t1 $t2" || echo "$t1" ;;
        *) echo "$t1" ;;
    esac
}

# ── Classify a key by the fixed safety table ─────
classify() {
    case "$1" in
        grep|find|cat|head|tail|ls|wc|sort|uniq|tr|echo|date|seq|awk|sed|printf|comm|paste|cut|"git diff"|"git log"|"git status")
            echo "safe|Auto-add" ;;
        scripts/*|fd|rg|jq|"bash -n"|shellcheck|mkdir|cp)
            echo "low|Propose add" ;;
        "dotnet test"|"dotnet build"|"git add"|"git commit"|dotnet|curl|gh|podman|chmod|xargs)
            echo "review|Present" ;;
        rm|rmdir|mv|"git reset"|"git push"|"git clean"|"git checkout"|"git rebase")
            echo "manual|Keep manual" ;;
        *) echo "review|Present" ;;
    esac
}

# ── Extract commands from fenced bash blocks ─────
declare -A KEY_FILES   # key -> space-separated set of basenames

scan_file() {
    local file="$1" base="$2" in_block=false line cmd key
    while IFS= read -r line; do
        if [[ "$line" =~ ^[[:space:]]*\`\`\`bash ]]; then in_block=true; continue; fi
        if [[ "$in_block" == true && "$line" =~ ^[[:space:]]*\`\`\` ]]; then in_block=false; continue; fi
        [[ "$in_block" == true ]] || continue

        # Trim leading whitespace; skip blanks and comments.
        line="${line#"${line%%[![:space:]]*}"}"
        [[ -z "$line" || "$line" == \#* ]] && continue
        # Skip placeholder/heredoc/control lines and pure assignments.
        [[ "$line" == \{* || "$line" == \(* || "$line" == EOF* ]] && continue
        [[ "$line" =~ ^[A-Za-z_][A-Za-z0-9_]*= ]] && continue

        # No pipe-splitting: derive the leading command, glob-match the whole
        # line (so `rg "a|b" lode/` matches `rg *`), classify by command head.
        key="$(derive_key "$line")"
        [[ -z "$key" ]] && continue
        cmd="${line#bash }"; cmd="${cmd#./}"
        is_covered "$cmd" && continue
        printf '%s\t%s\n' "$key" "$base"
    done < "$file"
}

# Collect gap rows from all catalog files (subshell-safe via temp aggregation).
shopt -s nullglob
{
    for f in "$AGENTS_DIR"/*.md; do scan_file "$f" "$(basename "$f")"; done
    for d in "$SKILLS_DIR"/*/SKILL.md; do scan_file "$d" "$(basename "$(dirname "$d")")"; done
} | sort -u > "$ROOT/.claude/tmp/.settings-gap.$$"

while IFS=$'\t' read -r key base; do
    [[ -z "$key" ]] && continue
    if [[ -z "${KEY_FILES[$key]:-}" ]]; then
        KEY_FILES[$key]="$base"
    elif [[ " ${KEY_FILES[$key]} " != *" $base "* ]]; then
        KEY_FILES[$key]="${KEY_FILES[$key]} $base"
    fi
done < "$ROOT/.claude/tmp/.settings-gap.$$"
rm -f "$ROOT/.claude/tmp/.settings-gap.$$"

# ── Emit ─────────────────────────────────────────
# Locally relax nounset: an empty associative array reads as "unbound" under set -u.
GAP_KEYS=()
set +u
mapfile -t GAP_KEYS < <(printf '%s\n' "${!KEY_FILES[@]}" | sort | sed '/^$/d')
set -u

if [[ "$JSON" == true ]]; then
    printf '{"gaps":['
    first=true
    for key in "${GAP_KEYS[@]}"; do
        IFS='|' read -r safety rec <<< "$(classify "$key")"
        files="${KEY_FILES[$key]}"
        count=$(echo "$files" | wc -w | tr -d ' ')
        files_json=$(echo "$files" | tr ' ' '\n' | jq -R . | jq -cs .)
        [[ "$first" == true ]] && first=false || printf ','
        printf '{"pattern":"%s","used_in":%s,"files":%s,"safety":"%s","recommendation":"%s"}' \
            "${key//\"/\\\"}" "$count" "$files_json" "$safety" "$rec"
    done
    printf ']}\n'
else
    if [[ ${#GAP_KEYS[@]} -eq 0 ]]; then
        ok "No allow-list gaps — every referenced command is covered."
        exit 0
    fi
    printf "${BOLD}Settings.json Allow-List Gaps${RESET}\n\n"
    printf "%-28s %5s  %-8s %-12s %s\n" "Command Pattern" "Used" "Safety" "Action" "Files"
    printf "%-28s %5s  %-8s %-12s %s\n" "----------------------------" "-----" "--------" "------------" "-----"
    for key in "${GAP_KEYS[@]}"; do
        IFS='|' read -r safety rec <<< "$(classify "$key")"
        files="${KEY_FILES[$key]}"
        count=$(echo "$files" | wc -w | tr -d ' ')
        printf "%-28s %5s  %-8s %-12s %s\n" "$key" "$count" "$safety" "$rec" "$files"
    done
fi
