# Shared implementation for lode-ollama wrapper scripts.
# Usage: use lode-ollama-lib.nu *

use lode-lib.nu [parse-lode-args load-system-prompt]

# Launch claude with an Ollama backend and injected SystemPrompt.txt.
# Handles --model and --append-system-prompt flags; all other args pass through.
export def lode-run [
    default_model: string   # Ollama model used unless overridden by --model in args
    ...args: string         # Pass-through args (may include --model and --append-system-prompt)
] {

    let parsed = parse-lode-args (load-system-prompt) $default_model ...$args

    $env.PATH = (
        $env.PATH | prepend "/opt/homebrew/bin" | prepend $"($env.HOME)/.local/bin"
    )

    with-env {
        ANTHROPIC_BASE_URL: "http://127.0.0.1:11434"
        ANTHROPIC_AUTH_TOKEN: "ollama"
        ANTHROPIC_API_KEY: ""
    } {
        ^claude --enable-auto-mode --model $parsed.model --append-system-prompt $parsed.prompt ...$parsed.claude_args
    }
}
