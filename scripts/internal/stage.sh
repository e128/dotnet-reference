#!/usr/bin/env bash
# Staging helper: add modified tracked files, optionally include new files.
# Usage: stage.sh [--include-new]
source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

INCLUDE_NEW=false
[[ "${1:-}" == "--include-new" ]] && INCLUDE_NEW=true

ROOT="$(find_repo_root)"

# Stage all modified tracked files
git -C "$ROOT" add -u

if [[ "$INCLUDE_NEW" == true ]]; then
    # Stage untracked files (excluding secrets and temp)
    git -C "$ROOT" ls-files --others --exclude-standard | while IFS= read -r file; do
        case "$file" in
            .env*|*.key|*.pem|credentials*) warn "Skipping potential secret: $file" ;;
            *) git -C "$ROOT" add "$file" ;;
        esac
    done
fi

STAGED_COUNT=$(git -C "$ROOT" diff --cached --name-only | wc -l | tr -d ' ')
ok "Staged $STAGED_COUNT file(s)"
