#!/usr/bin/env bash
# Poll-until-condition with timeout.
# Usage: loop.sh [--interval SEC] [--timeout SEC] [--until-clean] [--until-build] COMMAND...
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

SCRIPTS="$(dirname "${BASH_SOURCE[0]}")"
INTERVAL=10; TIMEOUT=300; CONDITION=""; CMD=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --interval)    INTERVAL="$2"; shift ;;
        --timeout)     TIMEOUT="$2"; shift ;;
        --until-clean) CONDITION="clean" ;;
        --until-build) CONDITION="build" ;;
        -*)            err "Unknown flag: $1"; exit 1 ;;
        *)             CMD+=("$1") ;;
    esac
    shift
done

START=$SECONDS
ATTEMPTS=0

while true; do
    ELAPSED=$((SECONDS - START))
    if [[ $ELAPSED -ge $TIMEOUT ]]; then
        err "Timeout after ${TIMEOUT}s ($ATTEMPTS attempts)"
        exit 1
    fi

    ATTEMPTS=$((ATTEMPTS + 1))

    case "$CONDITION" in
        clean)
            if [[ -z "$(git status --porcelain 2>/dev/null)" ]]; then
                ok "Working tree clean (attempt $ATTEMPTS)"
                exit 0
            fi
            dim "Attempt $ATTEMPTS: working tree not clean, retrying in ${INTERVAL}s..."
            ;;
        build)
            if "$SCRIPTS/build.sh" > /dev/null 2>&1; then
                ok "Build passed (attempt $ATTEMPTS)"
                exit 0
            fi
            dim "Attempt $ATTEMPTS: build failed, retrying in ${INTERVAL}s..."
            ;;
        *)
            if [[ ${#CMD[@]} -gt 0 ]]; then
                if "${CMD[@]}" > /dev/null 2>&1; then
                    ok "Command succeeded (attempt $ATTEMPTS)"
                    exit 0
                fi
                dim "Attempt $ATTEMPTS: command failed, retrying in ${INTERVAL}s..."
            else
                err "No condition or command specified"
                exit 1
            fi
            ;;
    esac

    sleep "$INTERVAL"
done
