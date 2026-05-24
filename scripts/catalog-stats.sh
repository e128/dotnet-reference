#!/usr/bin/env bash
# Inventory all agents and skills with frontmatter fields, description lengths, and line counts.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

JSON_MODE=false
VERBOSE=false
for arg in "$@"; do
    case "$arg" in
        --json) JSON_MODE=true ;;
        --verbose) VERBOSE=true ;;
        --help|-h)
            echo "Usage: catalog-stats.sh [--json] [--verbose]"
            echo "  --json      Output structured JSON"
            echo "  --verbose   Include description text in output"
            exit 0
            ;;
    esac
done

ROOT="$(find_repo_root)"
AGENTS_DIR="$ROOT/.claude/agents"
SKILLS_DIR="$ROOT/.claude/skills"
KEYWORDS_FILE="$ROOT/.claude/rules/keyword-shortcuts.md"

# Parse YAML frontmatter field from a file.
# Usage: parse_field FILE FIELD
parse_field() {
    local file="$1" field="$2"
    sed -n "/^---$/,/^---$/{ /^${field}:/{ s/^${field}: *//; p; q; }; }" "$file" 2>/dev/null
}

# Parse multi-line YAML description (handles > folded style).
parse_description() {
    local file="$1"
    local in_desc=false
    local desc=""
    while IFS= read -r line; do
        if [[ "$line" =~ ^description: ]]; then
            in_desc=true
            local after="${line#description:}"
            after="${after# }"
            if [[ "$after" == ">"* ]]; then
                continue
            fi
            desc="$after"
            continue
        fi
        if $in_desc; then
            if [[ "$line" =~ ^[a-zA-Z_-]+: ]] || [[ "$line" == "---" ]]; then
                break
            fi
            desc="$desc ${line#"${line%%[![:space:]]*}"}"
        fi
    done < <(sed -n '/^---$/,/^---$/p' "$file" 2>/dev/null)
    echo "$desc"
}

