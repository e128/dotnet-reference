#!/usr/bin/env bash
# Sync Claude Code agent definitions into the opencode agent mirror.
# Usage: opencode-agents.sh [sync|check] [--json]
set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
SRC_DIR="$REPO_ROOT/.claude/agents"
DST_DIR="$REPO_ROOT/.opencode/agents"

MODE="check"
JSON=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    sync) MODE="sync"; shift ;;
    check) MODE="check"; shift ;;
    --json|-j) JSON=true; shift ;;
    -*) echo "Unknown flag: $1" >&2; exit 1 ;;
    *) echo "Unexpected argument: $1" >&2; exit 1 ;;
  esac
done

[[ -d "$SRC_DIR" ]] || { echo "Source directory not found: $SRC_DIR" >&2; exit 1; }

# Translate a Claude Code tool name to its opencode equivalent.
# Prints the name, or nothing when the tool has no opencode counterpart.
translate_tool() {
  local tool="$1"
  case "$tool" in
    Bash) echo "bash" ;;
    Read) echo "read" ;;
    Write) echo "write" ;;
    Edit) echo "edit" ;;
    Glob) echo "glob" ;;
    Grep) echo "grep" ;;
    Agent|Task) echo "task" ;;
    WebFetch) echo "webfetch" ;;
    WebSearch) echo "websearch" ;;
    TodoWrite) echo "todowrite" ;;
    mcp__*) ;; # Claude-only MCP tools have no opencode counterpart
    *) echo "$tool" >&2 ;;
  esac
}

# Rewrite one Claude Code agent file as an opencode subagent definition.
# Keeps description and body verbatim. Drops Claude-only frontmatter fields.
# Adds mode: subagent and translates the tools list into a permission map.
# Read-only agents get an explicit edit deny.
transform_agent() {
  local src="$1" dst="$2"
  local -a fm=()
  local -a body=()
  local -a raw_tools=()
  local line key rest word mapped tok
  local in_fm=false done_fm=false has_write=false
  local -A seen=()

  while IFS= read -r line; do
    if [[ "$done_fm" == false ]]; then
      if [[ "$line" == "---" ]]; then
        if [[ "$in_fm" == true ]]; then
          done_fm=true
        else
          fm+=("---" "mode: subagent")
          in_fm=true
        fi
        continue
      fi
      if [[ "$line" =~ ^([A-Za-z][A-Za-z0-9_-]*):(.*)$ ]]; then
        key="${BASH_REMATCH[1]}"
        rest="${BASH_REMATCH[2]}"
        case "$key" in
          description) fm+=("$line") ;;
          tools)
            if [[ -n "${rest// /}" ]]; then
              raw_tools+=("$rest")
            fi
            ;;
          name|color|model|maxTurns|memory|isolation|effort) ;;
          *) fm+=("$line") ;;
        esac
        continue
      fi
      # Continuation lines follow the current top-level key.
      case "$key" in
        description) fm+=("$line") ;;
        tools) raw_tools+=("$line") ;;
      esac
      continue
    fi
    body+=("$line")
  done < "$src"

  for tok in "${raw_tools[@]}"; do
    tok="${tok//,/ }"
    for word in $tok; do
      mapped="$(translate_tool "$word")"
      if [[ -n "$mapped" && -z "${seen[$mapped]:-}" ]]; then
        seen["$mapped"]=1
      fi
      case "$word" in
        Write|Edit) has_write=true ;;
      esac
    done
  done

  if [[ ${#seen[@]} -gt 0 || "$has_write" == false ]]; then
    fm+=("permission:")
    for word in "${!seen[@]}"; do
      fm+=("  $word: allow")
    done
    if [[ "$has_write" == false ]]; then
      fm+=("  edit: deny")
    fi
  fi
  fm+=("---")

  mkdir -p "$(dirname "$dst")"
  {
    printf '%s\n' "${fm[@]}"
    printf '%s\n' "${body[@]}"
  } > "$dst"
}

emit_result() {
  local status="$1" generated="$2" differences="$3"
  if $JSON; then
    jq -nc \
      --arg status "$status" \
      --argjson generated "$generated" \
      --argjson differences "$differences" \
      '{status: $status, generated: $generated, differences: $differences}'
  else
    echo "$status: generated=$generated differences=$differences"
  fi
}

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

shopt -s nullglob
GENERATED=0
for src in "$SRC_DIR"/*.md; do
  name="$(basename "$src")"
  transform_agent "$src" "$TMP_DIR/$name"
  GENERATED=$((GENERATED + 1))
done
[[ "$GENERATED" -gt 0 ]] || { echo "No agent files found in $SRC_DIR" >&2; exit 1; }

# Copy agent asset directories (references, templates) verbatim.
for asset_dir in "$SRC_DIR"/*/; do
  [[ -d "$asset_dir" ]] || continue
  cp -R "$asset_dir" "$TMP_DIR/$(basename "$asset_dir")"
done

DIFF_COUNT=0
if [[ -d "$DST_DIR" ]]; then
  DIFF_COUNT="$({ diff -rq "$DST_DIR" "$TMP_DIR" || true; } | wc -l | tr -d ' ')"
else
  DIFF_COUNT="$GENERATED"
fi

if [[ "$MODE" == "sync" ]]; then
  rm -rf "$DST_DIR"
  cp -R "$TMP_DIR" "$DST_DIR"
  emit_result "ok" "$GENERATED" 0
else
  if [[ "$DIFF_COUNT" -eq 0 ]]; then
    emit_result "clean" 0 0
  else
    if $JSON; then
      emit_result "stale" 0 "$DIFF_COUNT"
    else
      echo "Agent mirror is stale ($DIFF_COUNT differences). Run: scripts/internal/opencode-agents.sh sync" >&2
      diff -rq "$DST_DIR" "$TMP_DIR" || true
      exit 1
    fi
  fi
fi
