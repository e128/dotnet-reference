# AGENTS.md

Cross-harness instructions for any AI coding agent (Claude Code, Codex CLI,
Cursor, Aider, or another tool). This file is the portable source of truth.
Claude Code loads it through an import in `CLAUDE.md` and layers Claude-only
extensions on top. See `CLAUDE.md` for that extra layer, and
`lode/infrastructure/claude-code-maintenance.md` for the full capability map
of what is portable versus Claude-only.

## Communication

- Write terse responses. Give code, not explanation, unless asked.
- Never give a time estimate.
- Ask a question only when a decision is materially ambiguous, risky, or
  needs approval. Otherwise pick the sensible default and state the choice.
- Write every timestamp in ISO 8601 UTC (`2026-04-09T12:00:00Z`).
- Write every artifact (docs, READMEs, PR bodies, commit bodies, code
  comments, `lode/` files) in Simplified Technical English: active voice,
  one name per thing, no semicolons, no contractions, sentences under
  20-25 words. See `lode/practices.md`.

## Workflow

- **Tests** — never run raw `dotnet test`. Run `scripts/test.sh ClassName`
  or `scripts/test.sh --all`. The stack is xUnit v3 on Microsoft Testing
  Platform (MTP).
- **TDD** — follow Red-Green-Refactor. A RED-phase stub calls
  `Assert.Fail(message)`. Never use `Assert.True(false)` or
  `NotImplementedException`.
- **Build** — `scripts/check.sh` is the single format, build, and test gate.
- **Route every repeated operation through a script in `scripts/`.** Never
  run the raw command when a script exists. Run `scripts/help.sh` for the
  full list. The full routing table lives in
  `.claude/rules/deterministic-scripts.md`. Claude Code loads that file
  automatically. On another harness, open it directly, the table still
  applies since it only names bash scripts.
- **Lode** — `lode/` is the authoritative project memory, not this file or
  any harness config. Read `lode/lode-map.md`, `lode/terminology.md`, and
  `lode/summary.md` at the start of a session. Update the matching lode
  file the same turn code, config, or structure changes. Never defer.
  Remove lode content the same turn the feature it describes disappears.
  The full Lode Coding methodology lives in `prompts/SystemPrompt.txt`. On
  Claude Code, `scripts/lode.nu` (or the `.ps1` variant, or the legacy
  `scripts/internal/lode.sh`) launches a session with that file injected as
  the system prompt. On a harness
  without a system-prompt-injection flag, read `prompts/SystemPrompt.txt`
  directly at session start. Its rules apply regardless of launch method.

## .NET Development

- Implicit usings are disabled (`<ImplicitUsings>disable</ImplicitUsings>`).
  Give every `.cs` file explicit `using` directives.
- Seal by default. Every non-abstract class is sealed. E128005 and
  ArchUnitNET enforce this.
- Design order: Immutability > Memory > CPU > Parallelism. See
  `lode/practices.md` and `lode/coding-standards/solid.md`.
- Never write these. Each compiles cleanly and produces incorrect or
  fragile code:
  - `DateTime.Now` or `DateTime.UtcNow` directly. Inject `TimeProvider`
    through DI instead.
  - `new HttpClient()`. Use `IHttpClientFactory` instead.
  - `async void` outside an event handler. Use `async Task` instead.
  - `.Result` or `.GetAwaiter().GetResult()`. Await throughout instead.
- Never hardcode a URL, secret, cryptographic key, connection string, or
  CORS allowlist in source. Expose the value through a strongly-typed
  options class bound to configuration.
- In tests, expose test-only surface with `internal` plus
  `[InternalsVisibleTo]`. Use reflection only when genuinely required, and
  explain why in a comment.
- The format gate: `dotnet format` must pass before a commit. Run
  `scripts/format.sh` to apply fixes, `scripts/format.sh --check` to
  verify.
- Never use `#pragma warning disable`, `[SuppressMessage]`, or an
  editorconfig severity downgrade without explicit user approval first.
- Never use the null-forgiving operator (`!`) to silence CS8600 through
  CS8604.

## Git Conventions

- Write commit messages in the imperative mood with a concise summary.
- Never put an email address in a commit message. No `noreply@` address.
  No email in any trailer. A `user@example.com` placeholder is allowed.
- Name branches `feature/`, `fix/`, or `refactor/`.
- Squash all local commits before push. Ship one clean commit per PR.
- Run `scripts/status.sh` before staging. A file staged, added, or removed
  by a concurrent session is normal working state, not a stray file.

## General Behavior

- Make one focused change, verify it, then make the next.
- Fix the root cause the second time a friction point repeats.
- Keep a change focused and simple. Avoid an unrelated edit, an
  unnecessary abstraction, and a low-signal test.
- Test observable behavior. Validate user-facing work in the real
  interface when applicable.
- Keep unrelated work intact. Never take a destructive, production, or
  external action beyond what was authorized.
- Ground research in authoritative, current sources. Link important
  evidence.

## Repo-Specific Gotchas

- macOS is case-insensitive. Linux CI (`ubuntu-24.04`, ext4) is
  case-sensitive. Use the exact filesystem casing in every path, `using`
  directive, and solution folder name. After a rename, search for the
  stale name.
- Write no inline Python. Fetch a URL with a proper fetch capability,
  parse JSON with `jq`, and process local data with a `scripts/*.sh`
  entry.

## What This File Does Not Cover

Claude Code loads three more layers that have no equivalent on another
harness: `.claude/rules/*.md` (always-loaded domain rules), `.claude/skills/`
and `.claude/agents/` (Claude-only skills and subagents), and
`.claude/hooks/` (Claude-only automation hooks). On another harness,
`scripts/`, `lode/`, and this file are the full toolkit. See
`lode/infrastructure/claude-code-maintenance.md` for the capability map.
