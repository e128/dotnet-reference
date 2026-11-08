#!/usr/bin/env bash
# Lode-enabled opencode wrapper. Model and prompt come from opencode.json.
# Usage: dotnet-reference-oc.sh [--model MODEL] [MESSAGE|SUBCOMMAND...]

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

# One-shot runs and the mini TUI take --model. The full TUI does not; it reads
# the model from opencode.json.
readonly DEFAULT_MODEL="${OPENCODE_OC_MODEL:-opencode-go/glm-5.3-flash}"
readonly SUBCOMMANDS=" run mini models mcp auth agent config serve upgrade help version "

main() {
    local model="$DEFAULT_MODEL"
    local model_given=false
    local -a args=()
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --model)
                if [[ $# -lt 2 || -z "$2" ]]; then
                    err "--model requires a value"
                    exit 2
                fi
                model="$2"
                model_given=true
                shift 2
                ;;
            --model=*)
                model="${1#--model=}"
                model_given=true
                shift
                ;;
            *)
                args+=("$1")
                shift
                ;;
        esac
    done

    if [[ ${#args[@]} -eq 0 ]]; then
        if [[ "$model_given" == true ]]; then
            warn "The full TUI takes no --model flag; using the model in opencode.json. Use /model inside the TUI to switch."
        fi
        exec opencode
    fi

    # A bare message is a one-shot run. A subcommand or a flag passes through.
    local first="${args[0]}"
    if [[ "$first" == -* || "$SUBCOMMANDS" == *" $first "* ]]; then
        set -- "${args[@]}"
    else
        first="run"
        set -- run "${args[@]}"
    fi

    case "$first" in
        run|mini)
            exec opencode "$first" --model "$model" "${@:2}"
            ;;
        *)
            exec opencode "${@}"
            ;;
    esac
}

main "$@"
