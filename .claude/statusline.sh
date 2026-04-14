#!/usr/bin/env bash
set -euo pipefail

DATA=$(cat)

# Extract fields via single jq call
IFS=$'\t' read -r MODEL MODEL_ID DIR PCT DURATION_MS ADDED REMOVED < <(
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
        (.cost.total_duration_ms // 0),
        (.cost.total_lines_added // 0),
        (.cost.total_lines_removed // 0)
    ] | @tsv'
)

# Git info
BRANCH=$(git -c core.useBuiltinFSMonitor=false branch --show-current 2>/dev/null || echo "")

# CI status (cached 30s to avoid API spam)
CI_CACHE="/tmp/.claude-ci-status-$(git -C "$(pwd)" rev-parse --show-toplevel 2>/dev/null | tr '/' '-')"
CI_TTL=30
CI_ICON=""
if [ -f "$CI_CACHE" ] && [ "$(( $(date +%s) - $(stat -f%m "$CI_CACHE" 2>/dev/null || echo 0) ))" -lt "$CI_TTL" ]; then
  CI_ICON=$(cat "$CI_CACHE")
else
  # Derive org/repo from git remote URL (works for both HTTPS and SSH remotes)
  REMOTE_URL=$(git remote get-url origin 2>/dev/null || echo "")
  GH_REPO=$(echo "$REMOTE_URL" | sed -E 's|.*github\.com[:/]||; s|\.git$||')
  CI_RAW=$([ -n "$GH_REPO" ] && gh api "repos/${GH_REPO}/actions/runs" --jq '.workflow_runs[0] | (.status + ":" + (.conclusion // ""))' 2>/dev/null || echo "")
  case "$CI_RAW" in
    completed:success)   CI_ICON="\033[38;5;84m● CI pass\033[0m" ;;
    completed:failure)   CI_ICON="\033[38;5;203m● CI fail\033[0m" ;;
    completed:cancelled) CI_ICON="\033[38;5;228m● CI cancelled\033[0m" ;;
    in_progress:*)       CI_ICON="\033[38;5;228m◌ CI running\033[0m" ;;
    queued:*)            CI_ICON="\033[38;5;242m◌ CI queued\033[0m" ;;
    *)                   CI_ICON="\033[38;5;242m○ CI ?\033[0m" ;;
  esac
  echo -e "$CI_ICON" > "$CI_CACHE"
fi

# Build progress bar
FILLED=$((PCT * 10 / 100))
EMPTY=$((10 - FILLED))
BAR=""
for ((i=0; i<FILLED; i++)); do
  if [ $i -lt 3 ]; then BAR+="\033[38;5;51m█"
  elif [ $i -lt 6 ]; then BAR+="\033[38;5;33m█"
  else BAR+="\033[38;5;57m█"
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

# Threshold colors
if [ "$PCT" -gt 80 ]; then CTX_CLR="\033[38;5;203m"
elif [ "$PCT" -gt 50 ]; then CTX_CLR="\033[38;5;228m"
else CTX_CLR="\033[38;5;84m"
fi

echo -e "\033[38;5;141m🔷\033[0m \033[38;5;141;1m$MODEL\033[0m\033[2m\033[38;5;242m ║ \033[0m\033[38;5;141m📁 $DIR\033[0m\033[2m\033[38;5;242m ║ \033[0m$([ -n "$BRANCH" ] && printf '%b' "\033[38;5;84m🌿 $BRANCH\033[0m")\033[2m\033[38;5;242m ║ \033[0m$CI_ICON\033[0m\033[2m\033[38;5;242m ║ \033[0m$BAR\033[0m ${CTX_CLR}$PCT%\033[0m\033[2m\033[38;5;242m ║ \033[0m\033[38;5;141m$TIME\033[0m\033[2m\033[38;5;242m ║ \033[0m\033[38;5;84m+$ADDED\033[0m \033[38;5;203m-$REMOVED\033[0m\033[0m"
