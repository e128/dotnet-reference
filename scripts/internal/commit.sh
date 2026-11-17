#!/usr/bin/env bash
# Commit helper. Rejects email addresses and Claude Code attribution in the message.
# Usage: commit.sh [--skip-precommit] MESSAGE
source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

SKIP_PRECOMMIT=false; MSG=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --skip-precommit) SKIP_PRECOMMIT=true ;;
        --msg)            MSG="$2"; shift ;;
        -*)               err "Unknown flag: $1"; exit 1 ;;
        *)                [[ -z "$MSG" ]] && MSG="$1" ;;
    esac
    shift
done

if [[ -z "$MSG" ]]; then
    err "Commit message required"
    exit 1
fi

# Block any email address in the commit message (global privacy rule).
if contains_real_email "$MSG"; then
    err "Commit message contains an email address — blocked by the privacy rule:"
    real_emails_in "$MSG" | while IFS= read -r e; do printf "    %s\n" "$e" >&2; done
    err "Remove it (use a user@example.com placeholder)."
    exit 1
fi

# Block tool attribution (AGENTS.md: never name Claude Code in a commit or PR).
if printf '%s\n' "$MSG" | rg -qi 'claude[ -]code|generated with claude|co-authored-by: *claude'; then
    err "Commit message names Claude Code — blocked by the attribution rule."
    exit 1
fi

SCRIPTS="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Run precommit checks
if [[ "$SKIP_PRECOMMIT" == false ]]; then
    if ! "$SCRIPTS/internal/precommit.sh"; then
        err "Precommit checks failed"
        exit 1
    fi
fi

# Commit the message as written. No attribution trailer is appended.
git commit -m "$MSG"

ok "Committed: $MSG"
