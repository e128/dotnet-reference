#!/usr/bin/env bash
# Session analytics: error trends, tool counts, bash commands, and topics.
# Usage: session-health.sh [SUBCOMMAND] [OPTIONS]
#   Subcommands: (none)|stats|tool-counts|bash-commands|topics|errors
#   Options: --json, --baseline, --days N, --sessions N, --category
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
BASELINE_FILE="$ROOT/.claude/tmp/error-baseline.json"
PROJECT_DIR="$HOME/.claude/projects"

# ── Temp directory (cleaned on exit) ────────────
TMPDIR_SH="$(mktemp -d)"
trap 'rm -rf "$TMPDIR_SH"' EXIT

# ── Defaults ────────────────────────────────────
SUBCMD=""
JSON=false
BASELINE=false
DAYS=7
SESSIONS=0
CATEGORY=false

# ── Arg parsing ─────────────────────────────────
while [[ $# -gt 0 ]]; do
    case "$1" in
        stats|tool-counts|bash-commands|topics|errors)
            SUBCMD="$1" ;;
        --json)      JSON=true ;;
        --baseline)  BASELINE=true ;;
        --days)      shift; DAYS="${1:?--days requires a value}" ;;
        --sessions)  shift; SESSIONS="${1:?--sessions requires a value}" ;;
        --category)  CATEGORY=true ;;
        *)           err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

# ── Helpers ─────────────────────────────────────

# Find project session dir matching current repo
find_session_dir() {
    # Claude Code stores sessions at ~/.claude/projects/-<path-with-slashes-as-dashes>
    # Slashes and dots become dashes: /path/to/repo -> -path-to-repo
    # Dots in path components become dashes too.
    local repo_path
    repo_path="$(echo "$ROOT" | sed 's|/|-|g; s|\.|-|g')"
    local dir="$PROJECT_DIR/${repo_path}"
    if [[ -d "$dir" ]]; then
        echo "$dir"
        return
    fi
    # Fallback: glob match on the final directory name
    local basename
    basename="$(basename "$ROOT")"
    local match
    match="$(find "$PROJECT_DIR" -maxdepth 1 -type d -name "*${basename}" 2>/dev/null | head -1)"
    if [[ -n "$match" ]]; then
        echo "$match"
    fi
}

