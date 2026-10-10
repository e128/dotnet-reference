# Shared implementation for lode-opencode wrapper scripts.
# Usage: use lode-opencode-lib.nu *

use lode-lib.nu [parse-lode-args load-system-prompt]

# Launch opencode with an Ollama backend and the injected SystemPrompt.txt.
# The model comes from the `model` key in opencode.json. This wrapper handles
# --append-system-prompt; all other args pass through.
export def lode-run [...args: string] {
    let parsed = parse-lode-args (load-system-prompt) "" ...$args

    if ($parsed.claude_args | is-empty) {
        # No message: launch the real interactive TUI, primed with the lode
        # prompt as its first turn (opencode has no persistent system-prompt
        # flag; --prompt auto-sends as the opening message, then the session
        # stays open for interactive use).
        ^opencode --prompt $parsed.prompt
    } else {
        let message = $"($parsed.prompt)\n\n($parsed.claude_args | str join ' ')"
        ^opencode run $message
    }
}
