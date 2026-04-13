# Token Efficiency

- Never re-read files you just wrote or edited. You know the contents.
- Never re-run commands to "verify" unless the outcome was uncertain.
- Don't echo back large blocks of code or file contents unless asked.
- Batch related edits into single operations.
- If a task needs 1 tool call, don't use 3.
- **Use `scripts/build.sh` instead of raw `dotnet build`.** Output is terse JSON by default; use `--verbose` for full MSBuild log.
- **Use `scripts/test.sh` instead of raw `dotnet test`.** Output is terse JSON by default; use `--verbose` for full output.
- **Use `scripts/status.sh --json` instead of raw `git status`.**
- **Use `scripts/diff.sh --json` instead of multiple `git diff` / `git log` calls.** Use `--staged` for staged-only view instead of raw `git diff --cached`.
- **Use `scripts/check.sh` instead of running format + build + test separately.**
- **Use `scripts/ci.sh` for full CI runs.** Combines format + build + test.
- **Use `scripts/context.sh` at session start.** Combines status + diff + plans.
- **Use `scripts/task.sh` instead of Read → Edit → Read on tasks.md files.**
- **Use `scripts/ts.sh` instead of raw `date -u`.**
- **Use `scripts/branch.sh` instead of `git rev-list --count`.**
- **Use `scripts/help.sh` to discover available scripts.**
- **Use `/yeet` (via Skill tool) instead of raw `git push`.** `/yeet` runs preflight + commit + push.
- Do not summarize what you just did unless the result is ambiguous.
