#!/usr/bin/env bash
# Shared argument parsing for lode wrapper scripts.
# Usage: source lode-lib.sh

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

# One owner for the lode file size limit. lode-guard.sh and lode-check.sh
# both read this value.
# shellcheck disable=SC2034  # read by the sourcing script, never here
LODE_FILE_LIMIT=250


# Print prompts/SystemPrompt.txt from the repo root (parent of scripts/).
load_system_prompt() {
    local scripts_dir path
    scripts_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    path="$scripts_dir/../prompts/SystemPrompt.txt"
    if [[ ! -f "$path" ]]; then
        err "SystemPrompt.txt not found at $path"
        return 1
    fi
    cat "$path"
}

# Parse lode wrapper arguments into three globals.
# LODE_PROMPT: base prompt plus every --append-system-prompt value
# LODE_MODEL:  default model unless --model overrides it
# LODE_ARGS:   every other argument, passed through verbatim
# Usage: parse_lode_args BASE_PROMPT DEFAULT_MODEL "$@"
parse_lode_args() {
    local base_prompt="$1" default_model="$2"
    local model="$default_model"
    shift 2

    LODE_PROMPT="$base_prompt"
    LODE_ARGS=()

    while [[ $# -gt 0 ]]; do
        case "$1" in
            --model)
                if [[ $# -lt 2 ]]; then
                    err "--model requires a value"
                    exit 1
                fi
                model="$2"
                shift 2
                ;;
            --model=*)
                model="${1#--model=}"
                shift
                ;;
            --append-system-prompt)
                if [[ $# -lt 2 ]]; then
                    err "--append-system-prompt requires a value"
                    exit 1
                fi
                LODE_PROMPT+=$'\n\n'"$2"
                shift 2
                ;;
            --append-system-prompt=*)
                LODE_PROMPT+=$'\n\n'"${1#--append-system-prompt=}"
                shift
                ;;
            *)
                LODE_ARGS+=("$1")
                shift
                ;;
        esac
    done

    # shellcheck disable=SC2034  # read by the sourcing launcher, never here
    LODE_MODEL="$model"
}
