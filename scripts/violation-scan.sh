#!/usr/bin/env bash
# Scan for .NET anti-patterns and rule violations in source and .claude/ files.
# Usage: violation-scan.sh [--json] [--claude] [--path DIR]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ROOT="$(find_repo_root)"
JSON=false
SCAN_CLAUDE=false
SCAN_PATH=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --json)   JSON=true ;;
        --claude) SCAN_CLAUDE=true ;;
        --path)   SCAN_PATH="$2"; shift ;;
        *)        err "Unknown flag: $1"; exit 1 ;;
    esac
    shift
done

# ── Pattern definitions ──────────────────────────────────────────────────────
# Parallel arrays: REGEX, NAME, REASON.
# Regexes use | internally so we cannot use | as a delimiter — use parallel arrays.
declare -a P_REGEX=(
    'DateTime\.(Now|UtcNow)\b'
    'new HttpClient\s*\('
    'async void\s+[A-Za-z_]'
    '\.(Result|GetAwaiter\(\)\.GetResult\(\))\s*[;,\)]'
)
declare -a P_NAME=(
    'datetime-now'
    'new-httpclient'
    'async-void'
    'sync-over-async'
)
declare -a P_REASON=(
    'Use TimeProvider via DI instead of DateTime.Now/UtcNow'
    'Use IHttpClientFactory instead of new HttpClient()'
    'Use async Task instead of async void (except event handlers)'
    'Await instead of .Result / .GetAwaiter().GetResult()'
)

# Lines matching this pattern are excluded as string-literal or comment contexts.
EXCLUDE_PATTERN='^\s*(//|///|\*|title:|messageFormat:|message:|description:)'

# ── Scan targets ─────────────────────────────────────────────────────────────
declare -a SCAN_CS_DIRS=()
declare -a SCAN_MD_DIRS=()

if [[ -n "$SCAN_PATH" ]]; then
    SCAN_CS_DIRS=("$SCAN_PATH")
else
    [[ -d "$ROOT/src" ]] && SCAN_CS_DIRS+=("$ROOT/src")
fi

if [[ "$SCAN_CLAUDE" == true ]]; then
    [[ -d "$ROOT/.claude/skills" ]] && SCAN_MD_DIRS+=("$ROOT/.claude/skills")
    [[ -d "$ROOT/.claude/agents" ]] && SCAN_MD_DIRS+=("$ROOT/.claude/agents")
fi

if [[ ${#SCAN_CS_DIRS[@]} -eq 0 && ${#SCAN_MD_DIRS[@]} -eq 0 ]]; then
    err "No directories to scan"
    exit 1
fi

# ── Scan ─────────────────────────────────────────────────────────────────────
TOTAL_VIOLATIONS=0
declare -A BY_COUNT
declare -A BY_FILES

for i in "${!P_NAME[@]}"; do
    BY_COUNT["${P_NAME[$i]}"]=0
    BY_FILES["${P_NAME[$i]}"]=""
done

# Scan a single directory set for a given pattern; filter out comment/literal lines.
# Usage: scan_files REGEX DIR... -> prints matching file paths
scan_files() {
    local regex="$1"; shift
    local dirs=("$@")
    [[ ${#dirs[@]} -eq 0 ]] && return
    rg --type cs -l "$regex" "${dirs[@]}" 2>/dev/null \
        | while IFS= read -r f; do
            if rg "$regex" "$f" 2>/dev/null | rg -v "$EXCLUDE_PATTERN" -q 2>/dev/null; then
                echo "$f"
            fi
        done
}

scan_md_files() {
    local regex="$1"; shift
    local dirs=("$@")
    [[ ${#dirs[@]} -eq 0 ]] && return
    rg --type md -l "$regex" "${dirs[@]}" 2>/dev/null || true
}

for i in "${!P_REGEX[@]}"; do
    regex="${P_REGEX[$i]}"
    name="${P_NAME[$i]}"

    mapfile -t hits < <(
        {
            [[ ${#SCAN_CS_DIRS[@]} -gt 0 ]] && scan_files "$regex" "${SCAN_CS_DIRS[@]}" || true
            [[ ${#SCAN_MD_DIRS[@]} -gt 0 ]] && scan_md_files "$regex" "${SCAN_MD_DIRS[@]}" || true
        } | sort -u
    )

    count=${#hits[@]}
    BY_COUNT[$name]=$count
    TOTAL_VIOLATIONS=$((TOTAL_VIOLATIONS + count))

    if [[ $count -gt 0 ]]; then
        local_files=()
        for f in "${hits[@]}"; do
            local_files+=("${f#"$ROOT/"}")
        done
        BY_FILES[$name]="$(printf '%s\n' "${local_files[@]}" | tr '\n' ',')"
    fi
done

# ── Output ────────────────────────────────────────────────────────────────────
if [[ "$JSON" == true ]]; then
    printf '{"violations":%d,"by_pattern":{' "$TOTAL_VIOLATIONS"
    first=true
    for i in "${!P_NAME[@]}"; do
        name="${P_NAME[$i]}"
        reason="${P_REASON[$i]}"
        [[ "$first" == true ]] && first=false || printf ','
        count="${BY_COUNT[$name]}"
        files="${BY_FILES[$name]}"
        files_json="["
        if [[ -n "$files" ]]; then
            file_first=true
            IFS=',' read -ra file_arr <<< "$files"
            for f in "${file_arr[@]}"; do
                [[ -z "$f" ]] && continue
                [[ "$file_first" == true ]] && file_first=false || files_json+=","
                # Escape backslashes and double quotes for JSON
                escaped="${f//\\/\\\\}"
                escaped="${escaped//\"/\\\"}"
                files_json+="\"$escaped\""
            done
        fi
        files_json+="]"
        # Escape reason for JSON
        reason_esc="${reason//\\/\\\\}"
        reason_esc="${reason_esc//\"/\\\"}"
        printf '"%s":{"count":%d,"reason":"%s","files":%s}' \
            "$name" "$count" "$reason_esc" "$files_json"
    done
    printf '}}\n'
else
    if [[ $TOTAL_VIOLATIONS -eq 0 ]]; then
        ok "No anti-pattern violations found"
        exit 0
    fi

    printf "${BOLD}Anti-pattern violations: %d${RESET}\n\n" "$TOTAL_VIOLATIONS"

    for i in "${!P_NAME[@]}"; do
        name="${P_NAME[$i]}"
        reason="${P_REASON[$i]}"
        count="${BY_COUNT[$name]}"
        [[ $count -eq 0 ]] && continue

        printf "${YELLOW}⚠${RESET}  ${BOLD}%s${RESET} (%d file%s)\n" \
            "$name" "$count" "$([[ $count -eq 1 ]] && echo '' || echo 's')"
        printf "   ${DIM}%s${RESET}\n" "$reason"

        files="${BY_FILES[$name]}"
        IFS=',' read -ra file_arr <<< "$files"
        for f in "${file_arr[@]}"; do
            [[ -z "$f" ]] && continue
            printf "   %s\n" "$f"
        done
        echo
    done
fi

[[ $TOTAL_VIOLATIONS -gt 0 ]] && exit 1 || exit 0
