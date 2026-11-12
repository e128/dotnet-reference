# Analyzer Configuration

*Updated: 2026-11-12T00:54:46Z*

How a rule reads a configuration value. The project-level file split lives in
[Analyzers](../analyzers.md) § Configuration Split. This file covers the
analyzer side: which API reads the value, and which key name belongs in the
config file.

## Two Option Surfaces

`AnalyzerConfigOptionsProvider` exposes three members. Choose by scope, not by
convenience.

| Member                    | Scope                            | Source                                     |
| ------------------------- | -------------------------------- | ------------------------------------------ |
| `GlobalOptions`           | One per compilation              | `.globalconfig`, `build_property.*`        |
| `GetOptions(SyntaxTree)`  | One per source file              | `.editorconfig` section for that path      |
| `GetOptions(AdditionalText)` | One per additional file       | `.editorconfig` section for that path      |

A project-wide switch belongs on `GlobalOptions`. A per-directory or
per-file-name switch belongs on `GetOptions(tree)`.

`UnregisteredServiceResolveAnalyzer` reads `e128_registered_services` from
`GlobalOptions`, because a service list applies to the whole project.
`StaleReferenceAssembliesAnalyzer` reads `e128_minimum_framework_version` from
`GetOptions(tree)`, so a path-scoped `.editorconfig` can override the project
value. The repo sets the project value in `tests/.globalconfig`.

The repo value is `100`. `Microsoft.CodeAnalysis.Analyzer.Testing` exposes
`ReferenceAssemblies.Net` codes up to `Net100`, and its latest release adds no
`Net110`. A higher default would flag the `Net100` snippets that every test
uses, and no replacement snippet version exists. Raise the value when the
package publishes a `Net110` band.

The code fix reaches the same surface through
`document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider`. Read the value
there, never from a second copy of the configuration.

## Key Naming

The analyzer owns the key text, and the compiler enforces no namespace for it.
Use a lowercase `snake_case` name with the `e128_` prefix, so a key in a shared
`.globalconfig` cannot collide with a third-party analyzer.

The compiler driver consumes only a `.severity` key. Every other key reaches
the analyzer as an opaque string, and the analyzer parses it.

## Severity Keys

Three keys exist, from narrow to broad:

```ini
dotnet_diagnostic.E128028.severity = error
dotnet_analyzer_diagnostic.category-E128-Security.severity = error
dotnet_analyzer_diagnostic.severity = error
```

The prefix differs on purpose. One rule uses `dotnet_diagnostic`. A bulk key
uses `dotnet_analyzer_diagnostic`.

Every E128 rule declares a category named `E128-<Area>`, such as `E128-Security`.
The key `category-E128-Security` selects the rules of one area and no
third-party rule. Roslyn matches the whole category string, so no one key
selects every E128 rule. Give a new rule an `E128-` category, never a CA-style
name such as `Reliability`. The areas are Design, Maintainability, Performance,
Reliability, Security, Style, Testing, and Unsafe.

Precedence runs rule ID, then category, then blanket. A rule-specific key wins
over both bulk forms.

A bulk key reaches only a rule whose default is enabled. A rule that declares
`isEnabledByDefault: false` stays silent until a rule-specific key turns it on.

## MSBuild Properties

An MSBuild property reaches an analyzer only after the build opts it in.

```xml
<ItemGroup>
  <CompilerVisibleProperty Include="RootNamespace" />
</ItemGroup>
```

The key then carries a `build_property.` prefix:

```csharp
context.Options.AnalyzerConfigOptionsProvider.GlobalOptions
    .TryGetValue("build_property.RootNamespace", out var rootNamespace);
```

The bare property name returns nothing. The prefix is the load-bearing part.

Ship the `CompilerVisibleProperty` item in a `.props` file under `build/` in the
package, so a consumer inherits it without an edit. See
[package-layout.md](package-layout.md) § NuGet Layout Rule.

A missing key and a wrong value both read as absent. Test the value with
`string.Equals(value, "true", StringComparison.Ordinal)`. Never compare a
string with `==` in analyzer code. See
[new-analyzer-checklist.md](new-analyzer-checklist.md) § Netstandard2.0
Constraints.

## Additional Files

A `.editorconfig` holds key and value pairs. A file with structure of its own
belongs in `AdditionalFiles`, and the analyzer parses it.

```xml
<ItemGroup>
  <AdditionalFiles Include="e128-services.txt" />
</ItemGroup>
```

Read the file once in `RegisterCompilationStartAction`, so the cost lands once
per compilation.

```csharp
foreach (var file in start.Options.AdditionalFiles)
{
    var text = file.GetText(start.CancellationToken);
    if (text is null)
    {
        continue;
    }

    foreach (var line in text.Lines)
    {
        if (line.Span.IsEmpty)
        {
            continue;
        }
        // Parse the line.
    }
}
```

The analyzer owns the file format. Text and JSON both work. `file.Path` names
the file when several entries exist.

The repo already uses the item for `AnalyzerReleases.Shipped.md`,
`AnalyzerReleases.Unshipped.md`, `PublicAPI.Shipped.txt`, and
`PublicAPI.Unshipped.txt`. The Roslyn release-tracking analyzer reads them. See
[release-tracking.md](../release-tracking.md).

## Generated Code Key

`generated_code = true` under a path section marks files as generated for the
code-analysis driver:

```ini
[*.MyGenerated.cs]
generated_code = true
```

The key changes the driver only. The rule still chooses its own flags. See
[new-analyzer-checklist.md](new-analyzer-checklist.md) § Generated Code.

## Suppressors Are Not a Configuration Surface

A `DiagnosticSuppressor` derives from `DiagnosticAnalyzer`, but the compiler
seals its `Initialize`, so it registers no analysis action. It also cannot
report a new diagnostic, and it cannot suppress a diagnostic whose default
severity is `Error`.

This repo ships every rule at the severity the policy demands, so a suppressor
would never apply. A consumer that needs an exemption edits `.globalconfig`.
Do not add a suppressor to answer a false positive. Fix the rule instead.

## Related

- [Analyzers](../analyzers.md): severity strategy and the file split
- [New Analyzer Checklist](new-analyzer-checklist.md): pitfalls and gates
- [Package Layout](package-layout.md): shipping a props file
- [Analyzer Testing](testing.md): supplying an option in a test
