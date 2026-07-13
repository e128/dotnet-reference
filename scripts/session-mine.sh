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
  slash-freq          Slash-command / skill invocation frequency
  redundant-ci        Adjacent redundant CI runs (no edit between them)
  runner-fallback     test.sh failure followed by a raw dotnet test
  all                 Run all subcommands (tool-freq, repeated-commands,
                      most-read, agent-spawns)

Options:
  --days N    Look back N days (default: 7)
  --top N     Show top N results (default: 20; slash-freq only)
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

run_slash_freq() {
    local data
    data=$( {
        # User-typed slash commands: <command-name>/foo</command-name>
        cat_sessions | grep -oE '<command-name>[^<]*</command-name>' 2>/dev/null \
            | sed 's|</\{0,1\}command-name>||g; s|^/||'
        # Skill tool invocations
        cat_sessions | jq -r 'select(.type == "assistant") | .message.content[]? | select(.type == "tool_use" and .name == "Skill") | .input.skill' 2>/dev/null
    } | grep -v '^$' | sort | uniq -c | sort -rn | head -"$TOP")

    if [[ "$JSON" == true ]]; then
        echo "$data" | awk 'NF>=2 {gsub(/"/, "\\\"", $2); print "{\"command\":\"" $2 "\",\"count\":" $1 "}"}' | jq -s '{slash_frequency: .}'
    else
        printf "${BOLD}Slash-Command / Skill Frequency (last %d days)${RESET}\n" "$DAYS"
        echo "$data" | while read -r count name; do
            [[ -z "$name" ]] && continue
            printf "  %4d  %s\n" "$count" "$name"
        done
    fi
}

run_redundant_ci() {
    # Adjacent CI runs (check.sh --all / ci.sh / build.sh) with no Edit/Write
    # tool call between them, detected per session (no cross-session adjacency).
    local pairs
    pairs=$(
        for f in "${recent_files[@]}"; do
            jq -c -s '
                [ .[]
                  | .timestamp as $ts
                  | select(.type == "assistant")
                  | .message.content[]?
                  | select(.type == "tool_use")
                  | {ts: $ts, name: .name, cmd: (.input.command // "")} ]
                | reduce .[] as $e ({last: null, pairs: []};
                    if ($e.name == "Edit" or $e.name == "Write" or $e.name == "NotebookEdit") then
                        .last = null
                    elif ($e.name == "Bash" and ($e.cmd | test("ci\\.sh|build\\.sh|check\\.sh.*--all"))) then
                        (if .last != null then
                            .pairs += [{first_ts: .last.ts, first_cmd: .last.cmd, second_ts: $e.ts, second_cmd: $e.cmd}]
                         else . end) | .last = $e
                    else . end)
                | .pairs[]
            ' "$f" 2>/dev/null
        done | jq -s '.'
    )
    [[ -z "$pairs" ]] && pairs='[]'

    if [[ "$JSON" == true ]]; then
        jq -n --argjson pairs "$pairs" --argjson days "$DAYS" '{days: $days, redundant_ci: $pairs}'
    else
        printf "${BOLD}Redundant CI Runs (last %d days)${RESET}\n" "$DAYS"
        if [[ "$(echo "$pairs" | jq 'length')" -eq 0 ]]; then
            dim "  none"
        else
            echo "$pairs" | jq -r '.[] | "  \(.first_ts) → \(.second_ts)\n    1: \(.first_cmd)\n    2: \(.second_cmd)"'
        fi
    fi
}

run_runner_fallback() {
    # A test.sh invocation whose tool_result is an error, followed by a raw
    # dotnet test invocation (test-runner fallback), detected per session.
    local occ
    occ=$(
        for f in "${recent_files[@]}"; do
            jq -c -s '
                [ .[]
                  | .timestamp as $ts
                  | if .type == "assistant" then
                        (.message.content[]? | select(.type == "tool_use" and .name == "Bash")
                         | {ts: $ts, kind: "bash", id: .id, cmd: (.input.command // "")})
                    elif .type == "user" then
                        (.message.content[]? | select(.type == "tool_result")
                         | {ts: $ts, kind: "result", id: .tool_use_id, err: (.is_error // false)})
                    else empty end ]
                | reduce .[] as $e ({testsh: {}, failts: null, occ: []};
                    if $e.kind == "bash" then
                        if ($e.cmd | test("test\\.sh")) then
                            .testsh[$e.id] = $e.ts
                        elif ($e.cmd | test("dotnet\\s+test")) then
                            (if .failts != null then
                                .occ += [{failed_ts: .failts, fallback_ts: $e.ts, fallback_cmd: $e.cmd}] | .failts = null
                             else . end)
                        else . end
                    elif $e.kind == "result" then
                        (if ($e.err == true and (.testsh[$e.id] != null)) then .failts = .testsh[$e.id] else . end)
                    else . end)
                | .occ[]
            ' "$f" 2>/dev/null
        done | jq -s '.'
    )
    [[ -z "$occ" ]] && occ='[]'

    if [[ "$JSON" == true ]]; then
        jq -n --argjson occ "$occ" --argjson days "$DAYS" '{days: $days, runner_fallback: $occ}'
    else
        printf "${BOLD}Test-Runner Fallbacks (last %d days)${RESET}\n" "$DAYS"
        if [[ "$(echo "$occ" | jq 'length')" -eq 0 ]]; then
            dim "  none"
        else
            echo "$occ" | jq -r '.[] | "  test.sh failed @ \(.failed_ts) → raw runner @ \(.fallback_ts)\n    \(.fallback_cmd)"'
        fi
    fi
}

# ── Dispatch ────────────────────────────────────

case "$SUBCOMMAND" in
    tool-freq)          run_tool_freq ;;
    repeated-commands)  run_repeated_commands ;;
    most-read)          run_most_read ;;
    agent-spawns)       run_agent_spawns ;;
    slash-freq)         run_slash_freq ;;
    redundant-ci)       run_redundant_ci ;;
    runner-fallback)    run_runner_fallback ;;
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
        err "Valid: tool-freq, repeated-commands, most-read, agent-spawns, slash-freq, redundant-ci, runner-fallback, all"
        exit 1
        ;;
esac