# Collect JSONL files within the time window
collect_jsonl_files() {
    local session_dir="$1"
    local cutoff_epoch
    cutoff_epoch=$(date -v"-${DAYS}d" +%s 2>/dev/null || date -d "${DAYS} days ago" +%s 2>/dev/null)

    local files=()
    for f in "$session_dir"/*.jsonl; do
        [[ ! -f "$f" ]] && continue
        local mod_epoch
        mod_epoch=$(stat -f %m "$f" 2>/dev/null || stat -c %Y "$f" 2>/dev/null || echo 0)
        if (( mod_epoch >= cutoff_epoch )); then
            files+=("$f")
        fi
    done

    # If --sessions is set, take only the N most recent
    if (( SESSIONS > 0 )); then
        printf '%s\n' "${files[@]}" | sort -t/ -k"$(echo "${files[0]}" | tr '/' '\n' | wc -l)" -r | head -n "$SESSIONS"
    else
        printf '%s\n' "${files[@]}"
    fi
}

# ── Legacy mode (no subcommand) ─────────────────
run_legacy() {
    local build_errors=0
    local solution
    solution="$(find_solution 2>/dev/null)" || true

    if [[ -n "$solution" ]]; then
        if ! scripts/build.sh > /dev/null 2>&1; then
            build_errors=$(scripts/build.sh 2>&1 | grep -c ' error ' || true)
        fi
    fi

    local format_errors=0
    if ! scripts/format.sh --check > /dev/null 2>&1; then
        format_errors=1
    fi

    local current="{\"build\":$build_errors,\"format\":$format_errors}"

    if [[ "$BASELINE" == true ]]; then
        mkdir -p "$(dirname "$BASELINE_FILE")"
        echo "$current" > "$BASELINE_FILE"
        ok "Baseline saved: build=$build_errors, format=$format_errors"
        exit 0
    fi

    if [[ "$JSON" == true ]]; then
        if [[ -f "$BASELINE_FILE" ]]; then
            printf '{"current":%s,"baseline":%s}\n' "$current" "$(cat "$BASELINE_FILE")"
        else
            printf '{"current":%s,"baseline":null}\n' "$current"
        fi
    else
        printf "${BOLD}Current:${RESET} build=%d format=%d\n" "$build_errors" "$format_errors"
        if [[ -f "$BASELINE_FILE" ]]; then
            local prev_build prev_format
            prev_build=$(jq -r '.build // "?"' "$BASELINE_FILE" 2>/dev/null || echo "?")
            prev_format=$(jq -r '.format // "?"' "$BASELINE_FILE" 2>/dev/null || echo "?")
            printf "${DIM}Baseline: build=%s format=%s${RESET}\n" "$prev_build" "$prev_format"
        else
            dim "No baseline saved — run with --baseline to set one"
        fi
    fi
}

# ── Stats subcommand ────────────────────────────
run_stats() {
    local session_dir
    session_dir="$(find_session_dir)"
    if [[ -z "$session_dir" ]]; then
        if [[ "$JSON" == true ]]; then
            echo '{"error":"no session directory found"}'
        else
            err "No session directory found"
        fi
        exit 1
    fi

    local files
    mapfile -t files < <(collect_jsonl_files "$session_dir")
    local session_count=${#files[@]}
    local total_messages=0
    local total_tool_use=0
    local total_tool_result=0

    for f in "${files[@]}"; do
        [[ -z "$f" || ! -f "$f" ]] && continue
        local msgs tool_uses tool_results
        msgs=$(wc -l < "$f" | tr -d ' ')
        tool_uses=$(grep -c '"tool_use"' "$f" 2>/dev/null || echo 0)
        tool_results=$(grep -c '"tool_result"' "$f" 2>/dev/null || echo 0)
        total_messages=$((total_messages + msgs))
        total_tool_use=$((total_tool_use + tool_uses))
        total_tool_result=$((total_tool_result + tool_results))
    done

    if [[ "$JSON" == true ]]; then
        printf '{"sessions":%d,"days":%d,"total_messages":%d,"tool_uses":%d,"tool_results":%d}\n' \
            "$session_count" "$DAYS" "$total_messages" "$total_tool_use" "$total_tool_result"
    else
        printf "${BOLD}Session stats${RESET} (%d-day window)\n" "$DAYS"
        printf "  Sessions:      %d\n" "$session_count"
        printf "  Messages:      %d\n" "$total_messages"
        printf "  Tool uses:     %d\n" "$total_tool_use"
        printf "  Tool results:  %d\n" "$total_tool_result"
    fi
}

# ── Tool-counts subcommand ──────────────────────
run_tool_counts() {
    local session_dir
    session_dir="$(find_session_dir)"
    if [[ -z "$session_dir" ]]; then
        if [[ "$JSON" == true ]]; then echo '{"error":"no session directory found"}'; fi
        exit 1
    fi

    local files
    mapfile -t files < <(collect_jsonl_files "$session_dir")

    local tmpfile="$TMPDIR_SH/tool-counts"
    : > "$tmpfile"

    for f in "${files[@]}"; do
        [[ -z "$f" || ! -f "$f" ]] && continue
        grep -o '"name":"[^"]*"' "$f" >> "$tmpfile" 2>/dev/null || true
    done

    if [[ "$JSON" == true ]]; then
        printf '{"days":%d,"tools":[' "$DAYS"
        local first=true
        sort "$tmpfile" | uniq -c | sort -rn | while read -r count name_field; do
            local tool_name
            tool_name=$(echo "$name_field" | sed 's/"name":"//;s/"//')
            if [[ "$first" == true ]]; then
                first=false
            else
                printf ','
            fi
            printf '{"name":"%s","count":%d}' "$tool_name" "$count"
        done
        printf ']}\n'
    else
        printf "${BOLD}Tool counts${RESET} (%d-day window)\n" "$DAYS"
        sort "$tmpfile" | uniq -c | sort -rn | head -20 | while read -r count name_field; do
            local tool_name
            tool_name=$(echo "$name_field" | sed 's/"name":"//;s/"//')
            printf "  %4d  %s\n" "$count" "$tool_name"
        done
    fi
}

# ── Bash-commands subcommand ────────────────────
run_bash_commands() {
    local session_dir
    session_dir="$(find_session_dir)"
    if [[ -z "$session_dir" ]]; then
        if [[ "$JSON" == true ]]; then echo '{"error":"no session directory found"}'; fi
        exit 1
    fi

    local files
    mapfile -t files < <(collect_jsonl_files "$session_dir")

    local tmpfile="$TMPDIR_SH/bash-commands"
    : > "$tmpfile"

    for f in "${files[@]}"; do
        [[ -z "$f" || ! -f "$f" ]] && continue
        grep -o '"command":"[^"]*"' "$f" >> "$tmpfile" 2>/dev/null || true
    done

    if [[ "$CATEGORY" == true ]]; then
        # Categorize by first token
        local catfile="$TMPDIR_SH/bash-categories"

        sed 's/"command":"//;s/".*//' "$tmpfile" | while read -r cmd; do
            local first_token="${cmd%% *}"
            # Normalize scripts/ paths
            if [[ "$first_token" == scripts/* ]]; then
                echo "$first_token"
            elif [[ "$first_token" == bash || "$first_token" == '#' ]]; then
                echo "$first_token"
            else
                echo "$first_token"
            fi
        done | sort | uniq -c | sort -rn > "$catfile"

        if [[ "$JSON" == true ]]; then
            printf '{"days":%d,"categories":[' "$DAYS"
            local first=true
            while read -r count category; do
                # Escape backslashes and quotes for valid JSON
                category=$(echo "$category" | sed 's/\\/\\\\/g; s/"/\\"/g')
                if [[ "$first" == true ]]; then
                    first=false
                else
                    printf ','
                fi
                printf '{"command":"%s","count":%d}' "$category" "$count"
            done < "$catfile"
            printf ']}\n'
        else
            printf "${BOLD}Bash command categories${RESET} (%d-day window)\n" "$DAYS"
            head -20 "$catfile" | while read -r count category; do
                printf "  %4d  %s\n" "$count" "$category"
            done
        fi
    else
        # Raw commands, top 20
        if [[ "$JSON" == true ]]; then
            printf '{"days":%d,"commands":[' "$DAYS"
            local first=true
            sort "$tmpfile" | uniq -c | sort -rn | head -20 | while read -r count cmd_field; do
                local cmd
                cmd=$(echo "$cmd_field" | sed 's/"command":"//;s/"$//')
                if [[ "$first" == true ]]; then
                    first=false
                else
                    printf ','
                fi
                # Escape inner quotes for JSON
                cmd=$(echo "$cmd" | sed 's/\\/\\\\/g; s/"/\\"/g')
                printf '{"command":"%s","count":%d}' "$cmd" "$count"
            done
            printf ']}\n'
        else
            printf "${BOLD}Top bash commands${RESET} (%d-day window)\n" "$DAYS"
            sort "$tmpfile" | uniq -c | sort -rn | head -20 | while read -r count cmd_field; do
                local cmd
                cmd=$(echo "$cmd_field" | sed 's/"command":"//;s/"$//')
                printf "  %4d  %s\n" "$count" "$cmd"
            done
        fi
    fi
}

# ── Topics subcommand ───────────────────────────
run_topics() {
    local session_dir
    session_dir="$(find_session_dir)"
    if [[ -z "$session_dir" ]]; then
        if [[ "$JSON" == true ]]; then echo '{"error":"no session directory found"}'; fi
        exit 1
    fi

    local files
    mapfile -t files < <(collect_jsonl_files "$session_dir")

    # Extract skill invocations and agent types as topic proxies
    local tmpfile="$TMPDIR_SH/topics"
    : > "$tmpfile"

    for f in "${files[@]}"; do
        [[ -z "$f" || ! -f "$f" ]] && continue
        # Skills
        grep -o '"skill":"[^"]*"' "$f" 2>/dev/null | sed 's/"skill":"//;s/"$//' | sed 's/^/skill:/' >> "$tmpfile" || true
        # Agent types
        grep -o '"subagent_type":"[^"]*"' "$f" 2>/dev/null | sed 's/"subagent_type":"//;s/"$//' | sed 's/^/agent:/' >> "$tmpfile" || true
    done

    if [[ "$JSON" == true ]]; then
        printf '{"days":%d,"topics":[' "$DAYS"
        local first=true
        sort "$tmpfile" | uniq -c | sort -rn | while read -r count topic; do
            if [[ "$first" == true ]]; then
                first=false
            else
                printf ','
            fi
            printf '{"topic":"%s","count":%d}' "$topic" "$count"
        done
        printf ']}\n'
    else
        printf "${BOLD}Session topics${RESET} (%d-day window)\n" "$DAYS"
        sort "$tmpfile" | uniq -c | sort -rn | while read -r count topic; do
            printf "  %4d  %s\n" "$count" "$topic"
        done
    fi
}

# ── Errors subcommand ───────────────────────────
run_errors() {
    local session_dir
    session_dir="$(find_session_dir)"
    if [[ -z "$session_dir" ]]; then
        if [[ "$JSON" == true ]]; then echo '{"error":"no session directory found"}'; fi
        exit 1
    fi

    local files
    mapfile -t files < <(collect_jsonl_files "$session_dir")

    local tmpfile="$TMPDIR_SH/errors"
    : > "$tmpfile"

    for f in "${files[@]}"; do
        [[ -z "$f" || ! -f "$f" ]] && continue
        # Extract error types from tool_result with is_error
        grep -E '"is_error":\s*true' "$f" 2>/dev/null | while read -r line; do
            # Categorize by error pattern
            if echo "$line" | grep -qi 'EISDIR'; then
                echo "eisdir"
            elif echo "$line" | grep -qi 'write.*before.*read\|must.*read.*before\|have not read'; then
                echo "write-before-read"
            elif echo "$line" | grep -qi 'file.*modified\|content.*changed'; then
                echo "file-modified"
            elif echo "$line" | grep -qi 'not found\|ENOENT\|does not exist'; then
                echo "path-not-found"
            elif echo "$line" | grep -qi 'permission\|EACCES'; then
                echo "permission-denied"
            elif echo "$line" | grep -qi 'timeout\|ETIMEDOUT'; then
                echo "timeout"
            elif echo "$line" | grep -qi 'old_string.*not found\|Could not find'; then
                echo "edit-not-found"
            elif echo "$line" | grep -qi 'too large\|too long\|exceeds'; then
                echo "file-too-large"
            elif echo "$line" | grep -qi 'user.*denied\|user.*rejected'; then
                echo "user-rejected"
            elif echo "$line" | grep -qi 'hook.*denied\|hook.*blocked\|hook.*failed'; then
                echo "hook-denied"
            elif echo "$line" | grep -qi 'HTTP.*error\|status.*[45][0-9][0-9]\|ECONNREFUSED'; then
                echo "http-error"
            elif echo "$line" | grep -qi 'exit code\|exited with\|non-zero'; then
                echo "bash-failure"
            else
                echo "other"
            fi
        done >> "$tmpfile" || true
    done

    local total
    total=$(wc -l < "$tmpfile" | tr -d ' ')

    # Save baseline if requested
    local error_baseline="$ROOT/.claude/tmp/error-categories-baseline.json"
    if [[ "$BASELINE" == true ]]; then
        mkdir -p "$(dirname "$error_baseline")"
        printf '{"total_errors":%d,"date":"%s"}\n' "$total" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > "$error_baseline"
        ok "Error baseline saved: $total errors"
        exit 0
    fi

    # Load baseline for trend comparison
    local prev_total=0
    local has_baseline=false
    if [[ -f "$error_baseline" ]]; then
        has_baseline=true
        prev_total=$(jq -r '.total_errors // 0' "$error_baseline" 2>/dev/null || echo 0)
    fi

    local trend="stable"
    if (( total > prev_total + 2 )); then
        trend="up"
    elif (( total < prev_total - 2 )); then
        trend="down"
    fi

    if [[ "$JSON" == true ]]; then
        printf '{"days":%d,"total_errors":%d,"prev_total":%d,"has_baseline":%s,"total_trend":"%s","categories":[' \
            "$DAYS" "$total" "$prev_total" "$has_baseline" "$trend"
        local first=true
        sort "$tmpfile" | uniq -c | sort -rn | while read -r count category; do
            if [[ "$first" == true ]]; then
                first=false
            else
                printf ','
            fi
            printf '{"name":"%s","count":%d}' "$category" "$count"
        done
        printf ']}\n'
    else
        printf "${BOLD}Error audit${RESET} (%d-day window)\n" "$DAYS"
        printf "  Total: %d errors  [%s]\n\n" "$total" "$trend"
        sort "$tmpfile" | uniq -c | sort -rn | while read -r count category; do
            printf "  %4d  %s\n" "$count" "$category"
        done
        if [[ "$has_baseline" == true ]]; then
            printf "\n${DIM}Baseline: %d errors${RESET}\n" "$prev_total"
        else
            dim "No baseline — run with --baseline to save one"
        fi
    fi
}

# ── Dispatch ────────────────────────────────────
case "$SUBCMD" in
    "")            run_legacy ;;
    stats)         run_stats ;;
    tool-counts)   run_tool_counts ;;
    bash-commands) run_bash_commands ;;
    topics)        run_topics ;;
    errors)        run_errors ;;
esac
