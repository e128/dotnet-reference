using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

namespace E128.Analyzers.Tests;

/// <summary>
///     The non-default settings for one code fix test run. Every value is optional.
/// </summary>
internal sealed record CodeFixTestOptions
{
    internal int? FixAllIterations { get; init; }

    internal IEnumerable<DiagnosticResult>? FixedExpected { get; init; }

    internal IEnumerable<string>? Sources { get; init; }

    internal LanguageVersion? LanguageVersion { get; init; }

    internal IEnumerable<string>? SuppressedDiagnosticIds { get; init; }
}
