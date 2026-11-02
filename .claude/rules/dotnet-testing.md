# .NET Testing

Defect-prevention rules for test code. Each rule is negative-form because it
blocks a real bug class.

## Never Use Reflection in Tests by Default

Expose test-only surface with `internal` plus `[InternalsVisibleTo]`.
Reflection-based test access is fragile. It breaks under renaming and it
bypasses the type system's access checks.

When reflection is genuinely required, explain why in a comment. One example
is a sealed method that cannot become `internal`.

## Confirm the Tests Ran

Read the test count in the `scripts/test.sh` output. A passing exit code with a
zero test count proves nothing. Run the class or the assembly again when the
count looks wrong.

## Never Leave a Test Disabled

Do not finish a change with a test commented out or skipped. A skip that already
existed before the change stays. Name it in the report.

## Prefer an Existing Test Class

Add a test method to the class that already covers the behavior. Create a file
only for a new unit under test.

## Never Run Raw `dotnet test`

The stack is xUnit v3 on MTP. Raw `dotnet test --filter` does not work. Run
`scripts/test.sh ClassName` or `scripts/test.sh --all`.

Coverage collection is the one exception. It needs a runsettings file that this
repo does not ship.

## Before You Change `<TargetFramework>`

Run `dotnet --list-sdks` and confirm the target SDK is installed locally. CI
runs on `ubuntu-26.04` with a pinned SDK. A target that is missing locally
fails at build time in unexpected ways.

## See Also

- [quality-gates.md](quality-gates.md)
- [deterministic-scripts.md](deterministic-scripts.md)
