# Decision Records

*Updated: 2026-11-20T17:16:51Z*

Source files in `src/` carry no line comments. `scripts/comment-scan.sh` enforces that rule. A fact that cannot be read from the code belongs here, not in a comment.

Each page groups the decisions of one area. A `##` heading names the type that carried the original comment. A bullet names one constraint and the reason it holds.

## Pages

- [analyzer-reliability.md](analyzer-reliability.md): Reliability rule detection limits and Roslyn behavior.
- [analyzer-security.md](analyzer-security.md): Security, unsafe, and file-system rule decisions.
- [analyzer-performance.md](analyzer-performance.md): Performance rule gates and rewrite safety.
- [analyzer-testing.md](analyzer-testing.md): Test-code rule heuristics.
- [analyzer-design.md](analyzer-design.md): Design and maintainability rule shapes.
- [analyzer-style.md](analyzer-style.md): Style rule decisions.
- [analyzer-helpers.md](analyzer-helpers.md): Shared analyzer helpers and union types.
- [code-fixers-design.md](code-fixers-design.md): Design code fix providers.
- [code-fixers-reliability.md](code-fixers-reliability.md): Reliability code fix providers.
- [code-fixers-performance.md](code-fixers-performance.md): Performance code fix providers.
- [code-fixers-style.md](code-fixers-style.md): Style code fix providers.
- [code-fixers-other.md](code-fixers-other.md): Testing, security, unsafe, and file-system fixes.
- [code-fixers-helpers.md](code-fixers-helpers.md): Shared fixer syntax helpers.
- [reference.md](reference.md): Reference CLI, core, and web app decisions.

## Add a Decision

1. Confirm the constraint against the code.
2. Add a bullet under the type heading, or add a heading for a new type.
3. State the constraint and the reason. Keep the sentence under 25 words.
4. Run `scripts/lode-check.sh <file>`.

`scripts/comment-scan.sh` enforces the ban and runs inside `scripts/check.sh`. The `/delete-comment` skill repeats the migration on another tree. Analyzer authoring rules live in [Analyzer Authoring](../dotnet/analyzers/authoring.md).
