#!/usr/bin/env bash
# Build the solution or a specific project.
# Usage: build.sh [--project NAME] [--warnings] [--fix] [--verbose]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

VERBOSE=false; WARNINGS=false; FIX=false; PROJECT=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json)     : ;;  # legacy alias — terse is now the default
        --verbose)  VERBOSE=true ;;
        --warnings) WARNINGS=true ;;
        --fix)      FIX=true ;;
        --project)  PROJECT="$2"; shift ;;
        *)          err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

if [[ "$FIX" == true ]]; then
    "$(dirname "${BASH_SOURCE[0]}")/format.sh" --changed
fi

TARGET=""
if [[ -n "$PROJECT" ]]; then
    TARGET="$(resolve_project "$PROJECT")"
    if [[ -z "$TARGET" ]]; then
        err "Project not found: $PROJECT"
        exit 1
    fi
else
    TARGET="$(find_solution)"
fi

ARGS=(dotnet build "$TARGET" --nologo)
if [[ "$WARNINGS" == false ]]; then
    ARGS+=(-clp:ErrorsOnly)
fi

if ! output=$("${ARGS[@]}" 2>&1); then
    if [[ "$VERBOSE" == true ]]; then
        err "Build failed"
        echo "$output"
    else
        json_object status=fail target="$(basename "$TARGET")" \
            errors="$(echo "$output" | grep -c ' error ' || true)"
    fi
    exit 1
fi

if [[ "$VERBOSE" == true ]]; then
    ok "Build succeeded: $(basename "$TARGET")"
else
    json_object status=ok target="$(basename "$TARGET")" warnings=0 errors=0
fi
