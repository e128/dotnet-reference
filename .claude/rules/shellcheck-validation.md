# Shell Script Validation

Validate syntax after you write or edit any `.sh` file. Do this before you call
the task complete:

```bash
bash -n <file.sh>
```

When `shellcheck` is installed, also run:

```bash
shellcheck <file.sh>
```

This catches a parse error immediately, not on the first run.

**After you create a new `scripts/*.sh` file**, confirm it appears in the help
output:

```bash
scripts/help.sh | rg <script-name>
```
