# Script Performance
*Updated: 2026-10-31T16:06:37Z*

Rules for the bash scripts in `scripts/`. The scripts run on macOS and on Linux
CI. Both hosts pay a high price for a new process. The rules below remove that
price.

## The Cost Model

A fork plus exec costs one to three milliseconds on macOS. That cost is
invisible once. It dominates a loop that runs 10,000 times.

A pipeline builds two or more processes for every stage. A `cmd | while read`
loop also moves the loop body into a subshell. That move costs correctness as
well as time. See [The Subshell Trap](#the-subshell-trap).

Measure a change before you land it. The corpus in `.claude/tmp/` holds
session transcripts for the session scripts. Use it as the benchmark input.

## Rules

1. Never start a subprocess inside a loop body. Hoist it above the loop.
2. Use a bash builtin before you use an external tool.
3. Use `mapfile` instead of `while read` to fill an array.
4. Use `done < <(cmd)` instead of `cmd | while read` when the loop mutates a
   variable.
5. Use parameter expansion instead of `sed`, `cut`, `tr`, or `basename`.
6. Pass many files to one `grep` or `rg` call with `-h` or `--no-filename`.
7. Use one `awk` pass instead of a `sort | uniq -c | sort -rn` chain.
8. Use one `jq` call at the end of a loop instead of one `jq` call per row.
9. Never build a JSON document inside a pipeline subshell.
10. Prove the output does not change before you land the fix. See
    [Verification](#verification).

## Match the Tool to the Job

Process count is not the only cost. Scan speed per byte matters too.

- `grep`, `wc`, and `rg` scan far faster per byte than `awk`.
- `awk` wins when one pass replaces many passes over a small input.
- `awk` loses when it replaces a few passes over a large input.

Measure both forms on a large corpus before you choose.

```bash
# Faster here: three scans by fast tools over a few large files.
total_messages=$(wc -l "${files[@]}" | awk 'END { print $1 + 0 }')
tool_uses=$( { grep -c '"tool_use"' "${files[@]}" || true; } |
    awk -F: '{ s += $NF } END { print s + 0 }')

# Slower here: one awk pass over the same bytes.
read -r total_messages tool_uses < <(
    awk '{ n++; if (/"tool_use"/) u++ } END { print n+0, u+0 }' "${files[@]}"
)
```

## Patterns

### Hoist the Subprocess

Each loop iteration starts a new `grep`. One call replaces every one of them.

```bash
# Slow: one grep process per file.
for f in "${files[@]}"; do
    grep -o '"name":"[^"]*"' "$f" >> "$tmpfile"
done

# Fast: one grep process for the whole list.
(( ${#files[@]} )) && grep -ho '"name":"[^"]*"' "${files[@]}" >> "$tmpfile" || true
```

### Build JSON Once

```bash
# Slow: one jq process per row.
for row in "${rows[@]}"; do
    jq -n --arg k "$row" '{key: $k}'
done | jq -s '.'

# Fast: one printf loop, one jq process.
for row in "${rows[@]}"; do
    printf '%s\n' "$row"
done | jq -R -s 'split("\n") | map(select(length > 0)) | map({key: .})'
```

### Cache a Shared Read

A subcommand that reads every transcript is fine alone. Four of them in one run
read the corpus four times. Concatenate once into a temporary file.

```bash
TMPDIR_WORK="$(mktemp -d)"
trap 'rm -rf "$TMPDIR_WORK"' EXIT
CACHE="$TMPDIR_WORK/sessions.jsonl"
cat "${recent_files[@]}" > "$CACHE"
```

Every subcommand then reads `"$CACHE"`. The `trap` removes it on any exit.

## The Subshell Trap

`cmd | while read` runs the loop body in a subshell. A variable the body sets
does not survive the loop. This defect also breaks JSON output.

```bash
# Broken: the loop body sets `first`, but the body runs in a subshell.
first=true
printf '['
grep -o '"name":"[^"]*"' "$file" | while read -r name; do
    name="${name#\"name\":\"}"
    [[ "$first" == true ]] || printf ','
    printf '{"name":"%s"}' "${name%\"}"
    first=false
done
printf ']\n'
# Emits malformed JSON: [{"name":"Bash"}{"name":"Read"}{"name":"Edit"}]

# Correct: read from a process substitution, so the loop runs here.
first=true
printf '['
while read -r name; do
    name="${name#\"name\":\"}"
    [[ "$first" == true ]] || printf ','
    printf '{"name":"%s"}' "${name%\"}"
    first=false
done < <(grep -o '"name":"[^"]*"' "$file")
printf ']\n'
# Emits valid JSON: [{"name":"Bash"},{"name":"Read"},{"name":"Edit"}]
```

A second trap sits in the same place. With `set -e` and `set -o pipefail`, one
failing command inside the pipeline ends the whole loop. The loop then exits
early with no error message.

```bash
# Broken: rg exits 1 on the first file with no match. `set -e` kills the loop.
mapfile -t ids < <(fd -e cs . src | while read -r f; do
    rg 'DiagnosticId = "(E128[0-9]{3})"' "$f" -or '$1' --no-filename
done | sort -u)

# Correct: the loop survives a non-zero exit from one iteration.
mapfile -t ids < <(rg 'DiagnosticId = "(E128[0-9]{3})"' src -or '$1' --no-filename | sort -u)
```

Prefer the second form even when the exit code does not matter. One `rg` call
over a directory is faster than a loop over its files.

## head Fails Under pipefail

A pipeline that ends in `head` fails while the producer still writes. `head`
exits after N lines. The producer then receives SIGPIPE and exits 141. With
`set -o pipefail` that status becomes the status of the whole pipeline.

```bash
# Broken: exits 141 and truncates the output.
git log -S "TODO" --format="%H %ad" -- . | head -30

# Correct: `|| true` absorbs the SIGPIPE.
git log -S "TODO" --format="%H %ad" -- . | head -30 || true
```

The same trap hits a command substitution. There the failed assignment ends the
script through `set -e`. Put the `|| true` inside the substitution.

## Bash Version Floor

The scripts require bash 4.0 or later. Bash 4.0 provides `mapfile` and
associative arrays. Do not use a feature above that floor.

| Feature                            | Needs |
| ---------------------------------- | ----- |
| `mapfile`, `declare -A`, `${v,,}`  | 4.0   |
| `wait -n`                          | 4.3   |
| `${var@Q}`                         | 4.4   |
| `EPOCHSECONDS`, `${var@U}`         | 5.0   |
| Fork-free `${ cmd; }`              | 5.3   |

`scripts/lib.sh` re-execs under Homebrew Bash when the interpreter is older
than 4.0. See [scripts/README.md](../../scripts/README.md) for the version
table.

## Stable Order

`rg` and `fd` emit each file as a worker finishes it. Two runs over the same
files can give two different orders.

```bash
# Unstable: the edge list changes from run to run.
done < <(rg -oN -r '$1' 'Include="([^"]+)"' "${CSPROJ[@]}")

# Stable: sorted once, before the loop.
done < <(rg -oN -r '$1' 'Include="([^"]+)"' "${CSPROJ[@]}" | LC_ALL=C sort)
```

A `-H` result carries a `path:value` prefix. Split on the colon to read it. A
default `IFS` keeps the whole line in the first variable.

```bash
while IFS=: read -r src_file src_value; do ... done
```

## Verification

Never land a performance fix on a promise. Prove the output is byte-identical.

1. Copy the committed script to a scratch directory.
   `git show HEAD:scripts/foo.sh > .claude/tmp/old/foo.sh`
2. Copy every file the old copy sources or calls, with the same layout.
   A script in `scripts/internal/` sources `../lib.sh`, not `./lib.sh`.
3. Run the old and the new script with the same arguments.
4. Capture stdout, stderr, and the exit code for each.
5. Compare with `diff`.

A missing sibling makes the old copy fail early. An early failure reads as a
fixed defect when the new script is the only one that runs. Copy the sibling
scripts from `HEAD` too, not just the library.

```bash
run() { local bin="$1"; shift; { "$bin" "$@" 2>&1; echo "EXIT=$?"; } ; }

for args in "" "--json" "--verbose"; do
    diff <(run .claude/tmp/old/foo.sh $args) <(run scripts/foo.sh $args) \
        || echo "DIFFERS: $args"
done
```

A normalized compare is acceptable only while a script is still unstable. Fix
the source of the instability first, then compare the raw output.

Compare shellcheck findings by file and rule code, never by raw text. A moved
line changes the reported line number. A text compare then reports an old
finding as new. Use `shellcheck -f gcc` and strip the line and column.

State every assumption your fix adds. An added assumption is a latent defect
when it stops holding.

## See Also

- [claude-code-maintenance.md](claude-code-maintenance.md): script conventions
  and the harness layout
- [scripts/README.md](../../scripts/README.md): version floors and the script
  catalog
- [deterministic-scripts.md](../../.claude/rules/deterministic-scripts.md): the
  command routing table
- [shellcheck-validation.md](../../.claude/rules/shellcheck-validation.md): the
  shell linter and where the gate runs
