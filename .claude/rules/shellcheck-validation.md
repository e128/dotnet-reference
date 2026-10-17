# Shell Script Validation

Run `scripts/lint-shell.sh` after you write or edit any `.sh` file. Do this
before you call the task complete.

```bash
scripts/lint-shell.sh              # every tracked .sh file
scripts/lint-shell.sh path/to.sh   # one file
```

The script runs `shellcheck -S warning`. A warning fails the run. The gate
ignores the note-level findings, so `SC1091` needs no suppression.

Pass `--json` for `{"status","files","findings","details"}`.

## Where the Gate Runs

The same script is a step of these entries. Never add a second shellcheck
invocation to them.

- `scripts/check.sh`
- `scripts/ci.sh`
- `scripts/internal/precommit.sh`, over the staged shell files only
- the CI job named `shellcheck`

shellcheck parses every file it reads, so a syntax error fails the same run. A
separate `bash -n` call is not needed.

**After you create a new `scripts/*.sh` file**, confirm it appears in the help
output:

```bash
scripts/help.sh | rg <script-name>
```

## Fix the Finding, Do Not Suppress It

Resolve the finding in the code. Use `# shellcheck disable=SC<N>` only for a
false positive, on one line, with a comment that states why.

```bash
# BOLD is used only by the sourcing scripts, never inside this file.
# shellcheck disable=SC2034
RED='' GREEN='' YELLOW='' BLUE='' DIM='' BOLD='' RESET=''
```

To compare findings before and after a refactor, match on file and rule code.
Never match on raw text. The line number moves with the edit, so a text compare
reports an unchanged finding as new.

## See Also

- [deterministic-scripts.md](deterministic-scripts.md) — the command routing
  table
- [quality-gates.md](quality-gates.md) — the format gate and the suppression
  policy
