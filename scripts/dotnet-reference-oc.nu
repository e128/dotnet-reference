#!/usr/bin/env nu

# Lode-enabled opencode wrapper. Composes prompts/SystemPrompt.txt into the first
# turn, because opencode takes no persistent system-prompt flag.
# Usage: dotnet-reference-oc [--append-system-prompt <text>] [...message]

use lode-lib.nu [parse-lode-args load-system-prompt]

const MODEL = "opencode-go/deepseek-v4.1-flash"

def --wrapped main [...args: string] {
    let parsed = parse-lode-args (load-system-prompt) $MODEL ...$args

    if ($parsed.claude_args | is-empty) {
        # No message: launch the real interactive TUI, primed with the lode
        # prompt as its opening turn. The session stays open after that turn.
        ^opencode --prompt $parsed.prompt
    } else {
        let message = $"($parsed.prompt)\n\n($parsed.claude_args | str join ' ')"
        ^opencode run --model $MODEL $message
    }
}
