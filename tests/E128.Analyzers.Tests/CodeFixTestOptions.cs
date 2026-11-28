using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

namespace E128.Analyzers.Tests;

/// <summary>
///     The non-default settings for one code fix test run. Every value is optional.
/// </summary>
internal sealed record CodeFixTestOptions
{
    internal int? FixAllIterations { get; init; }

    internal int? FixAllInDocumentIterations { get; init; }

    internal IEnumerable<DiagnosticResult>? FixedExpected { get; init; }

    internal IEnumerable<string>? Sources { get; init; }

    internal LanguageVersion? LanguageVersion { get; init; }

    internal IEnumerable<string>? SuppressedDiagnosticIds { get; init; }

    internal CompilerDiagnostics? CompilerDiagnostics { get; init; }

    internal TestBehaviors TestBehaviors { get; init; }

    internal string? CodeActionEquivalenceKey { get; init; }

    internal int? CodeActionIndex { get; init; }

    internal bool? AllowUnsafe { get; init; }

    internal (string Name, string Content)? AnalyzerConfig { get; init; }

    internal OutputKind? OutputKind { get; init; }
}
