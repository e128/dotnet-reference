#!/usr/bin/env bash
# Discover code-review-relevant agents dynamically from .claude/agents/.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

JSON_MODE=false
for arg in "$@"; do
    case "$arg" in
        --json) JSON_MODE=true ;;
        --help|-h)
            echo "Usage: review-agents.sh [--json]"
            echo "Discovers agents relevant to code review by keyword matching."
            exit 0
            ;;
    esac
done

ROOT="$(find_repo_root)"
AGENTS_DIR="$ROOT/.claude/agents"

if [[ ! -d "$AGENTS_DIR" ]]; then
    if $JSON_MODE; then
        printf '{"agents":[],"count":0}\n'
    else
        warn "No agents directory found"
    fi
    exit 0
fi

# Include keywords — agent description must contain at least one
INCLUDE_PATTERN='code|review|check|fix|validate|compliance|security|quality|refactor|build|test|warning|diagnostic|concurrency|performance'

# Exclude keywords — agent description must not contain any
EXCLUDE_PATTERN='pipeline|lode|corpus|mhtml|markdown|web|fetch|download'

json_entries=""
count=0

for file in "$AGENTS_DIR"/*.md; do
    [[ ! -f "$file" ]] && continue

    name="$(basename "${file%.md}")"

    # Extract description from frontmatter
    desc=""
    in_desc=false
    while IFS= read -r line; do
        if [[ "$line" =~ ^description: ]]; then
            in_desc=true
            desc="${line#description:}"
            desc="${desc# }"
            [[ "$desc" == ">"* ]] && desc="" && continue
            continue
        fi
        if $in_desc; then
            [[ "$line" =~ ^[a-zA-Z_-]+: ]] || [[ "$line" == "---" ]] && break
            desc="$desc ${line#"${line%%[![:space:]]*}"}"
        fi
    done < <(sed -n '/^---$/,/^---$/p' "$file" 2>/dev/null)

    desc_lower="$(echo "$desc" | tr '[:upper:]' '[:lower:]')"

    # Check include
    if ! echo "$desc_lower" | rg -q "$INCLUDE_PATTERN" 2>/dev/null; then
        continue
    fi

    # Check exclude
    if echo "$desc_lower" | rg -q "$EXCLUDE_PATTERN" 2>/dev/null; then
        continue
    fi

    ((count++)) || true

    if $JSON_MODE; then
        local_entry="$(printf '{"name":"%s","path":".claude/agents/%s.md"}' "$name" "$name")"
        if [[ -n "$json_entries" ]]; then
            json_entries="$json_entries,$local_entry"
        else
            json_entries="$local_entry"
        fi
    else
        printf "  %s\n" "$name"
    fi
done

if $JSON_MODE; then
    printf '{"agents":[%s],"count":%d}\n' "$json_entries" "$count"
else
    ok "Found $count review-relevant agents"
fi
