# Project Instructions for Claude

*Last updated: 2026-07-25*

High-frequency rules only. Detail lives in `.claude/rules/`, `lode/`,
and skills — load them on demand via filename match or progressive
disclosure.

## Communication

- Terse responses — code over explanation unless asked
- No time estimates
- Decisions go through `AskUserQuestion`, never inline text
- ISO 8601 UTC timestamps everywhere (e.g., `2026-04-09T12:00:00Z`)

Full AI assistant preferences: [lode/practices.md](lode/practices.md).

## Workflow

- **Tests** — never raw `dotnet test`; use `scripts/test.sh ClassName` or `--all`. xUnit v3 MTP. See [deterministic-scripts.md](.claude/rules/deterministic-scripts.md).
- **TDD** — Red-Green-Refactor. RED phase stubs use `Assert.Fail(message)`, never `Assert.True(false)` or `NotImplementedException`. See [quality-gates.md](.claude/rules/quality-gates.md).
- **Build** — `scripts/check.sh` is the single format+build+test gate. Build/test scripts run without an ask-gate.
- **Lode** is the authoritative memory store. All project knowledge goes to `lode/`, never `MEMORY.md`. See [lode/lode-map.md](lode/lode-map.md).

## .NET Development

- **Implicit usings disabled** (`<ImplicitUsings>disable</ImplicitUsings>`). Every `.cs` file has explicit `using` directives.
- **Sealed by default** — all non-abstract classes are sealed (enforced by E128005 + ArchUnitNET).
- **Design principles** — Immutability > Memory > CPU > Parallelism. Full guidance: [lode/practices.md](lode/practices.md) and [lode/coding-standards/solid.md](lode/coding-standards/solid.md).
- **Anti-patterns, security, testing** — see [dotnet-anti-patterns.md](.claude/rules/dotnet-anti-patterns.md), [security.md](.claude/rules/security.md), [dotnet-testing.md](.claude/rules/dotnet-testing.md).

## Git Conventions

- **Commit messages**: imperative mood, concise summary
- **Never include an email address in a commit message** — no `noreply@`, no email in any trailer. Enforced by `commit.sh` and `scripts/internal/precommit.sh`. `user@example.com` placeholders are allowed.
- **Branch naming**: `feature/`, `fix/`, `refactor/`
- **Squash all local commits before push** — one clean commit per PR
- **Concurrent sessions on the same branch are expected.** Staged or added/removed files that another session left behind are normal working state, not stray. Never `git stash`/`reset`/`clean` them without checking first. Run `scripts/status.sh` before staging.
- **Commit and push as one bundle.** When shipping, include all sessions' changes together in one commit, not split by session. Use `git log` to confirm author identity.

## General Behavior

- Prefer focused incremental changes: one change, verify, then next
- When friction repeats (2+ times), fix the root cause immediately
- Prefer `.claude/` (project-level) over `~/.claude/` (global) for all config
- Use `.claude/tmp/` for working/scratch files. **Never write to `/tmp`.** Lode scraps go in `lode/tmp/`
- Never write absolute user profile paths — use `~` or repo-relative paths
- Agents and skills must not specify `model:`. All inherit the session model. See [agent-vs-skill-routing.md](.claude/rules/agent-vs-skill-routing.md)
- Use canonical scripts for repeated operations. See [deterministic-scripts.md](.claude/rules/deterministic-scripts.md) and `scripts/help.sh`
