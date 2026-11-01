#!/usr/bin/env bash
# Lode-enabled opencode wrapper. The model comes from the global opencode config.
# Usage: dotnet-reference-oc.sh [--append-system-prompt TEXT] [MESSAGE...]

source "$(dirname "${BASH_SOURCE[0]}")/lode-lib.sh"

main() {
    parse_lode_args "$(load_system_prompt)" "" "$@"

    if [[ ${#LODE_ARGS[@]} -eq 0 ]]; then
        # No message: launch the real interactive TUI, primed with the lode
        # prompt as its first turn (opencode has no persistent system-prompt
        # flag; --prompt auto-sends as the opening message, then the session
        # stays open for interactive use).
        exec opencode --prompt "$LODE_PROMPT"
    fi

    exec opencode run "$LODE_PROMPT"$'\n\n'"${LODE_ARGS[*]}"
}

main "$@"
