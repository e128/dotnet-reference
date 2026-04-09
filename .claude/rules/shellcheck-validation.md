# Shell Script Validation

After writing or editing any `.sh` file, validate syntax before considering the task complete:

```bash
bash -n <file.sh>
```

If `shellcheck` is installed, also run:

```bash
shellcheck <file.sh>
```

This catches parse errors immediately instead of discovering them on first run.

**When creating a new `scripts/*.sh` file**, also verify it appears in `help.sh` output:

```bash
scripts/help.sh | grep <script-name>
```
