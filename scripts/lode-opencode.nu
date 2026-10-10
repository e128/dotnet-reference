#!/usr/bin/env nu

# Lode-enabled opencode wrapper with an Ollama backend.
# The model comes from the `model` key in opencode.json.
# Usage: lode-opencode [--append-system-prompt <text>] [...opencode args]

use lode-opencode-lib.nu *

def main [...args: string] {
    lode-run ...$args
}
