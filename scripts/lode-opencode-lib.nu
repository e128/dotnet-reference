# Shared implementation for lode-opencode wrapper scripts.
# Usage: use lode-opencode-lib.nu *

use lode-lib.nu [parse-lode-args load-system-prompt]

# Launch opencode with an Ollama backend and injected SystemPrompt.txt.
# Handles --model and --append-system-prompt flags; all other args pass through.
export def lode-run [
    default_model: string   # Ollama model used unless overridden by --model in args
    ...args: string         # Pass-through args (may include --model and --append-system-prompt)
] {

    let parsed = parse-lode-args (load-system-prompt) $default_model ...$args

    if ($parsed.claude_args | is-empty) {
        # No message: launch the real interactive TUI, primed with the lode
        # prompt as its first turn (opencode has no persistent system-prompt
        # flag -- --prompt auto-sends as the opening message, then the
        # session stays open for interactive use).
        ^opencode --model $"ollama/($parsed.model)" --prompt $parsed.prompt
    } else {
        let message = $"($parsed.prompt)\n\n($parsed.claude_args | str join ' ')"
        ^opencode run --model $"ollama/($parsed.model)" $message
    }
}
