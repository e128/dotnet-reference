#!/usr/bin/env bash
# Lode-enabled Claude wrapper that injects SystemPrompt.txt.
# Usage: lode.sh [--append-system-prompt TEXT] [--model MODEL] [...claude args]
set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || (cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd))"
PROMPT_FILE="$REPO_ROOT/prompts/SystemPrompt.txt"

if [[ ! -f "$PROMPT_FILE" ]]; then
    echo "Error: SystemPrompt.txt not found at $PROMPT_FILE" >&2
    exit 1
fi

BASE_PROMPT="$(cat "$PROMPT_FILE")"
COMBINED_PROMPT="$BASE_PROMPT"
MODEL=""
CLAUDE_ARGS=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --model)
            MODEL="$2"; shift 2 ;;
        --model=*)
            MODEL="${1#--model=}"; shift ;;
        --append-system-prompt)
            COMBINED_PROMPT="${COMBINED_PROMPT}

$2"
            shift 2 ;;
        --append-system-prompt=*)
            COMBINED_PROMPT="${COMBINED_PROMPT}

${1#--append-system-prompt=}"
            shift ;;
        *)
            CLAUDE_ARGS+=("$1"); shift ;;
    esac
done

EXEC_ARGS=(claude --enable-auto-mode --append-system-prompt "$COMBINED_PROMPT")

if [[ -n "$MODEL" ]]; then
    EXEC_ARGS+=(--model "$MODEL")
fi

exec "${EXEC_ARGS[@]}" ${CLAUDE_ARGS[@]+"${CLAUDE_ARGS[@]}"}
