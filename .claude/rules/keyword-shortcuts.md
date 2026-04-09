# Keyword Shortcuts

These phrases in user messages always invoke the corresponding script or skill.

| User says                                                  | Invokes                       |
| ---------------------------------------------------------- | ----------------------------- |
| `am I good` / `check this` / `verify changes`             | `scripts/check.sh`            |
| `what changed` / `show status` / `git status`             | `scripts/status.sh`           |
| `show diff` / `diff summary`                              | `scripts/diff.sh`             |
| `next task` / `task progress`                              | `scripts/task.sh next`        |
| `format check` / `check format`                           | `scripts/format.sh --check`   |
| `fix format` / `apply format`                             | `scripts/format.sh --changed` |
| `get timestamp` / `iso timestamp`                          | `scripts/ts.sh`               |
| `run ci` / `full ci`                                       | `scripts/ci.sh`               |
| `preflight` / `ready to commit`                            | `/yeet --dry-run`             |
