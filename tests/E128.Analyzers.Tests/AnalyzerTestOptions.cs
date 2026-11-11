using System.Collections.Generic;
using Microsoft.CodeAnalysis.Testing;

namespace E128.Analyzers.Tests;

/// <summary>
///     The non-default settings for one analyzer test run. Every value is optional.
/// </summary>
internal sealed record AnalyzerTestOptions
{
    internal ReferenceAssemblies? References { get; init; }

    internal MarkupOptions MarkupOptions { get; init; }

    internal (string Name, string Content)? AnalyzerConfig { get; init; }

    internal CompilerDiagnostics? CompilerDiagnostics { get; init; }

    internal TestBehaviors TestBehaviors { get; init; }

    internal bool AllowUnsafe { get; init; }

    internal IEnumerable<(string Name, string Content)>? AdditionalFiles { get; init; }

    internal IEnumerable<string>? Sources { get; init; }
}
