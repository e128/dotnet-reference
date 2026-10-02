#!/usr/bin/env bash
# tech-writing-lint.sh: mechanical self-lint for the tech-writing skill.
#
# Checks prose against Google Developer Style plus the house overrides:
#   DASH       em dash, en dash, horizontal bar, minus sign, " -- ", and entity spellings
#   SENTENCE   sentence over 25 words (repo STE descriptive cap)
#   CONTRACT   contraction (repo STE bans them)
#   SEMICOLON  semicolon (repo STE bans them)
#   WORD       banned word choices ("allows you to", "i.e.", "e.g.", "etc.", "and/or",
#              "click here", "please", "simply", "the user")
#   TIME       time-anchored words (currently, now, soon, eventually, ...)
#   CLAIM      superlative or guarantee wording
#   PASSIVE?   heuristic only: regular past participle followed by "by"
#
# Scope: plain text and markdown. Fenced code blocks, inline code spans and HTML
# tags are stripped first. Judgment calls stay with the writer. The repo STE rule
# (.claude/rules/writing-style.md) adds the contraction and semicolon checks.
#
# Usage:
#   tech-writing-lint.sh <file>...        check files
#   cat text.md | tech-writing-lint.sh -  check stdin
#   tech-writing-lint.sh --json <file>    machine-readable findings (stderr)
#
# Exit: 0 clean, 1 findings present, 2 usage error.

set -euo pipefail

usage() { sed -n '2,22p' "$0" | sed 's/^# \{0,1\}//'; }

JSON=0
FILES=()
for arg in "$@"; do
  case "$arg" in
    --help|-h) usage; exit 0 ;;
    --json) JSON=1 ;;
    *) FILES+=("$arg") ;;
  esac
done
[[ ${#FILES[@]} -eq 0 ]] && { usage; exit 2; }

AWK_PROG='
BEGIN {
  n_word = split("allows you to|enables you to|and/or|i.e.|e.g.|etc.|click here|please|simply|the user|in order to|it is worth noting|please note", word_arr, "|");
  n_time = split("currently|now|soon|eventually|presently|as of this writing|newer|latest|at this time", time_arr, "|");
  n_claim = split("seamless|robust|powerful|effortless|revolutionary|cutting-edge|world-class|guarantees|always works|fastest|best", claim_arr, "|");
}
{
  raw = $0; line = raw
  gsub(/`[^`]*`/, " ", line)
  gsub(/<[^>]*>/, " ", line)
  if (in_fence) { if (line ~ /^```/) in_fence = 0; next }
  if (line ~ /^```/) { in_fence = 1; next }
  lc = tolower(line)

  if (line ~ /\xe2\x80\x94|\xe2\x80\x93|\xe2\x80\x95|\xe2\x88\x92/ || lc ~ /&mdash;|&ndash;|&minus;|&#8212;|&#8211;|&#x2014;|&#x2013;/ || line ~ / -- /) report(FNR, "DASH", raw)

  for (i = 1; i <= n_word; i++) if (index(lc, word_arr[i]) > 0) report(FNR, "WORD", word_arr[i])
  for (i = 1; i <= n_time; i++) if (match(lc, "(^|[^a-z])" time_arr[i] "([^a-z]|$)")) report(FNR, "TIME", time_arr[i])
  for (i = 1; i <= n_claim; i++) if (index(lc, claim_arr[i]) > 0) report(FNR, "CLAIM", claim_arr[i])

  if (lc ~ "[a-z]\047(t|re|ll|ve|d|m|s)([^a-z]|$)") report(FNR, "CONTRACT", raw)
  if (index(line, ";") > 0) report(FNR, "SEMICOLON", raw)

  if (lc ~ /(^|[^a-z])(is|are|was|were|be|been|being) [a-z]+ed by /) report(FNR, "PASSIVE?", raw)

  nsent = split(line, sents, /[.!?]+[ \t]/)
  for (s = 1; s <= nsent; s++) {
    sent = sents[s]; gsub(/^[ \t]+|[ \t]+$/, "", sent)
    if (sent == "") continue
    if (split(sent, words, /[ \t]+/) > 25) report(FNR, "SENTENCE>25w", sent)
  }
}
function report(ln, rule, snippet) {
  if (length(snippet) > 80) snippet = substr(snippet, 1, 77) "..."
  gsub(/"/, "\\\"", snippet)
  if (json) printf("{\"file\":\"%s\",\"line\":%d,\"rule\":\"%s\",\"text\":\"%s\"}\n", FILENAME, ln, rule, snippet) > "/dev/stderr"
  else printf("%s:%d: [%s] %s\n", FILENAME, ln, rule, snippet)
}
'

exit_code=0
for f in "${FILES[@]}"; do
  target="$f"; label="$f"
  [[ "$f" == "-" ]] && { target=/dev/stdin; label="(stdin)"; }
  out=$(LC_ALL=C awk -v json="$JSON" "$AWK_PROG" "$target" 2>&1 | sed "s#^$target#$label#") || true
  [[ -n "$out" ]] && { echo "$out"; exit_code=1; }
done
exit "$exit_code"
