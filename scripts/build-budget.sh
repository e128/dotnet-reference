#!/usr/bin/env bash
# Build cycle budget enforcer for token efficiency.
# Usage: build-budget.sh tick [--limit N] | status | reset [--json]
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
BUDGET_FILE="$ROOT/.claude/tmp/build-budget.json"
JSON=false
LIMIT=5

# Parse subcommand first, then flags
SUBCMD="${1:-help}"
shift 2>/dev/null || true

while [[ $# -gt 0 ]]; do
    case "$1" in
        --limit) LIMIT="$2"; shift 2 ;;
        --json)  JSON=true; shift ;;
        -h|--help) SUBCMD="help"; shift ;;
        *) shift ;;
    esac
done

SESSION_ID="${CLAUDE_SESSION_ID:-unknown}"

load_state() {
    if [[ -f "$BUDGET_FILE" ]]; then
        local saved_session
        saved_session="$(jq -r '.session // ""' "$BUDGET_FILE" 2>/dev/null || echo "")"
        if [[ "$saved_session" == "$SESSION_ID" ]]; then
            cat "$BUDGET_FILE"
            return
        fi
    fi
    json_object session="$SESSION_ID" count=0 first_at="" last_at=""
}

save_state() {
    mkdir -p "$(dirname "$BUDGET_FILE")"
    echo "$1" > "$BUDGET_FILE"
}

case "$SUBCMD" in
    tick)
        STATE="$(load_state)"
        COUNT=$(echo "$STATE" | jq -r '.count' 2>/dev/null || echo 0)
        FIRST=$(echo "$STATE" | jq -r '.first_at // ""' 2>/dev/null || echo "")
        NOW="$(iso_timestamp)"
        NEW_COUNT=$((COUNT + 1))
        [[ -z "$FIRST" || "$FIRST" == "null" ]] && FIRST="$NOW"

        HARD_LIMIT=$((LIMIT * 2))
        if [[ $NEW_COUNT -ge $HARD_LIMIT ]]; then
            STATUS="HARD_STOP"
        elif [[ $NEW_COUNT -ge $LIMIT ]]; then
            STATUS="WARNING"
        else
            STATUS="OK"
        fi

        save_state "$(jq -n --arg s "$SESSION_ID" --argjson c "$NEW_COUNT" \
            --arg f "$FIRST" --arg l "$NOW" \
            '{session: $s, count: $c, first_at: $f, last_at: $l}')"

        if $JSON; then
            json_object count="$NEW_COUNT" limit="$LIMIT" hard_limit="$HARD_LIMIT" status="$STATUS"
        else
            case "$STATUS" in
                HARD_STOP) err "BUILD BUDGET EXCEEDED: $NEW_COUNT/$LIMIT builds (hard limit $HARD_LIMIT). Batch fixes." ;;
                WARNING)   warn "Build budget warning: $NEW_COUNT/$LIMIT builds used. Batch fixes." ;;
                *)         info "Build $NEW_COUNT/$LIMIT" ;;
            esac
        fi
        ;;
    status)
        STATE="$(load_state)"
        if $JSON; then
            echo "$STATE"
        else
            COUNT=$(echo "$STATE" | jq -r '.count' 2>/dev/null || echo 0)
            FIRST=$(echo "$STATE" | jq -r '.first_at // ""' 2>/dev/null || echo "")
            LAST=$(echo "$STATE" | jq -r '.last_at // ""' 2>/dev/null || echo "")
            echo "Build count: $COUNT"
            [[ -n "$FIRST" && "$FIRST" != "null" ]] && echo "First build: $FIRST"
            [[ -n "$LAST" && "$LAST" != "null" ]] && echo "Last build:  $LAST"
        fi
        ;;
    reset)
        save_state "$(jq -n --arg s "$SESSION_ID" '{session: $s, count: 0, first_at: "", last_at: ""}')"
        if $JSON; then
            json_object reset=true
        else
            ok "Build budget reset."
        fi
        ;;
    help|*)
        echo "build-budget.sh — Build cycle budget enforcer"
        echo ""
        echo "Subcommands:"
        echo "  tick     Record a build and check budget"
        echo "  status   Show current build count"
        echo "  reset    Reset the counter"
        echo ""
        echo "Flags:"
        echo "  --limit N   Warn threshold (default: 5, hard stop at 2x)"
        echo "  --json      Machine-readable output"
        ;;
esac
