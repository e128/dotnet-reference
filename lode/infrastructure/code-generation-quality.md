# Code Generation Quality with Claude Code
*Updated: 2026-04-09T13:49:15Z*

Mechanisms in this repo that drive better AI code generation, ranked by impact.

## 1. Analyzers as Guardrails (highest leverage)

Deny-by-default `.globalconfig` with 8 analyzer packages means Claude writes "close enough" code and `dotnet build` immediately reports what's wrong. `TreatWarningsAsErrors=true` means nothing slips through as a warning. The AI iterates against compiler output, not instructions it might forget.

## 2. CLAUDE.md — Always-Loaded Rules

Under 200 lines, loaded every turn. High-frequency rules that prevent the most common AI mistakes:
- "Never use raw `dotnet test`" — prevents broken commands
- "Implicit usings disabled" — forces explicit `using` directives
- "Assert.Fail for stubs" — consistent TDD pattern
- "Never use `!` to silence nullability" — prevents the most common bad fix

## 3. Contextual Rules (`.claude/rules/`)

Loaded only when relevant. Most impactful:
- `dotnet-anti-patterns.md` — 4 rules preventing top .NET mistakes (`DateTime.Now`, `new HttpClient()`, `async void`, sync-over-async)
- `quality-gates.md` — format before check, no analyzer suppressions
- `read-before-edit.md` + `reread-triggers.md` — prevents editing stale file contents after format
- `token-efficiency.md` — "use scripts, not raw commands" reduces wasted turns

## 4. Scripts as Correct Commands

`scripts/test.sh`, `scripts/check.sh`, `scripts/build.sh` — Claude calls one script instead of figuring out the right `dotnet test --solution ... -- --filter-trait` invocation. Scripts handle MTP syntax, solution discovery, and output parsing. Eliminates "wrong command" errors.

## 5. `.editorconfig` + `dotnet format`

Format gate means Claude's code style doesn't matter on first write — `dotnet format` normalizes it. The `.editorconfig` covers `var` usage, brace style, namespace style, naming conventions. No style debates.

## 6. Central Build Defaults (`Directory.Build.props` / `.targets`)

Claude doesn't set `TargetFramework`, `Nullable`, `OutputType`, or analyzer packages in every `.csproj` — they're inherited. New projects just need the SDK and `IsTestProject=true`. Less boilerplate = fewer places for mistakes.

## 7. Skills with Step Files

The `dotnet-overhaul` skill breaks complex tasks into discrete steps with explicit agent prompts. Each step file specifies what to search for, what severity to assign, and the output format. Structured decomposition prevents Claude from trying to do everything at once.

## 8. Conventions File

`conventions.md` explicitly states what's auto-approved vs. needs human judgment. The immutability table gives concrete conversion rules rather than vague "prefer immutability." Concrete tables outperform abstract principles for code generation.

## 9. Lode as Domain Knowledge

`lode/dotnet/testing.md` has the exact `dotnet test` syntax. `lode/dotnet/analyzers.md` explains CA2007 scoping. Claude reads these before writing code in those areas. Pre-researched answers prevent guessing.

## 10. Test-Specific Analyzer Overrides

`tests/.globalconfig` at `global_level=101` lets Claude write test code with underscores in names, without `ConfigureAwait`, without async suffixes — and the build still passes. Making test vs. production rules explicit prevents false failures.

## The Pattern

Make the right thing automatic and the wrong thing fail loudly:

1. Wrong code fails to build (analyzers)
2. Right commands are one script call away (scripts)
3. Style is normalized automatically (format)
4. Defaults are correct centrally (props/targets)
5. Domain knowledge is pre-loaded (lode + rules)

The repo is a **compiler-enforced style guide** — Claude iterates against errors, not instructions.

## Related

- [Claude Code Maintenance](claude-code-maintenance.md)
- [Analyzers](../dotnet/analyzers.md)
- [Testing](../dotnet/testing.md)
- [Practices](../practices.md)
