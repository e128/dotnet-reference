#!/usr/bin/env bash
# Git history forensics: churn, contributors, bugs, velocity, firefights.
# Usage: git-forensics.sh [COMMAND] [--since DATE] [--top N] [--json]

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

SINCE="1 year ago"
TOP=20
JSON=false
COMMAND="all"

usage() {
    cat <<'EOF'
Usage: git-forensics.sh [churn|contributors|bugs|velocity|firefights|all] [options]

  --since DATE   Time window for churn, bugs, firefights (default: "1 year ago")
  --top N        Row count for ranked lists (default: 20)
  --json         Emit structured data instead of tables
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        churn|contributors|bugs|velocity|firefights|all) COMMAND="$1"; shift ;;
        --since)      SINCE="${2:?--since requires a value}"; shift 2 ;;
        --since=*)    SINCE="${1#--since=}"; shift ;;
        --top)        TOP="${2:?--top requires a value}"; shift 2 ;;
        --top=*)      TOP="${1#--top=}"; shift ;;
        --json)       JSON=true; shift ;;
        -h|--help)    usage; exit 0 ;;
        *)            err "Unknown argument: $1"; usage >&2; exit 1 ;;
    esac
done

if [[ ! "$TOP" =~ ^[1-9][0-9]*$ ]]; then
    err "--top needs a positive integer: $TOP"
    exit 1
fi

# Convert "left<TAB>right" rows into a JSON array. $1 is a jq filter over
# arrays of two strings.
rows_to_json() {
    local filter="$1"
    shift
    if [[ $# -eq 0 ]]; then
        printf '[]\n'
        return 0
    fi
    printf '%s\n' "$@" \
        | jq -R -s 'split("\n") | map(select(length > 0) | split("\t"))' \
        | jq "$filter"
}

# Print a two-column table with a title line.
print_table() {
    local title="$1" col1="$2" col2="$3"
    shift 3
    if [[ -n "$title" ]]; then
        printf '%b%s%b\n' "$BLUE" "$title" "$RESET"
    fi
    local width=${#col1} row left
    for row in ${@+"$@"}; do
        left="${row%%$'\t'*}"
        if [[ ${#left} -gt $width ]]; then width=${#left}; fi
    done
    printf '  %-*s  %s\n' "$width" "$col1" "$col2"
    for row in ${@+"$@"}; do
        printf '  %-*s  %s\n' "$width" "${row%%$'\t'*}" "${row#*$'\t'}"
    done
}

# Rank paths by how often git log names them.
rank_paths() {
    sort | uniq -c \
        | awk '{ count=$1; $1=""; sub(/^ +/, ""); print count "\t" $0 }' \
        | sort -t$'\t' -k1,1nr -k2,2
}

# Keep the first TOP existing files. $1 holds "count<TAB>path" rows.
keep_existing() {
    local n=0 count file
    while IFS=$'\t' read -r count file; do
        [[ -n "$file" && -f "$file" ]] || continue
        printf '%s\t%s\n' "$file" "$count"
        n=$((n + 1))
        if [[ $n -ge $TOP ]]; then break; fi
    done <<<"$1"
}

churn_rows() {
    local ranked
    ranked="$(git log --format=format: --name-only --since="$SINCE" | awk 'NF' | rank_paths)"
    keep_existing "$ranked"
}

bug_rows() {
    local ranked
    ranked="$(git log -i -E --grep=fix --grep=bug --grep=broken --name-only --format='' \
        --since="$SINCE" | awk 'NF' | rank_paths)"
    keep_existing "$ranked"
}

contributor_rows() {
    git shortlog -sn --no-merges --all \
        | awk -v top="$TOP" -F'\t' 'NR <= top { gsub(/^ +/, "", $1); print $2 "\t" $1 }'
}

velocity_rows() {
    git log --format='%ad' --date=format:'%Y-%m' \
        | sort | uniq -c \
        | awk '{ print $2 "\t" $1 }'
}

firefight_rows() {
    git log --oneline --since="$SINCE" \
        | grep -iE 'revert|hotfix|emergency|rollback' \
        | awk '{ hash=$1; $1=""; sub(/^ +/, ""); print hash "\t" $0 }' || true
}

show_churn() {
    local -a rows=()
    mapfile -t rows < <(churn_rows)
    if $JSON; then
        rows_to_json 'map({file: .[0], count: (.[1] | tonumber)})' ${rows[@]+"${rows[@]}"}
    else
        print_table "Most-changed files (since $SINCE):" "file" "count" ${rows[@]+"${rows[@]}"}
    fi
}

show_contributors() {
    local -a rows=()
    mapfile -t rows < <(contributor_rows)
    if $JSON; then
        rows_to_json 'map({author: .[0], count: (.[1] | tonumber)})' ${rows[@]+"${rows[@]}"}
    else
        print_table "Top contributors:" "author" "count" ${rows[@]+"${rows[@]}"}
    fi
}

show_bugs() {
    local -a rows=()
    mapfile -t rows < <(bug_rows)
    if $JSON; then
        rows_to_json 'map({file: .[0], count: (.[1] | tonumber)})' ${rows[@]+"${rows[@]}"}
    else
        print_table "Bug hotspots (since $SINCE):" "file" "count" ${rows[@]+"${rows[@]}"}
    fi
}

show_velocity() {
    local -a rows=()
    mapfile -t rows < <(velocity_rows)
    if $JSON; then
        rows_to_json 'map({month: .[0], count: (.[1] | tonumber)})' ${rows[@]+"${rows[@]}"}
    else
        print_table "Commit velocity by month:" "month" "count" ${rows[@]+"${rows[@]}"}
    fi
}

show_firefights() {
    local -a rows=()
    mapfile -t rows < <(firefight_rows)
    if $JSON; then
        rows_to_json 'map({hash: .[0], message: .[1]})' ${rows[@]+"${rows[@]}"}
    else
        printf '%b%s%b\n' "$BLUE" "Firefighting commits (since $SINCE): ${#rows[@]}" "$RESET"
        if [[ ${#rows[@]} -eq 0 ]]; then
            echo "None found."
        else
            print_table "" "hash" "message" ${rows[@]+"${rows[@]}"}
        fi
    fi
}

show_all() {
    if $JSON; then
        jq -n \
            --argjson churn "$(show_churn)" \
            --argjson contributors "$(show_contributors)" \
            --argjson bugs "$(show_bugs)" \
            --argjson velocity "$(show_velocity)" \
            --argjson firefights "$(show_firefights)" \
            '{churn: $churn, contributors: $contributors, bugs: $bugs, velocity: $velocity, firefights: $firefights}'
        return 0
    fi
    show_churn
    echo
    show_contributors
    echo
    show_bugs
    echo
    show_velocity
    echo
    show_firefights
}

case "$COMMAND" in
    churn)        show_churn ;;
    contributors) show_contributors ;;
    bugs)         show_bugs ;;
    velocity)     show_velocity ;;
    firefights)   show_firefights ;;
    all)          show_all ;;
esac
