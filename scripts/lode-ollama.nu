#!/usr/bin/env nu

# Lode-enabled claude wrapper with Ollama backend — default model: deepseek-v4-flash
# Usage: lode-ollama [--model <model>] [--append-system-prompt <text>] [...claude args]

use lode-ollama-lib.nu *

def main [...args: string] {
    lode-run "glm-5.3-flash:cloud" ...$args
}
