#!/usr/bin/env bash
# Lode-enabled claude wrapper against the opencode Go endpoint. Reads the API key from the macOS Keychain.
# Usage: dotnet-reference-go.sh [--model MODEL] [--append-system-prompt TEXT] [CLAUDE_ARGS...]

source "$(dirname "${BASH_SOURCE[0]}")/lode-lib.sh"

# The endpoint and the fallback model are overridable, so no value lives in
# source without an escape hatch.
readonly GO_BASE_URL="${OPENCODE_GO_BASE_URL:-https://opencode.ai/inference/go/anthropic}"
readonly DEFAULT_MODEL="${OPENCODE_GO_MODEL:-glm-5.3-flash}"
readonly DEFAULT_KEYCHAIN_SERVICE="OPENCODE_API_KEY"

# Print the oc_sk_ key from the Keychain. Override the service name with
# OPENCODE_KEYCHAIN_SERVICE.
read_keychain_token() {
    local service="${OPENCODE_KEYCHAIN_SERVICE:-$DEFAULT_KEYCHAIN_SERVICE}"
    local token
    if ! token="$(security find-generic-password -s "$service" -w 2>/dev/null)"; then
        err "no Keychain item for service '$service'"
        err "add one with: security add-generic-password -s $service -a opencode -w oc_sk_..."
        exit 1
    fi
    token="$(printf '%s' "$token" | tr -d '[:space:]')"
    if [[ -z "$token" ]]; then
        err "Keychain item '$service' is empty"
        exit 1
    fi
    printf '%s' "$token"
}

main() {
    local token
    parse_lode_args "$(load_system_prompt)" "$DEFAULT_MODEL" "$@"
    token="$(read_keychain_token)"

    export PATH="$HOME/.local/bin:/opt/homebrew/bin:$PATH"

    # The endpoint translates non-Anthropic models, so every model alias points
    # at the selected Go model.
    export ANTHROPIC_BASE_URL="$GO_BASE_URL"
    export ANTHROPIC_AUTH_TOKEN="$token"
    export ANTHROPIC_API_KEY=""
    export ANTHROPIC_DEFAULT_OPUS_MODEL="$LODE_MODEL"
    export ANTHROPIC_DEFAULT_SONNET_MODEL="$LODE_MODEL"
    export ANTHROPIC_DEFAULT_HAIKU_MODEL="$LODE_MODEL"

    exec claude --enable-auto-mode --model "$LODE_MODEL" \
        --append-system-prompt "$LODE_PROMPT" ${LODE_ARGS[@]+"${LODE_ARGS[@]}"}
}

main "$@"
