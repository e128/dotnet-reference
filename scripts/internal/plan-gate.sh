#!/usr/bin/env bash
# Phase gate verification: check if a phase's prerequisites are met.
# Usage: plan-gate.sh --plan NAME [--json]
source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

ROOT="$(find_repo_root)"
PLAN=""; JSON=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --plan) PLAN="$2"; shift ;;
        --json) JSON=true ;;
        *)      err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

if [[ -z "$PLAN" ]]; then
    err "Usage: plan-gate.sh --plan NAME"
    exit 1
fi

TASKS_FILE="$ROOT/plans/$PLAN/$PLAN-tasks.md"
CONTEXT_FILE="$ROOT/plans/$PLAN/$PLAN-context.md"

if [[ ! -f "$TASKS_FILE" ]]; then
    err "Plan not found: $PLAN"
    exit 1
fi

# Check if acceptance criteria exist and have stubs
if [[ -f "$CONTEXT_FILE" ]] && grep -q '## Acceptance Criteria' "$CONTEXT_FILE"; then
    # Check if acceptance contract section has checked tasks
    if grep -q '## Acceptance Contract' "$TASKS_FILE"; then
        UNCHECKED=$(grep -A 50 '## Acceptance Contract' "$TASKS_FILE" | grep -c '^\- \[ \]' || true)
        if [[ $UNCHECKED -gt 0 ]]; then
            if [[ "$JSON" == true ]]; then
                json_object gate=fail reason="Acceptance Contract incomplete" missing="$UNCHECKED"
            else
                err "Acceptance Contract incomplete: $UNCHECKED tasks remaining"
            fi
            exit 1
        fi
        [[ "$JSON" == true ]] && json_object gate=pass || ok "Acceptance gate passed"
    else
        [[ "$JSON" == true ]] && json_object gate=skip reason="no acceptance contract section" || dim "No acceptance contract — skipping gate"
    fi
else
    [[ "$JSON" == true ]] && json_object gate=skip reason="no acceptance criteria section" || dim "No acceptance criteria — skipping gate"
fi
