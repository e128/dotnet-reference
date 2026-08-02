# Project Instructions for Claude

*Last updated: 2026-08-02*

High-frequency rules only. Detail lives in `.claude/rules/`, `lode/`, and
skills. Load detail on demand.

## Communication

- Write terse responses. Give code, not explanation, unless the user asks.
- Never give a time estimate.
- Put every decision through `AskUserQuestion`. Never ask inline.
- Write every timestamp in ISO 8601 UTC (`2026-04-09T12:00:00Z`).
- Write every artifact in Simplified Technical English. See
  [writing-style.md](.claude/rules/writing-style.md).

Full AI assistant preferences: [lode/practices.md](lode/practices.md).

## Workflow

- **Tests** — never run raw `dotnet test`. Run `scripts/test.sh ClassName` or
  `scripts/test.sh --all`. The stack is xUnit v3 on MTP. See
  [deterministic-scripts.md](.claude/rules/deterministic-scripts.md).
- **TDD** — follow Red-Green-Refactor. A RED-phase stub calls
  `Assert.Fail(message)`. Never use `Assert.True(false)` or
  `NotImplementedException`. See [quality-gates.md](.claude/rules/quality-gates.md).
- **Build** — `scripts/check.sh` is the single format, build, and test gate.
  Build and test scripts run without an ask-gate.
- **Lode** — `lode/` is the authoritative memory store. Write all project
  knowledge to `lode/`, never to `MEMORY.md`. See
  [lode/lode-map.md](lode/lode-map.md).

## .NET Development

- **Implicit usings are disabled** (`<ImplicitUsings>disable</ImplicitUsings>`).
  Give every `.cs` file explicit `using` directives.
- **Seal by default.** Every non-abstract class is sealed. E128005 and
  ArchUnitNET enforce this.
- **Design order** — Immutability > Memory > CPU > Parallelism. See
  [lode/practices.md](lode/practices.md) and
  [lode/coding-standards/solid.md](lode/coding-standards/solid.md).
- **Anti-patterns, security, testing** — see
  [dotnet-anti-patterns.md](.claude/rules/dotnet-anti-patterns.md),
  [security.md](.claude/rules/security.md),
  [dotnet-testing.md](.claude/rules/dotnet-testing.md).

## Git Conventions

- Write commit messages in the imperative mood with a concise summary.
- **Never put an email address in a commit message.** No `noreply@` address.
  No email in any trailer. `commit.sh` and `scripts/internal/precommit.sh`
  enforce this. A `user@example.com` placeholder is allowed.
- Name branches `feature/`, `fix/`, or `refactor/`.
- **Squash all local commits before you push.** Ship one clean commit per PR.
- **Expect concurrent sessions on the same branch.** Files that another session
  staged, added, or removed are normal working state, not stray files. Never run
  `git stash`, `git reset`, or `git clean` on them without asking first. Run
  `scripts/status.sh` before you stage.
- **Commit and push as one bundle.** Include every session's changes in one
  commit. Run `git log` to confirm the author identity.

## General Behavior

- Make one focused change, verify it, then make the next.
- Fix the root cause the second time a friction point repeats.
- Put config in `.claude/` (project level), not `~/.claude/` (global).
- Write working files to `.claude/tmp/`. **Never write to `/tmp`.** Lode scraps
  go in `lode/tmp/`.
- Never write an absolute user profile path. Use `~` or a repo-relative path.
- Never set `model:` in an agent or a skill. All inherit the session model. See
  [agent-vs-skill-routing.md](.claude/rules/agent-vs-skill-routing.md).
- Route every repeated operation through a canonical script. See
  [deterministic-scripts.md](.claude/rules/deterministic-scripts.md) and
  `scripts/help.sh`.
