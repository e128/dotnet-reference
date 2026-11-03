# Analyzer Testing

*Updated: 2026-11-03T03:13:32Z*

How a test drives an analyzer and a code fix. The harness types live in
`Microsoft.CodeAnalysis.CSharp.Testing`. The repo wrapper lives in
`tests/E128.Analyzers.Tests/CodeFixTestRunner.cs`.

Run the suite with `scripts/test.sh ClassName` or `scripts/test.sh --all`.
Never run raw `dotnet test`. See [.NET Testing](../testing.md).

## Markup Forms

The verifier reads the expected diagnostic from the source text. Each form
answers a different question.

| Form          | Use                                                  |
| ------------- | ---------------------------------------------------- |
| `[|code|]`    | An analyzer that declares exactly one descriptor      |
| `{\|E128NNN:code\|}` | A named diagnostic, for any analyzer           |
| `{\|#0:code\|}`     | A keyed location, paired with `.WithLocation(0)` |
| `$$`          | A bare position, paired with `.WithLocation(line, column)` |

`[|code|]` misbehaves on a multi-descriptor analyzer, and most E128 analyzers
declare one descriptor per rule. Write the named form
`{|E128NNN:code|}`.

Two diagnostics on one line need the full closing form `|#1}`. The short form
cannot resolve which marker closes which span.

`.WithSpan(new FileLinePositionSpan(...))` checks the span and its length.
`.WithLocation(...)` checks the start position only.

## Expected Compiler Diagnostics

An analyzer runs on a buffer that can fail to compile. A test source that does
not compile needs the compiler diagnostic marked in the source, or the verifier
reports an unexpected diagnostic:

```csharp
int x = {|CS0029:"abc"|};
```

Marking the compiler error is the test of a guard path. A rule that reads a
symbol with no binding must stay silent, and the test proves it.

## What a Code Fix Test Asserts

`VerifyCodeFixAsync` runs four checks:

1. The expected diagnostics are reported.
2. The fixed result holds no diagnostic that a fix can still repair.
3. Applying one fix at a time produces the expected output.
4. Fix All produces the expected output, when the provider offers one.

Check 4 means every code fix test exercises the fix-all provider. A provider
that returns a broken `FixAllProvider` fails a test that never mentions fix-all.

Set the fixed text equal to the input text to assert that a diagnostic is
reported with no fix offered.

Keep the analyzer and the fix in one test. A split test loses checks 3 and 4
and duplicates the source text.

## Supply a Configuration Option

`TestState.AnalyzerConfigFiles` holds a global config. Add it before the run:

```csharp
test.TestState.AnalyzerConfigFiles.Add(
    ("/.globalconfig", "is_global = true\ne128_minimum_framework_version = 100"));
```

`is_global = true` is the header that makes the file global. Without it, the
keys do not reach `GlobalOptions`.

`TestState.AdditionalFiles` takes `(filename, content)` tuples, for a rule that
reads a structured file. See [configuration.md](configuration.md).

## Roslyn Version Alignment

The testing packages depend on an older Roslyn than the analyzer may use. When a
rule uses a language feature that the pinned testing Roslyn lacks, override the
Workspaces package in the test project to match the analyzer version.

```xml
<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces"
                  Version="$(RoslynVersion)" />
```

The misalignment surfaces either as a parse failure in the test source or as a
load exception. Both read like an analyzer defect.

## Obsolete Package Surface

The framework-suffixed packages (`.MSTest`, `.NUnit`, `.XUnit`) are obsolete.
Use the generic verifiers. The migration changes three things:

- Drop the suffix from the package name.
- Import `Microsoft.CodeAnalysis.CSharp.Testing` instead of the framework
  sub-namespace.
- Add the verifier type argument:
  `CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>`.

No package declares xUnit v3 support. Treat that combination as unverified, and
confirm the test count in the run output.

## Repo Harness

- Build the test directly with `CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>`
  or `CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>`. Most test files
  call `test.RunAsync()`, not a static verifier helper.
- Route a code fix test through `CodeFixTestRunner.RunAsync` for a fix and
  `CodeFixTestRunner.NoFixAsync` for a decline.
- The shared stub types live in `tests/E128.Analyzers.Tests/TestStubs.cs`.
- `ReferenceAssemblies` default to the version in `tests/.globalconfig` through
  `e128_minimum_framework_version`.
- xUnit v3 runs on the Microsoft Testing Platform. Only `scripts/test.sh`
  works.

## Related

- [New Analyzer Checklist](new-analyzer-checklist.md): registration and gates
- [Analyzer Configuration](configuration.md): the option the test supplies
- [Code Fix Patterns](code-fix-patterns.md): the fix a test drives
- [.NET Testing](../testing.md): runner, categories, conventions
