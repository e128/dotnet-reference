#!/usr/bin/env bash
# Pre-commit scan: shell lint, home paths, email addresses, and secrets (gitleaks).
# Usage: precommit.sh [--json]
source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

SCRIPTS="$(dirname "${BASH_SOURCE[0]}")/.."
JSON=false
[[ "${1:-}" == "--json" ]] && JSON=true

ROOT="$(find_repo_root)"
STAGED=$(git -C "$ROOT" diff --cached --name-only 2>/dev/null)

if [[ -z "$STAGED" ]]; then
    if [[ "$JSON" == true ]]; then
        json_object status=ok findings=0
    else
        ok "No staged files to scan"
    fi
    exit 0
fi

FINDINGS=0
DETAILS=()

# Check for home directory paths
# shortcut: a colon in a staged path breaks the file prefix, upgrade when one appears
HOME_PATTERN="/Users/[a-zA-Z]|/home/[a-zA-Z]|C:\\\\Users\\\\"
STAGED_FILES=()
while IFS= read -r file; do
    if [[ -n "$file" && -f "$ROOT/$file" ]]; then
        STAGED_FILES+=("$file")
    fi
done <<< "$STAGED"

if [[ ${#STAGED_FILES[@]} -gt 0 ]]; then
    # Skip the co-author line
    matches=$(cd "$ROOT" && grep -HnE "$HOME_PATTERN" "${STAGED_FILES[@]}" 2>/dev/null | grep -v "Co-Authored-By" || true)
    if [[ -n "$matches" ]]; then
        while IFS= read -r match_file; do
            FINDINGS=$((FINDINGS + 1))
            DETAILS+=("$match_file: home path detected")
        done < <(printf '%s\n' "$matches" | cut -d: -f1 | uniq)
    fi
fi

# Check staged diff additions for real email addresses (global privacy rule).
# Only added lines are scanned; example.com/.org/.net placeholders are allowed.
ADDED=$(git -C "$ROOT" diff --cached -U0 2>/dev/null | grep -E '^\+[^+]' || true)
while IFS= read -r email; do
    [[ -z "$email" ]] && continue
    FINDINGS=$((FINDINGS + 1))
    DETAILS+=("email address detected: $email")
done <<< "$(real_emails_in "$ADDED")"

# Secret scan: gitleaks over the staged diff. Findings are redacted in output.
if command -v gitleaks >/dev/null 2>&1; then
    if ! LEAKS=$(cd "$ROOT" && gitleaks git --staged --no-banner --redact --exit-code 1 2>&1); then
        FINDINGS=$((FINDINGS + 1))
        DETAILS+=("gitleaks found a secret in the staged diff:" "$LEAKS")
    fi
else
    warn "gitleaks not installed: secret scan skipped (brew install gitleaks)"
fi

# Shell lint: shellcheck over the staged shell scripts only. lint-shell.sh owns
# the severity and the tool-version check.
STAGED_SH=()
for f in "${STAGED_FILES[@]}"; do
    [[ "$f" == *.sh ]] && STAGED_SH+=("$f")
done

if [[ ${#STAGED_SH[@]} -gt 0 ]]; then
    if ! SC=$(cd "$ROOT" && "$SCRIPTS/lint-shell.sh" "${STAGED_SH[@]}" 2>&1); then
        FINDINGS=$((FINDINGS + 1))
        DETAILS+=("shellcheck finding(s) in staged scripts:" "$SC")
    fi
fi

if [[ $FINDINGS -gt 0 ]]; then
    if [[ "$JSON" == true ]]; then
        json_object status=fail findings="$FINDINGS"
    else
        err "Pre-commit scan found $FINDINGS issue(s):"
        for d in "${DETAILS[@]}"; do
            printf "  %s\n" "$d"
        done
    fi
    exit 1
fi

if [[ "$JSON" == true ]]; then
    json_object status=ok findings=0
else
    ok "Pre-commit scan clean"
fi
