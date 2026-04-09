#!/usr/bin/env bash
# Task management: check/next/progress on tasks.md files.
# Usage: task.sh check "substring" | next | progress [--plan NAME] [--phase "Phase N"]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

SCRIPTS="$(dirname "${BASH_SOURCE[0]}")"
ROOT="$(find_repo_root)"
PLAN=""; PHASE=""; JSON=false
ACTION="${1:-}"; shift 2>/dev/null || true

while [[ $# -gt 0 ]]; do
    case "$1" in
        --plan)  PLAN="$2"; shift ;;
        --phase) PHASE="$2"; shift ;;
        --json)  JSON=true ;;
        -*)      err "Unknown flag: $1"; exit 1 ;;
        *)       [[ "$ACTION" == "check" && -z "${SUBSTR:-}" ]] && SUBSTR="$1" ;;
    esac
    shift
done

# Discover plan
if [[ -z "$PLAN" ]]; then
    # Find the first active plan
    PLAN_DIR=$(fd -t d --max-depth 1 . "$ROOT/plans" 2>/dev/null | head -1)
    [[ -n "$PLAN_DIR" ]] && PLAN="$(basename "$PLAN_DIR")"
fi

if [[ -z "$PLAN" ]]; then
    err "No plan found. Use --plan NAME"
    exit 1
fi

TASKS_FILE="$ROOT/plans/$PLAN/$PLAN-tasks.md"
if [[ ! -f "$TASKS_FILE" ]]; then
    err "Tasks file not found: $TASKS_FILE"
    exit 1
fi

case "$ACTION" in
    check)
        if [[ -z "${SUBSTR:-}" ]]; then
            err "Usage: task.sh check \"substring\" [--phase \"Phase N\"]"
            exit 1
        fi
        # Find the line and check it off
        if grep -q "^\- \[ \].*${SUBSTR}" "$TASKS_FILE"; then
            sed -i '' "s/^\(- \)\[ \]\(.*${SUBSTR}\)/\1[x]\2/" "$TASKS_FILE"
            ok "Checked: $SUBSTR"
        else
            warn "No unchecked task matching: $SUBSTR"
        fi
        ;;
    next)
        # Find first unchecked task
        LINE=$(grep -n '^\- \[ \]' "$TASKS_FILE" | head -1)
        if [[ -z "$LINE" ]]; then
            ok "All tasks complete"
            exit 0
        fi
        LINE_NUM="${LINE%%:*}"
        TASK="${LINE#*\] }"
        if [[ "$JSON" == true ]]; then
            # Find the phase heading above this line
            PHASE_LINE=$(head -n "$LINE_NUM" "$TASKS_FILE" | grep -n '^## Phase' | tail -1)
            PHASE_NAME="${PHASE_LINE#*## }"
            json_object plan="$PLAN" phase="$PHASE_NAME" task="$TASK" line="$LINE_NUM"
        else
            echo "$TASK"
        fi
        ;;
    progress)
        # Count tasks per phase
        CURRENT_PHASE=""
        declare -A PHASE_TOTAL PHASE_DONE
        while IFS= read -r line; do
            if [[ "$line" =~ ^##\  ]]; then
                CURRENT_PHASE="${line#\#\# }"
                PHASE_TOTAL["$CURRENT_PHASE"]=0
                PHASE_DONE["$CURRENT_PHASE"]=0
            elif [[ "$line" =~ ^\-\ \[x\] ]]; then
                PHASE_TOTAL["$CURRENT_PHASE"]=$(( ${PHASE_TOTAL["$CURRENT_PHASE"]} + 1 ))
                PHASE_DONE["$CURRENT_PHASE"]=$(( ${PHASE_DONE["$CURRENT_PHASE"]} + 1 ))
            elif [[ "$line" =~ ^\-\ \[\ \] ]]; then
                PHASE_TOTAL["$CURRENT_PHASE"]=$(( ${PHASE_TOTAL["$CURRENT_PHASE"]} + 1 ))
            fi
        done < "$TASKS_FILE"

        GRAND_TOTAL=0; GRAND_DONE=0
        for phase in "${!PHASE_TOTAL[@]}"; do
            GRAND_TOTAL=$(( GRAND_TOTAL + ${PHASE_TOTAL["$phase"]} ))
            GRAND_DONE=$(( GRAND_DONE + ${PHASE_DONE["$phase"]} ))
        done

        printf "${BOLD}%s: %d/%d tasks${RESET}\n" "$PLAN" "$GRAND_DONE" "$GRAND_TOTAL"
        # Sort phases by their order in the file
        while IFS= read -r line; do
            if [[ "$line" =~ ^##\  ]]; then
                phase="${line#\#\# }"
                total="${PHASE_TOTAL["$phase"]:-0}"
                done_count="${PHASE_DONE["$phase"]:-0}"
                if [[ $total -eq 0 ]]; then
                    printf "    ${DIM}  —  %s${RESET}\n" "$phase"
                elif [[ $done_count -eq $total ]]; then
                    printf "    ${GREEN}done${RESET}  %s\n" "$phase"
                else
                    printf "  %d/%d  %s\n" "$done_count" "$total" "$phase"
                fi
            fi
        done < "$TASKS_FILE"
        ;;
    *)
        err "Usage: task.sh {check|next|progress} [--plan NAME]"
        exit 1
        ;;
esac
