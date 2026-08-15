#!/usr/bin/env nu

# Lode-enabled opencode wrapper with Ollama backend — default model: qwen3.8:27b-mlx
# Usage: lode-opencode [--model <model>] [--append-system-prompt <text>] [...opencode args]

use lode-opencode-lib.nu *

def main [...args: string] {
    lode-run "qwen3.8:27b-mlx" ...$args
}
