#!/usr/bin/env bash
# Sync Claude Code agent definitions into the opencode v2 agent mirror.
# Usage: opencode-agents.sh [sync|check] [--json]
set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
SRC_DIR="$REPO_ROOT/.claude/agents"
DST_DIR="$REPO_ROOT/.opencode/agents"
ASSETS_DIR="$REPO_ROOT/.opencode/agent-assets"

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

# Rewrite one Claude Code agent file as an opencode subagent definition.
# Keeps description and body verbatim. Drops Claude-only frontmatter fields.
# Adds mode: subagent and, for a read-only agent, an edit deny rule.
#
# opencode gives every agent the base policy {*, *, allow} before any agent
# rule, so a per-tool allow list adds nothing. Only a deny carries meaning.
transform_agent() {
  local src="$1" dst="$2"
  local -a fm=()
  local -a body=()
  local -a raw_tools=()
  local -a sed_args=()
  local line key rest word tok name

  local in_fm=false done_fm=false has_write=false

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
      case "$word" in
        Write|Edit) has_write=true ;;
      esac
    done
  done

  if [[ "$has_write" == false ]]; then
    fm+=("permissions:")
    fm+=("  - action: edit")
    fm+=("    resource: \"*\"")
    fm+=("    effect: deny")
  fi
  fm+=("---")

  # The mirror holds agent assets in a sibling directory, because opencode
  # walks agents/ recursively. Repoint the in-body links at that location.
  for name in "${ASSET_NAMES[@]}"; do
    sed_args+=(-e "s|](${name}/|](../agent-assets/${name}/|g")
  done

  mkdir -p "$(dirname "$dst")"
  {
    printf '%s\n' "${fm[@]}"
    printf '%s\n' "${body[@]}"
  } | sed "${sed_args[@]}" > "$dst"
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

TMP_AGENTS="$TMP_DIR/agents"
TMP_ASSETS="$TMP_DIR/agent-assets"
mkdir -p "$TMP_AGENTS" "$TMP_ASSETS"

shopt -s nullglob
ASSET_NAMES=()
for asset_dir in "$SRC_DIR"/*/; do
  [[ -d "$asset_dir" ]] || continue
  ASSET_NAMES+=("$(basename "$asset_dir")")
done

GENERATED=0
for src in "$SRC_DIR"/*.md; do
  name="$(basename "$src")"
  transform_agent "$src" "$TMP_AGENTS/$name"
  GENERATED=$((GENERATED + 1))
done
[[ "$GENERATED" -gt 0 ]] || { echo "No agent files found in $SRC_DIR" >&2; exit 1; }

# Copy agent asset directories (references, templates) verbatim. They live
# outside agents/, because opencode walks that tree recursively and would
# register every markdown file it finds as an agent.
for name in "${ASSET_NAMES[@]}"; do
  cp -R "$SRC_DIR/$name" "$TMP_ASSETS/$name"
done

# Both trees form the mirror: agents/ and the sibling agent-assets/.
report_diff() {
  diff -rq "$DST_DIR" "$TMP_AGENTS" 2>/dev/null || true
  diff -rq "$ASSETS_DIR" "$TMP_ASSETS" 2>/dev/null || true
}

DIFF_COUNT="$({ report_diff; } | wc -l | tr -d ' ')"

if [[ "$MODE" == "sync" ]]; then
  rm -rf "$DST_DIR" "$ASSETS_DIR"
  cp -R "$TMP_AGENTS" "$DST_DIR"
  if [[ -n "$(ls -A "$TMP_ASSETS")" ]]; then
    cp -R "$TMP_ASSETS" "$ASSETS_DIR"
  fi
  emit_result "ok" "$GENERATED" 0
else
  if [[ "$DIFF_COUNT" -eq 0 ]]; then
    emit_result "clean" 0 0
  else
    if $JSON; then
      emit_result "stale" 0 "$DIFF_COUNT"
    else
      echo "Agent mirror is stale ($DIFF_COUNT differences). Run: scripts/internal/opencode-agents.sh sync" >&2
      report_diff
      exit 1
    fi
  fi
fi
