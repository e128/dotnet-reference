#!/usr/bin/env nu

# Lode-enabled claude wrapper with an Ollama backend — default model: deepseek-v4.1-flash:cloud
# Usage: dotnet-reference [--model <model>] [--append-system-prompt <text>] [...claude args]

use lode-ollama-lib.nu *

def --wrapped main [...args: string] {
    lode-run "deepseek-v4.1-flash:cloud" ...$args
}
