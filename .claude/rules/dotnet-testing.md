# .NET Testing

Defect-prevention rules for test code. Kept negative-form because each
blocks a real bug class.

## Never Use Reflection in Tests by Default

Use `internal` + `[InternalsVisibleTo]` to expose test-only surface.
Reflection-based test access is fragile, breaks under renaming, and
bypasses the type system's access checks.

When reflection is genuinely required (e.g., testing a sealed method
that cannot be made `internal`), document why in a comment.

## Before Modifying `TargetFramework`

Run `dotnet --list-sdks` and confirm the target SDK is installed locally
before changing `<TargetFramework>` in a `.csproj`. CI runs on
`ubuntu-24.04` with a pinned SDK; a target not installed locally will
silently fail at build time in unexpected ways.

## Cross-Reference

- [quality-gates.md](quality-gates.md) — format gate, analyzer suppressions
- [deterministic-scripts.md](deterministic-scripts.md) — `scripts/test.sh` for
  running tests (xUnit v3 MTP; raw `dotnet test --filter` does not work)
