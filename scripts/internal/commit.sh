#!/usr/bin/env bash
# Commit helper. Appends a name-only Co-Authored-By trailer (never an email).
# Usage: commit.sh [--skip-precommit] MESSAGE
source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

# Name-only co-author trailer — no email, per the global privacy rule.
COAUTHOR_TRAILER="Co-Authored-By: Claude"

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
# A co-author trailer, if wanted, must be name-only; email placeholders use example.com.
if contains_real_email "$MSG"; then
    err "Commit message contains an email address — blocked by the privacy rule:"
    real_emails_in "$MSG" | while IFS= read -r e; do printf "    %s\n" "$e" >&2; done
    err "Remove it (use a name-only trailer, or a user@example.com placeholder)."
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

# Commit with the message plus a name-only co-author trailer (no email — global privacy rule)
git commit -m "$MSG" -m "$COAUTHOR_TRAILER"

ok "Committed: $MSG"
