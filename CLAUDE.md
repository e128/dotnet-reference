# Project Instructions for Claude

*Last updated: 2026-07-16T13:48:06Z*

## Communication

- **Terse responses** — code over explanation unless asked
- **No time estimates.** Never predict how long a task or feature will take.
- **Ask before fixing** — flag issues and suggestions, wait for approval before changing, **except** the low-risk changes enumerated in [Auto-Approvals](.claude/rules/auto-approvals.md), which are applied silently
- **Use `AskUserQuestion` for all decisions.** Never ask questions inline in text output.
- **Timestamps** — ISO 8601 UTC format everywhere (e.g., `2026-04-09T12:00:00Z`)
- **Markdown tables must use aligned columns.** See [Markdown Formatting](.claude/rules/markdown-formatting.md).

## Workflow

### Tests

- **Never use raw `dotnet test` directly.** Always use `scripts/test.sh` (targeted) or `scripts/test.sh --all` (full CI suite). This project uses xUnit v3 MTP — raw `dotnet test --filter` does not work. Pass a class name as a positional argument to run targeted tests (e.g., `scripts/test.sh MidNameUnderscoreAnalyzerTests`); other test projects returning exit code 8 (zero matches) is expected.
- **Never run `check.sh` and `build.sh` together.** `check.sh` already includes build.

### TDD

Follow TDD (Red-Green-Refactor) for new features and significant changes. New failing tests required before implementation code.

**RED phase stubs use `Assert.Fail(message)`.** Never use `Assert.True(false, message)` or `throw new NotImplementedException()`.

### TDD Efficiency

**Batch fixes before testing.** Collect fixes into batches of 5+, apply all edits first, then test once.

**Targeted tests first.** For changes affecting ≤5 files, use `scripts/test.sh ClassName` before the full suite.

### Builds

- Build/test scripts run without a separate ask-gate (they are effectively read-only); `scripts/build-budget.sh` governs frequency. See [Auto-Approvals](.claude/rules/auto-approvals.md).
- **`scripts/check.sh`** — composed verify (format → build → targeted tests). Use `--all` for full CI suite.
- **`scripts/build.sh`** — build only (no tests). Use `--warnings` to include warnings; `--fix` to format first.

### Lode

**Lode is the authoritative memory store.** All project knowledge MUST go to `lode/`, never `MEMORY.md`.

## .NET Development

**Implicit usings are disabled** (`<ImplicitUsings>disable</ImplicitUsings>`). Every `.cs` file must have explicit `using` directives.

### Design Principles

- **Favor immutable code by default.** Prefer `record`, `readonly struct`, `init`-only properties, and immutable collections.
- **Apply SOLID principles by default.** Balance with YAGNI.
- **All interface method parameters must be used.**
- **Never write methods you don't call.** YAGNI applies to methods too.
- **Collapse equivalent conditional branches; comments must match code exactly.**

When making design trade-offs, apply this priority order:

1. **Immutability** — default to immutable data structures and pure functions
2. **Memory efficiency** — minimize allocations, prefer `Span<T>`/`Memory<T>`
3. **CPU efficiency** — algorithmic complexity first, micro-optimizations only after measurement
4. **Parallelism** — emerges naturally from clean functional code

### Testing

- **Never use reflection in tests by default.** Use `internal` + `InternalsVisibleTo` instead.
- **Before modifying `TargetFramework`**, run `dotnet --list-sdks` and confirm the version is installed.

### Security

- **Never hardcode security-relevant defaults.** Use options classes with safe defaults.

### Verification Gates

**Lode file size gate (mandatory).** Before appending to any lode file, check size with `wc -l`. If >200 lines, create a focused sub-file. 250-line limit is hard.

## Git Conventions

- **Commit messages**: Imperative mood, concise summary
- **Never include an email address in a commit message** — no exceptions. No `noreply@` line,
  no email in any trailer. Enforced by `commit.sh` (scans the message) and
  `scripts/internal/precommit.sh` (scans the staged diff); both reject real emails. `user@example.com`
  placeholders are allowed.
- **Branch naming**: `feature/`, `fix/`, `refactor/`
- **Squash all local commits before push** — one clean commit per PR.
- **Concurrent sessions on the same branch are expected.** Staged changes or added/removed files
  that another session left behind are normal working state, not stray or accidental — never
  `git stash`/`reset`/`clean` them away without checking first. Before staging or committing, run
  `scripts/status.sh` to see the full working tree, not just what this session touched.
- **Commit and push as one bundle.** When shipping, include all sessions' changes together in a
  single commit rather than splitting by session. Attribute correctly: use `git log` / `scripts/branch.sh`
  to confirm the actual author identity in play.

## General Behavior

**Agents and skills must not specify `model:`.** All inherit the session model. See [Agent & Skill Routing](.claude/rules/agent-vs-skill-routing.md).

Prefer focused incremental changes: one change, verify, then next.

**When friction repeats (2+ times), fix the root cause immediately.**

Prefer `.claude/` (project-level) over `~/.claude/` (global) for all config.

Always use `.claude/tmp/` instead of `/tmp`. **Never write to `/tmp`.** Scope: `lode/tmp/` holds lode-related scraps and session handovers only; `.claude/tmp/` holds all other working/scratch files.

**Never write absolute user profile paths.** Use `~` for home-relative or repo-relative paths.

### Design Principle: Deterministic Scripts Over Ad-Hoc Commands

**Favor deterministic scripts for all repeated operations.** When an operation runs more than once -- builds, tests, formatting, status checks, symbol lookups -- it belongs in a `scripts/*.sh` script, not in inline shell commands. Never use raw `dotnet build/test/format`, raw `git status/log/diff`, or ad-hoc `rg` chains when a script covers the same operation.

### Scripts & Tooling

**Never use inline scripts for repeated operations.** Check `scripts/help.sh` first.

**Fix broken scripts immediately -- never work around them.** When a script produces wrong output, fix the script first.

**Script naming convention:**
- `scripts/*.sh` -- all scripts use bash 5+ and live in `scripts/`
- `scripts/internal/*.sh` -- scripts invoked by skills and agents only

### Key Scripts

Run `scripts/help.sh` for the full catalog (name, usage, and flags for every `scripts/*.sh` and `scripts/internal/*.sh`).

