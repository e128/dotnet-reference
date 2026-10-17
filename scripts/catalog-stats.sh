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

# Read the frontmatter once: the 7 tracked fields plus the folded description.
# Prints FIELD<TAB>VALUE lines. The --- toggle matches the old sed range.
read_frontmatter() {
    awk '
        BEGIN { split("name tools maxTurns memory model isolation effort", F, " "); nf = 7 }
        /^---$/ {
            in_range = !in_range
            if (in_desc && !desc_done) desc_done = 1
            next
        }
        in_range {
            for (i = 1; i <= nf; i++) {
                f = F[i]
                if (!(f in got) && $0 ~ ("^" f ":")) {
                    got[f] = 1
                    v = $0
                    sub("^" f ": *", "", v)
                    print f "\t" v
                }
            }
        }
        in_range && !desc_done && substr($0, 1, 12) == "description:" {
            after = substr($0, 13)
            if (substr(after, 1, 1) == " ") after = substr(after, 2)
            in_desc = 1
            if (substr(after, 1, 1) == ">") next
            desc = after
            next
        }
        in_range && in_desc && !desc_done {
            if ($0 == "---" || $0 ~ /^[a-zA-Z_-]+:/) { desc_done = 1; next }
            s = $0
            sub(/^[[:space:]]+/, "", s)
            desc = desc " " s
        }
        END { print "desc\t" desc }
    ' "$1" 2>/dev/null
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

# Check if a name appears in keyword-shortcuts.md. Read once into KW_CONTENT.
# A catalog name holds no regex metacharacter, so a literal test is safe.
in_keyword_table() {
    local name="$1"
    if [[ -n "$KW_CONTENT" && "$KW_CONTENT" == *"$name"* ]]; then
        echo "true"
    else
        echo "false"
    fi
}

KW_CONTENT="$(cat "$KEYWORDS_FILE" 2>/dev/null || true)"

# Collect entries
json_entries=""
declare -a text_rows=()

process_file() {
    local file="$1" type="$2"
    local name total_lines meaningful_lines desc desc_len
    local tools maxturns memory model isolation effort in_kw
    local -A fm=()
    local k v

    while IFS=$'\t' read -r k v; do
        fm["$k"]="$v"
    done < <(read_frontmatter "$file")

    name="${fm[name]:-}"
    [[ -z "$name" ]] && name="$(basename "${file%.md}")"

    total_lines="$(wc -l < "$file" | tr -d ' ')"
    meaningful_lines="$(count_meaningful "$file")"

    desc="${fm[desc]:-}"
    desc_len="${#desc}"

    tools="${fm[tools]:-}"
    maxturns="${fm[maxTurns]:-}"
    memory="${fm[memory]:-}"
    model="${fm[model]:-}"
    isolation="${fm[isolation]:-}"
    effort="${fm[effort]:-}"
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

        local row
        printf -v row "%-30s %-7s %5d %5d %5d %-6s %-10s %s" \
            "$name" "$type" "$total_lines" "$meaningful_lines" "$desc_len" "$in_kw" "${maxturns:-—}" "${flags:-—}"
        text_rows+=("$row")
    fi
}

# Discover agents
agent_count=0
if [[ -d "$AGENTS_DIR" ]]; then
    while IFS= read -r f; do
        process_file "$f" "agent"
        agent_count=$((agent_count + 1))
    done < <(fd -t f -g "*.md" "$AGENTS_DIR" 2>/dev/null | sort)
fi

# Discover skills
skill_count=0
if [[ -d "$SKILLS_DIR" ]]; then
    while IFS= read -r f; do
        process_file "$f" "skill"
        skill_count=$((skill_count + 1))
    done < <(fd -t f -g "SKILL.md" "$SKILLS_DIR" 2>/dev/null | sort)
fi

# First copy of the catalog rows, printed before the totals block.
if [[ "$JSON_MODE" == false && ${#text_rows[@]} -gt 0 ]]; then
    printf '%s\n' "${text_rows[@]}"
fi

if $JSON_MODE; then
    printf '{"agents":%d,"skills":%d,"catalog":[%s]}\n' "$agent_count" "$skill_count" "$json_entries"
else
    printf "\n${BOLD}%-30s %-7s %5s %5s %5s %-6s %-10s %s${RESET}\n" \
        "Name" "Type" "Lines" "Mean." "Desc" "InKW" "MaxTurns" "Flags"
    printf "${DIM}%-30s %-7s %5s %5s %5s %-6s %-10s %s${RESET}\n" \
        "------------------------------" "-------" "-----" "-----" "-----" "------" "----------" "----------"
    # Second copy of the catalog rows, from the one processing pass.
    if [[ ${#text_rows[@]} -gt 0 ]]; then
        printf '%s\n' "${text_rows[@]}"
    fi
    echo
    ok "Totals: $agent_count agents, $skill_count skills"
fi
