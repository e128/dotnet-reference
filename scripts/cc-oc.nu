#!/usr/bin/env nu

# Launch Claude Code against the opencode Go subscription endpoint.
# The API key is read from the macOS Keychain.
# Usage: cc-oc [--model <model>] [--append-system-prompt <text>] [...claude args]

use lode-lib.nu [parse-lode-args load-system-prompt]

const GO_BASE_URL = "https://opencode.ai/inference/go/anthropic"
const DEFAULT_MODEL = "deepseek-v4.1-flash"
const DEFAULT_KEYCHAIN_SERVICE = "OPENCODE_API_KEY"

# Read the oc_sk_ key from the Keychain. Override the service name with
# OPENCODE_KEYCHAIN_SERVICE.
def read-keychain-token []: nothing -> string {
    let service = ($env.OPENCODE_KEYCHAIN_SERVICE? | default $DEFAULT_KEYCHAIN_SERVICE)
    let result = (^security find-generic-password -s $service -w | complete)
    if $result.exit_code != 0 {
        print $"Error: no Keychain item for service '($service)'."
        print $"Add one with: security add-generic-password -s ($service) -a opencode -w oc_sk_..."
        exit 1
    }
    let token = ($result.stdout | str trim)
    if ($token | is-empty) {
        print $"Error: Keychain item '($service)' is empty."
        exit 1
    }
    $token
}

def main [...args: string] {
    let parsed = parse-lode-args (load-system-prompt) $DEFAULT_MODEL ...$args
    let token = (read-keychain-token)

    $env.PATH = ($env.PATH | prepend "/opt/homebrew/bin" | prepend $"($env.HOME)/.local/bin")

    # The endpoint translates non-Anthropic models, so every model alias points
    # at the selected Go model.
    with-env {
        ANTHROPIC_BASE_URL: $GO_BASE_URL
        ANTHROPIC_AUTH_TOKEN: $token
        ANTHROPIC_API_KEY: ""
        ANTHROPIC_DEFAULT_OPUS_MODEL: $parsed.model
        ANTHROPIC_DEFAULT_SONNET_MODEL: $parsed.model
        ANTHROPIC_DEFAULT_HAIKU_MODEL: $parsed.model
    } {
        ^claude --enable-auto-mode --model $parsed.model --append-system-prompt $parsed.prompt ...$parsed.claude_args
    }
}
