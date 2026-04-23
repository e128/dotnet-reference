#!/usr/bin/env bash
set -euo pipefail

DATA=$(cat)

# Extract fields via single jq call
IFS=$'\t' read -r MODEL MODEL_ID DIR PCT WIN_SIZE TOK_TOTAL CACHE_PCT VERSION DURATION_MS ADDED REMOVED < <(
    echo "$DATA" | jq -r '[
        (.model.display_name // "Claude"),
        (try (.model.id // "unknown") catch "unknown"),
        (.cwd // "~" | split("/") | last),
        (try (
    if (.context_window.remaining_percentage // null) != null then
      100 - (.context_window.remaining_percentage | floor)
    elif (.context_window.context_window_size // 0) > 0 then
      (((.context_window.current_usage.input_tokens // 0) +
        (.context_window.current_usage.cache_creation_input_tokens // 0) +
        (.context_window.current_usage.cache_read_input_tokens // 0)) * 100 /
       .context_window.context_window_size) | floor
    else 0 end
  ) catch 0),
        (try (
    if (.context_window.context_window_size // 0) >= 1000000 then
      ((.context_window.context_window_size / 1000000 * 10 | floor) / 10 | tostring) + "M"
    elif (.context_window.context_window_size // 0) >= 1000 then
      (.context_window.context_window_size / 1000 | floor | tostring) + "k"
    elif (.context_window.context_window_size // 0) > 0 then
      (.context_window.context_window_size | tostring)
    else "" end
  ) catch ""),
        (try (
    (.context_window.total_input_tokens // 0) +
    (.context_window.total_output_tokens // 0)
  ) catch 0),
        (try (
    if ((.context_window.current_usage.input_tokens // 0) +
        (.context_window.current_usage.cache_creation_input_tokens // 0) +
        (.context_window.current_usage.cache_read_input_tokens // 0)) > 0 then
      (.context_window.current_usage.cache_read_input_tokens // 0) * 100 /
      ((.context_window.current_usage.input_tokens // 0) +
       (.context_window.current_usage.cache_creation_input_tokens // 0) +
       (.context_window.current_usage.cache_read_input_tokens // 0)) | floor
    else -1 end
  ) catch -1),
        (.version // ""),
        (.cost.total_duration_ms // 0),
        (.cost.total_lines_added // 0),
        (.cost.total_lines_removed // 0)
    ] | @tsv'
)

# Fifth Element palette:
#   38;5;208  Leeloo orange  (warmth, fire, the chosen one)
#   38;5;45   electric blue  (the city, tech, Diva's glow)
#   38;5;93   Zorg purple    (darkness, menace)
#   38;5;220  stone gold     (the ancient elements, Mondoshawan)
#   38;5;202  hot coral-red  (explosions, danger)
#   38;5;242  dim gray       (separators, inactive)

# Git info
BRANCH=$(git -c core.useBuiltinFSMonitor=false branch --show-current 2>/dev/null || echo "")

# CI status (cached 30s to avoid API spam)
CI_CACHE="/tmp/.claude-ci-status"
CI_TTL=30
CI_ICON=""
if [ -f "$CI_CACHE" ] && [ "$(( $(date +%s) - $(stat -f%m "$CI_CACHE" 2>/dev/null || echo 0) ))" -lt "$CI_TTL" ]; then
  CI_ICON=$(cat "$CI_CACHE")
else
  CI_RAW=$(gh api repos/e128/pugworks/actions/runs --jq '.workflow_runs[0] | (.status + ":" + (.conclusion // ""))' 2>/dev/null || echo "")
  case "$CI_RAW" in
    completed:success)   CI_ICON="\033[38;5;45m● CI pass\033[0m" ;;
    completed:failure)   CI_ICON="\033[38;5;202m● CI fail\033[0m" ;;
    completed:cancelled) CI_ICON="\033[38;5;220m● CI cancelled\033[0m" ;;
    in_progress:*)       CI_ICON="\033[38;5;220m◌ CI running\033[0m" ;;
    queued:*)            CI_ICON="\033[38;5;242m◌ CI queued\033[0m" ;;
    *)                   CI_ICON="\033[38;5;242m○ CI ?\033[0m" ;;
  esac
  echo -e "$CI_ICON" > "$CI_CACHE"
fi

# Build progress bar — electric blue → Zorg purple → Leeloo orange
FILLED=$((PCT * 10 / 100))
EMPTY=$((10 - FILLED))
BAR=""
for ((i=0; i<FILLED; i++)); do
  if [ $i -lt 3 ]; then BAR+="\033[38;5;45m█"
  elif [ $i -lt 6 ]; then BAR+="\033[38;5;93m█"
  else BAR+="\033[38;5;208m█"
  fi
done
for ((i=0; i<EMPTY; i++)); do BAR+="\033[38;5;242m⣀"; done

# Format duration
TOTAL_SEC=$((DURATION_MS / 1000))
H=$((TOTAL_SEC / 3600))
M=$(((TOTAL_SEC % 3600) / 60))
S=$((TOTAL_SEC % 60))
if [ "$H" -gt 0 ]; then TIME="${H}h ${M}m"
elif [ "$M" -gt 0 ]; then TIME="${M}m ${S}s"
else TIME="${S}s"
fi

# Threshold colors — electric blue (low) → stone gold (mid) → hot coral-red (high)
if [ "$PCT" -gt 80 ]; then CTX_CLR="\033[38;5;202m"
elif [ "$PCT" -gt 50 ]; then CTX_CLR="\033[38;5;220m"
else CTX_CLR="\033[38;5;45m"
fi

WIN_PART=""
[ -n "$WIN_SIZE" ] && WIN_PART="\033[2m\033[38;5;242m ║ \033[0m\033[38;5;45m⊞ $WIN_SIZE\033[0m"

# Token count (cumulative session total) — Zorg purple
TOK_PART=""
if [[ "$TOK_TOTAL" =~ ^[0-9]+$ ]] && [ "$TOK_TOTAL" -gt 0 ]; then
  if [ "$TOK_TOTAL" -ge 1000000 ]; then
    TOK_FMT="$(awk "BEGIN{printf \"%.1fM\", $TOK_TOTAL/1000000}")"
  elif [ "$TOK_TOTAL" -ge 1000 ]; then
    TOK_FMT="$(awk "BEGIN{printf \"%dk\", int($TOK_TOTAL/1000)}")"
  else
    TOK_FMT="$TOK_TOTAL"
  fi
  TOK_PART="\033[2m\033[38;5;242m ║ \033[0m\033[38;5;141m${TOK_FMT} tok\033[0m"
fi

# Cache hit percent (last turn; -1 = no cache data)
CACHE_PART=""
if [[ "$CACHE_PCT" =~ ^[0-9]+$ ]]; then
  if [ "$CACHE_PCT" -ge 75 ]; then CACHE_CLR="\033[38;5;45m"
  elif [ "$CACHE_PCT" -ge 40 ]; then CACHE_CLR="\033[38;5;220m"
  else CACHE_CLR="\033[38;5;242m"; fi
  CACHE_PART="\033[2m\033[38;5;242m ║ \033[0m${CACHE_CLR}⚡${CACHE_PCT}%\033[0m"
fi

# Claude Code version — stone gold
VER_PART=""
[ -n "$VERSION" ] && VER_PART="\033[38;5;220mv${VERSION}\033[0m\033[2m\033[38;5;242m ║ \033[0m"

echo -e "\033[38;5;208m☥\033[0m ${VER_PART}\033[38;5;208;1m$MODEL\033[0m\033[2m\033[38;5;242m ║ \033[0m\033[38;5;208m📁 $DIR\033[0m\033[2m\033[38;5;242m ║ \033[0m$([ -n "$BRANCH" ] && printf '%b' "\033[38;5;45m🌿 $BRANCH\033[0m")\033[2m\033[38;5;242m ║ \033[0m$CI_ICON\033[0m$WIN_PART$TOK_PART$CACHE_PART\033[2m\033[38;5;242m ║ \033[0m$BAR\033[0m ${CTX_CLR}$PCT%\033[0m\033[2m\033[38;5;242m ║ \033[0m\033[38;5;208m$TIME\033[0m\033[2m\033[38;5;242m ║ \033[0m\033[38;5;45m+$ADDED\033[0m \033[38;5;202m-$REMOVED\033[0m\033[0m"
