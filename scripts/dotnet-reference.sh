#!/usr/bin/env bash
# Lode-enabled claude wrapper with an Ollama backend. Default model deepseek-v4.1-flash:cloud.
# Usage: dotnet-reference.sh [--model MODEL] [--append-system-prompt TEXT] [CLAUDE_ARGS...]

source "$(dirname "${BASH_SOURCE[0]}")/lode-ollama-lib.sh"

lode_run "deepseek-v4.1-flash:cloud" "$@"
