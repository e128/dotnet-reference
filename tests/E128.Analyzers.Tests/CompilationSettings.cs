using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace E128.Analyzers.Tests;

/// <summary>
///     Solution transform shared by the analyzer and code fix runners. One place applies the
///     language version and the diagnostic suppressions that a rule test needs.
/// </summary>
internal static class CompilationSettings
{
    internal static Solution Apply(
        Solution solution,
        ProjectId projectId,
        LanguageVersion? languageVersion,
        IEnumerable<string>? suppressedDiagnosticIds)
    {
        var project = solution.GetProject(projectId);
        if (project?.ParseOptions is not CSharpParseOptions parseOptions)
        {
            return solution;
        }

        if (languageVersion is { } version)
        {
            solution = solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(version));
        }

        if (suppressedDiagnosticIds is null
            || project.CompilationOptions is not CSharpCompilationOptions compilationOptions)
        {
            return solution;
        }

        var suppressions = compilationOptions.SpecificDiagnosticOptions;
        foreach (var id in suppressedDiagnosticIds)
        {
            suppressions = suppressions.Add(id, ReportDiagnostic.Suppress);
        }

        return solution.WithProjectCompilationOptions(
            projectId,
            compilationOptions.WithSpecificDiagnosticOptions(suppressions));
    }
}
