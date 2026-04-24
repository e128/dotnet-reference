#!/usr/bin/env nu

# Lode-enabled claude wrapper with Ollama backend — default model: glm-5.1:cloud
# Usage: lode-ollama [--model <model>] [--append-system-prompt <text>] [...claude args]

use lode-ollama-lib.nu *

def main [...args: string] {
    lode-run "glm-5:cloud" ...$args
}
