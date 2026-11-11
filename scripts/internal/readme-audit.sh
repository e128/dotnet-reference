#!/usr/bin/env bash
# Audit README claims against the repository source of truth.
# Compares install-snippet versions, the SDK requirement, the root project
# table, and the two generated table diffs. Pure string compare and set-diff,
# so it needs no judgment.
#
# Usage: readme-audit.sh [--json] [--help]
#
# The exit status is 0 whether or not drift is found. Read `status` from the
# output. A caller that treated a drift finding as a script failure would never
# see the finding list.
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib.sh"

ROOT="$(find_repo_root)"
JSON=false

for arg in "$@"; do
    case "$arg" in
        --json) JSON=true ;;
        -h | --help)
            cat <<'EOF'
readme-audit.sh — README claims against the source of truth

Options:
  --json   Emit {"status":"clean|drift","count":N,"findings":[...]}
  --help   Show this help

Each finding carries: file, line, check, expected, actual. Line 0 means the
finding points at a table row set, not one line.
EOF
            exit 0
            ;;
        *)
            err "Unknown flag: $arg"
            exit 1
            ;;
    esac
done

FINDINGS="$(mktemp)"
trap 'rm -f "$FINDINGS"' EXIT

add_finding() {
    printf '%s\t%s\t%s\t%s\t%s\n' "$1" "$2" "$3" "$4" "$5" >> "$FINDINGS"
}

# ── Install snippet version ──────────────────────
# A packable project README must quote the csproj version.
for csproj in "$ROOT"/src/*/*.csproj; do
    [[ -f "$csproj" ]] || continue
    readme="$(dirname "$csproj")/README.md"
    [[ -f "$readme" ]] || continue

    package="$(rg -o -r '$1' '<PackageId>([^<]+)</PackageId>' "$csproj" --no-filename | head -1 || true)"
    version="$(rg -o -r '$1' '<Version>([^<]+)</Version>' "$csproj" --no-filename | head -1 || true)"
    [[ -n "$package" && -n "$version" ]] || continue

    while IFS= read -r hit; do
        [[ -n "$hit" ]] || continue
        line="${hit%%:*}"
        text="${hit#*:}"
        documented="$(printf '%s' "$text" | rg -o -r '$1' 'Version="([^"]+)"' || true)"
        [[ "$documented" == "$version" ]] ||
            add_finding "$readme" "$line" "install version for $package" "$version" "$documented"
    done < <(rg -n --no-heading "Include=\"$package\" Version=\"" "$readme" || true)
done

# ── SDK requirement ──────────────────────────────
sdk="$(jq -r '.sdk.version' "$ROOT/global.json")"
sdk_band="${sdk%.*}"
sdk_hit="$(rg -n --no-heading '^\| *\.NET SDK *\| *[0-9]+\.[0-9]+\+' "$ROOT/README.md" | head -1 || true)"
if [[ -n "$sdk_hit" ]]; then
    line="${sdk_hit%%:*}"
    text="${sdk_hit#*:}"
    documented="$(printf '%s' "$text" | rg -o -r '$1' '\| *([0-9]+\.[0-9]+)\+' || true)"
    [[ "$documented" == "$sdk_band" ]] ||
        add_finding "README.md" "$line" "SDK requirement" "$sdk_band+" "$documented+"
fi

# ── Project table ────────────────────────────────
# A row whose bolded name carries a dot is a project claim. The other rows name
# tools and documents, so they stay out of the set.
solution="$(fd -e slnx -1 . "$ROOT" | head -1)"
solution_projects="$(rg -o -r '$1' 'Path="[^"]*/([^/"]+)\.csproj"' "$solution" | sort -u)"
readme_projects="$(rg -o -r '$1' '^\| \*\*([A-Za-z0-9.]+)\*\*' "$ROOT/README.md" | rg '\.' | sort -u || true)"

while IFS= read -r name; do
    [[ -n "$name" ]] || continue
    rg -qxF "$name" <<< "$readme_projects" ||
        add_finding "README.md" "0" "project table omits $name" "$name" "absent"
done <<< "$solution_projects"

while IFS= read -r name; do
    [[ -n "$name" ]] || continue
    rg -qxF "$name" <<< "$solution_projects" ||
        add_finding "README.md" "0" "project table lists unknown $name" "absent" "$name"
done <<< "$readme_projects"

# ── Generated tables ─────────────────────────────
analyzer_json="$("$ROOT/scripts/readme-table-diff.sh" --analyzer --json)"
if [[ "$(jq -r '.drift' <<< "$analyzer_json")" == "true" ]]; then
    while IFS= read -r item; do
        add_finding "src/E128.Analyzers/README.md" "0" "analyzer rule table" "in sync" "$item"
    done < <(jq -r '[(.missing_from_readme[]?), (.extra_in_readme[]?), (.code_fix_mismatches[]?)] | .[] | tostring' <<< "$analyzer_json")
fi

scripts_json="$("$ROOT/scripts/readme-table-diff.sh" --json)"
if [[ "$(jq -r '.drift' <<< "$scripts_json")" == "true" ]]; then
    while IFS= read -r item; do
        add_finding "scripts/README.md" "0" "script table" "in sync" "$item"
    done < <(jq -r '[(.missing_from_readme[]?), (.extra_in_readme[]?)] | .[] | tostring' <<< "$scripts_json")
fi

# ── Report ───────────────────────────────────────
count="$(awk 'END { print NR + 0 }' "$FINDINGS")"
status="clean"
((count == 0)) || status="drift"

if $JSON; then
    findings_json="$(jq -Rn '[inputs | split("\t") | {file: .[0], line: .[1], check: .[2], expected: .[3], actual: .[4]}]' < "$FINDINGS")"
    printf '{"status":"%s","count":%s,"findings":%s}\n' "$status" "$count" "$findings_json"
elif ((count == 0)); then
    ok "README audit clean"
else
    while IFS=$'\t' read -r file line check expected actual; do
        printf '%s:%s: %s (expected %s, found %s)\n' "$file" "$line" "$check" "$expected" "$actual"
    done < "$FINDINGS"
fi
