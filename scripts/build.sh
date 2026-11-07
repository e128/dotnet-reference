#!/usr/bin/env bash
# Build the solution or a specific project.
# Usage: build.sh [--project NAME] [--warnings] [--fix] [--errors] [--verbose] [--fast]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

VERBOSE=false; WARNINGS=false; FIX=false; ERRORS=false; PROJECT=""; FAST=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json)     : ;;  # legacy alias — terse is now the default
        --verbose)  VERBOSE=true ;;
        --warnings) WARNINGS=true ;;
        --fix)      FIX=true ;;
        --errors)   ERRORS=true ;;
        --fast)     FAST=true ;;
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
if [[ "$FAST" == true ]]; then
    # Analyzer execution is about 78 percent of a full rebuild. The fast path skips
    # every analyzer, so scripts/check.sh remains the gate before a commit.
    ARGS+=(-p:RunAnalyzers=false)
    warn "Fast build: analyzers skipped. Run scripts/check.sh before commit."
fi
if [[ "$WARNINGS" == false ]]; then
    ARGS+=(-clp:ErrorsOnly)
fi

if ! output=$("${ARGS[@]}" 2>&1); then
    if [[ "$ERRORS" == true ]]; then
        # A failure before the compiler runs has no error lines, so fall back to
        # the output tail instead of printing nothing.
        if ! echo "$output" | grep ': error ' | head -40; then
            echo "$output" | tail -20
        fi
    elif [[ "$VERBOSE" == true ]]; then
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
