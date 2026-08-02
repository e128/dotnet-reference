# .NET Testing

Defect-prevention rules for test code. Each rule is negative-form because it
blocks a real bug class.

## Never Use Reflection in Tests by Default

Expose test-only surface with `internal` plus `[InternalsVisibleTo]`.
Reflection-based test access is fragile. It breaks under renaming and it
bypasses the type system's access checks.

When reflection is genuinely required, explain why in a comment. One example
is a sealed method that cannot become `internal`.

## Never Run Raw `dotnet test`

The stack is xUnit v3 on MTP. Raw `dotnet test --filter` does not work. Run
`scripts/test.sh ClassName` or `scripts/test.sh --all`.

## Before You Change `<TargetFramework>`

Run `dotnet --list-sdks` and confirm the target SDK is installed locally. CI
runs on `ubuntu-24.04` with a pinned SDK. A target that is missing locally
fails at build time in unexpected ways.

## See Also

- [quality-gates.md](quality-gates.md)
- [deterministic-scripts.md](deterministic-scripts.md)
