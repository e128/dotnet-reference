#!/usr/bin/env bash
# Shared implementation for lode-ollama wrapper scripts.
# Usage: source lode-ollama-lib.sh

source "$(dirname "${BASH_SOURCE[0]}")/lode-lib.sh"

# Launch claude with an Ollama backend and injected SystemPrompt.txt.
# Handles --model and --append-system-prompt; all other args pass through.
# Usage: lode_run DEFAULT_MODEL [ARGS...]
lode_run() {
    local default_model="$1"
    shift

    parse_lode_args "$(load_system_prompt)" "$default_model" "$@"

    export PATH="$HOME/.local/bin:/opt/homebrew/bin:$PATH"
    export ANTHROPIC_BASE_URL="http://127.0.0.1:11434"
    export ANTHROPIC_AUTH_TOKEN="ollama"
    unset ANTHROPIC_API_KEY

    exec claude --enable-auto-mode --model "$LODE_MODEL" \
        --append-system-prompt "$LODE_PROMPT" ${LODE_ARGS[@]+"${LODE_ARGS[@]}"}
}
