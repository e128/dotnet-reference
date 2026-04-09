#!/usr/bin/env bash
# Commit helper with co-author trailer.
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

SCRIPTS="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Run precommit checks
if [[ "$SKIP_PRECOMMIT" == false ]]; then
    if ! "$SCRIPTS/internal/precommit.sh"; then
        err "Precommit checks failed"
        exit 1
    fi
fi

# Commit with co-author
git commit -m "$(cat <<EOF
${MSG}

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"

ok "Committed: $MSG"
