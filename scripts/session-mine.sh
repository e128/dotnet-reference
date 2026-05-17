#!/usr/bin/env bash
# Mine Claude Code session transcripts for repeated patterns.
# Usage: session-mine.sh <subcommand> [--days N] [--json] [--help]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

JSON=false
DAYS=7
SUBCOMMAND=""
TOP=20

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json)      JSON=true ;;
        --days)      shift; DAYS="${1:?--days requires a number}" ;;
        --top)       shift; TOP="${1:?--top requires a number}" ;;
        --help)
            cat <<'USAGE'
Usage: session-mine.sh <subcommand> [--days N] [--top N] [--json] [--help]

Subcommands:
  tool-freq           Tool call frequency (Read, Bash, Edit, etc.)
  repeated-commands   Most-repeated bash commands
  most-read           Most-read files
  agent-spawns        Agent spawn descriptions and frequencies
  all                 Run all subcommands

Options:
  --days N    Look back N days (default: 7)
  --top N     Show top N results (default: 20)
  --json      Structured JSON output
USAGE
            exit 0
            ;;
        -*)  err "Unknown flag: $1"; exit 1 ;;
        *)   SUBCOMMAND="$1" ;;
    esac
    shift
done

if [[ -z "$SUBCOMMAND" ]]; then
    err "Subcommand required. Use --help for usage."
    exit 1
fi

# ── Session file discovery ──────────────────────
SESSION_DIR="$HOME/.claude/projects/-Users-$(whoami)-repos-$(basename "$(dirname "$(find_repo_root)")")-$(basename "$(find_repo_root)")"
if [[ ! -d "$SESSION_DIR" ]]; then
    SESSION_DIR=$(find "$HOME/.claude/projects" -maxdepth 1 -type d -name "*$(basename "$(find_repo_root)")" 2>/dev/null | head -1)
fi

if [[ -z "$SESSION_DIR" || ! -d "$SESSION_DIR" ]]; then
    err "No session directory found"
    exit 1
fi

CUTOFF=$(date -v-"${DAYS}d" +%s 2>/dev/null || date -d "${DAYS} days ago" +%s)

recent_files=()
for f in "$SESSION_DIR"/*.jsonl; do
    [[ -f "$f" ]] || continue
    mod_time=$(stat -f %m "$f" 2>/dev/null || stat -c %Y "$f")
    if [[ "$mod_time" -ge "$CUTOFF" ]]; then
        recent_files+=("$f")
    fi
done

if [[ ${#recent_files[@]} -eq 0 ]]; then
    if [[ "$JSON" == true ]]; then
        json_object sessions=0 days="$DAYS" error="No sessions found"
    else
        warn "No sessions found in the last $DAYS days"
    fi
    exit 0
fi

cat_sessions() {
    cat "${recent_files[@]}"
}

# ── Subcommand implementations ──────────────────

run_tool_freq() {
    local data
    data=$(cat_sessions | jq -r 'select(.type == "assistant") | .message.content[]? | select(.type == "tool_use") | .name' 2>/dev/null | sort | uniq -c | sort -rn | head -"$TOP")

    if [[ "$JSON" == true ]]; then
        echo "$data" | awk '{print "{\"tool\":\"" $2 "\",\"count\":" $1 "}"}' | jq -s '{tool_frequency: .}'
    else
        printf "${BOLD}Tool Call Frequency (last %d days, %d sessions)${RESET}\n" "$DAYS" "${#recent_files[@]}"
        echo "$data" | while read -r count name; do
            printf "  %4d  %s\n" "$count" "$name"
        done
    fi
}

run_repeated_commands() {
    local data
    data=$(cat_sessions | jq -r 'select(.type == "assistant") | .message.content[]? | select(.type == "tool_use" and .name == "Bash") | .input.command' 2>/dev/null | grep -v '^$' | sort | uniq -c | sort -rn | head -"$TOP")

    if [[ "$JSON" == true ]]; then
        echo "$data" | awk -F'      ' '{gsub(/^ +/, "", $0); split($0, a, / +/); count=a[1]; cmd=""; for(i=2;i<=length(a);i++) cmd=cmd (i>2?" ":"") a[i]; gsub(/"/, "\\\"", cmd); print "{\"count\":" count ",\"command\":\"" cmd "\"}"}' | jq -s '{repeated_commands: .}'
    else
        printf "${BOLD}Repeated Bash Commands (last %d days)${RESET}\n" "$DAYS"
        echo "$data" | while IFS= read -r line; do
            printf "  %s\n" "$line"
        done
    fi
}

run_most_read() {
    local data
    data=$(cat_sessions | jq -r 'select(.type == "assistant") | .message.content[]? | select(.type == "tool_use" and .name == "Read") | .input.file_path' 2>/dev/null | sort | uniq -c | sort -rn | head -"$TOP")

    if [[ "$JSON" == true ]]; then
        echo "$data" | awk '{print "{\"file\":\"" $2 "\",\"reads\":" $1 "}"}' | jq -s '{most_read: .}'
    else
        printf "${BOLD}Most-Read Files (last %d days)${RESET}\n" "$DAYS"
        echo "$data" | while read -r count path; do
            printf "  %4d  %s\n" "$count" "$path"
        done
    fi
}

run_agent_spawns() {
    local data
    data=$(cat_sessions | jq -r 'select(.type == "assistant") | .message.content[]? | select(.type == "tool_use" and .name == "Agent") | .input | "\(.subagent_type // "general") | \(.description)"' 2>/dev/null | sort | uniq -c | sort -rn | head -"$TOP")

    if [[ "$JSON" == true ]]; then
        echo "$data" | awk -F' *[|] *' '{gsub(/^ +/, ""); split($0, a, / +/); count=a[1]; rest=$0; sub(/^ *[0-9]+ +/, "", rest); split(rest, parts, / *[|] */); gsub(/"/, "\\\"", parts[1]); gsub(/"/, "\\\"", parts[2]); print "{\"count\":" a[1] ",\"type\":\"" parts[1] "\",\"description\":\"" parts[2] "\"}"}' | jq -s '{agent_spawns: .}'
    else
        printf "${BOLD}Agent Spawns (last %d days)${RESET}\n" "$DAYS"
        echo "$data" | while IFS= read -r line; do
            printf "  %s\n" "$line"
        done
    fi
}

# ── Dispatch ────────────────────────────────────

case "$SUBCOMMAND" in
    tool-freq)          run_tool_freq ;;
    repeated-commands)  run_repeated_commands ;;
    most-read)          run_most_read ;;
    agent-spawns)       run_agent_spawns ;;
    all)
        if [[ "$JSON" == true ]]; then
            tf=$(run_tool_freq)
            rc=$(run_repeated_commands)
            mr=$(run_most_read)
            as=$(run_agent_spawns)
            jq -n \
                --argjson tf "$tf" \
                --argjson rc "$rc" \
                --argjson mr "$mr" \
                --argjson as "$as" \
                --argjson sessions "${#recent_files[@]}" \
                --argjson days "$DAYS" \
                '{sessions: $sessions, days: $days} + $tf + $rc + $mr + $as'
        else
            printf "${DIM}%d sessions in the last %d days${RESET}\n\n" "${#recent_files[@]}" "$DAYS"
            run_tool_freq
            echo
            run_repeated_commands
            echo
            run_most_read
            echo
            run_agent_spawns
        fi
        ;;
    *)
        err "Unknown subcommand: $SUBCOMMAND"
        err "Valid: tool-freq, repeated-commands, most-read, agent-spawns, all"
        exit 1
        ;;
esac