# Count meaningful lines (excludes blank, frontmatter, code fences, header-only lines).
count_meaningful() {
    local file="$1"
    local total=0 blank=0 fm=0 fence=0 header=0
    local in_frontmatter=false fm_count=0

    while IFS= read -r line; do
        ((total++)) || true
        if [[ "$line" == "---" ]]; then
            ((fm_count++)) || true
            if [[ $fm_count -le 2 ]]; then
                ((fm++)) || true
                if [[ $fm_count -eq 1 ]]; then in_frontmatter=true; fi
                if [[ $fm_count -eq 2 ]]; then in_frontmatter=false; fi
                continue
            fi
        fi
        if $in_frontmatter; then
            ((fm++)) || true
            continue
        fi
        if [[ -z "${line// /}" ]]; then
            ((blank++)) || true
        elif [[ "$line" =~ ^'```' ]]; then
            ((fence++)) || true
        elif [[ "$line" =~ ^#{1,6}[[:space:]] ]]; then
            ((header++)) || true
        fi
    done < "$file"
    echo $((total - blank - fm - fence - header))
}

# Check if a name appears in keyword-shortcuts.md
in_keyword_table() {
    local name="$1"
    if [[ -f "$KEYWORDS_FILE" ]]; then
        rg -q "$name" "$KEYWORDS_FILE" 2>/dev/null && echo "true" || echo "false"
    else
        echo "false"
    fi
}

# Collect entries
json_entries=""

process_file() {
    local file="$1" type="$2"
    local name total_lines meaningful_lines desc desc_len
    local tools maxturns memory model isolation effort in_kw

    name="$(parse_field "$file" "name")"
    [[ -z "$name" ]] && name="$(basename "${file%.md}")"

    total_lines="$(wc -l < "$file" | tr -d ' ')"
    meaningful_lines="$(count_meaningful "$file")"

    desc="$(parse_description "$file")"
    desc_len="${#desc}"

    tools="$(parse_field "$file" "tools")"
    maxturns="$(parse_field "$file" "maxTurns")"
    memory="$(parse_field "$file" "memory")"
    model="$(parse_field "$file" "model")"
    isolation="$(parse_field "$file" "isolation")"
    effort="$(parse_field "$file" "effort")"
    in_kw="$(in_keyword_table "$name")"

    local relpath="${file#"$ROOT"/}"

    if $JSON_MODE; then
        local entry
        entry=$(printf '{"name":"%s","type":"%s","path":"%s","total_lines":%d,"meaningful_lines":%d,"desc_chars":%d,"tools":"%s","maxTurns":"%s","memory":"%s","model":"%s","isolation":"%s","effort":"%s","in_keywords":%s' \
            "$name" "$type" "$relpath" "$total_lines" "$meaningful_lines" "$desc_len" \
            "$tools" "$maxturns" "$memory" "$model" "$isolation" "$effort" "$in_kw")
        if $VERBOSE; then
            local escaped_desc
            escaped_desc="$(echo "$desc" | sed 's/"/\\"/g' | tr '\n' ' ')"
            entry="$entry,\"description\":\"$escaped_desc\""
        fi
        entry="$entry}"

        if [[ -n "$json_entries" ]]; then
            json_entries="$json_entries,$entry"
        else
            json_entries="$entry"
        fi
    else
        local flags=""
        [[ "$desc_len" -gt 1536 ]] && flags="OVER_BUDGET"
        [[ "$desc_len" -gt 1000 && "$desc_len" -le 1536 ]] && flags="AT_RISK"
        [[ -n "$model" ]] && flags="${flags:+$flags,}HAS_MODEL"
        [[ -z "$maxturns" && ("$tools" == *"Bash"* || "$tools" == *"Agent"*) ]] && flags="${flags:+$flags,}NO_MAXTURNS"

        printf "%-30s %-7s %5d %5d %5d %-6s %-10s %s\n" \
            "$name" "$type" "$total_lines" "$meaningful_lines" "$desc_len" "$in_kw" "${maxturns:-—}" "${flags:-—}"
    fi
}

# Discover agents
if [[ -d "$AGENTS_DIR" ]]; then
    while IFS= read -r f; do
        process_file "$f" "agent"
    done < <(fd -t f -g "*.md" "$AGENTS_DIR" 2>/dev/null | sort)
fi

# Discover skills
if [[ -d "$SKILLS_DIR" ]]; then
    while IFS= read -r f; do
        process_file "$f" "skill"
    done < <(fd -t f -g "SKILL.md" "$SKILLS_DIR" 2>/dev/null | sort)
fi

# Count totals
agent_count=0; skill_count=0
if [[ -d "$AGENTS_DIR" ]]; then
    agent_count="$(fd -t f -g "*.md" "$AGENTS_DIR" 2>/dev/null | wc -l | tr -d ' ')"
fi
if [[ -d "$SKILLS_DIR" ]]; then
    skill_count="$(fd -t f -g "SKILL.md" "$SKILLS_DIR" 2>/dev/null | wc -l | tr -d ' ')"
fi

if $JSON_MODE; then
    printf '{"agents":%d,"skills":%d,"catalog":[%s]}\n' "$agent_count" "$skill_count" "$json_entries"
else
    printf "\n${BOLD}%-30s %-7s %5s %5s %5s %-6s %-10s %s${RESET}\n" \
        "Name" "Type" "Lines" "Mean." "Desc" "InKW" "MaxTurns" "Flags"
    printf "${DIM}%-30s %-7s %5s %5s %5s %-6s %-10s %s${RESET}\n" \
        "------------------------------" "-------" "-----" "-----" "-----" "------" "----------" "----------"
    # Re-run to print (simpler than collecting into array for bash)
    if [[ -d "$AGENTS_DIR" ]]; then
        while IFS= read -r f; do
            process_file "$f" "agent"
        done < <(fd -t f -g "*.md" "$AGENTS_DIR" 2>/dev/null | sort)
    fi
    if [[ -d "$SKILLS_DIR" ]]; then
        while IFS= read -r f; do
            process_file "$f" "skill"
        done < <(fd -t f -g "SKILL.md" "$SKILLS_DIR" 2>/dev/null | sort)
    fi
    echo
    ok "Totals: $agent_count agents, $skill_count skills"
fi
