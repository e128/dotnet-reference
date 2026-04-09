#!/usr/bin/env nu

# Lode-enabled claude wrapper that injects SystemPrompt.txt
# Usage: lode [--append-system-prompt <text>] [...claude args]

use lode-lib.nu [parse-lode-args load-system-prompt]

def main [...args: string] {
    $env.PATH = ($env.PATH | append $"($env.HOME)/.local/bin")
    let parsed = parse-lode-args (load-system-prompt) "" ...$args
    ^claude --enable-auto-mode --append-system-prompt $parsed.prompt ...$parsed.claude_args
}
