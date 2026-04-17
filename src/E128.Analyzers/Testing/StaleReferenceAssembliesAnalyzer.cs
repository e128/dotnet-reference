using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Testing;

/// <summary>
///     E128062: Flags <c>ReferenceAssemblies.Net.Net80</c> / <c>Net90</c> usages in test code
///     when the configured minimum framework version is higher. Tests that use older reference
///     assemblies may miss API availability issues specific to the production target framework.
///     Configurable via <c>e128_minimum_framework_version</c> in <c>.globalconfig</c> (default: 100).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StaleReferenceAssembliesAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128062";
    internal const string MinimumVersionOptionKey = "e128_minimum_framework_version";

    /// <summary>
    ///     Default minimum framework version: 100 = net10.0.
    /// </summary>
    private const int DefaultMinimumVersion = 100;

    private static readonly ImmutableDictionary<string, int> KnownVersions =
        ImmutableDictionary.CreateRange(StringComparer.Ordinal,
            [
                new KeyValuePair<string, int>("Net60", 60),
                new KeyValuePair<string, int>("Net70", 70),
                new KeyValuePair<string, int>("Net80", 80),
                new KeyValuePair<string, int>("Net90", 90),
                new KeyValuePair<string, int>("Net100", 100)
            ]);

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Test uses outdated ReferenceAssemblies — does not match project target framework",
        "ReferenceAssemblies.Net.{0} is outdated — use Net{1} to match the project target framework",
        "Testing",
        DiagnosticSeverity.Warning,
        true,
        "Tests using older ReferenceAssemblies (e.g., Net80 when the project targets net10.0) " +
        "may miss API availability issues. Set e128_minimum_framework_version in .globalconfig to match the project TFM.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        if (!TryMatchPattern(memberAccess, out var versionName, out var versionNumber))
        {
            return;
        }

        // Semantic check: verify it resolves to the Microsoft.CodeAnalysis.Testing type
        if (!IsReferenceAssembliesNetProperty(memberAccess, context))
        {
            return;
        }

        var minimumVersion = GetMinimumVersion(context);
        if (versionNumber >= minimumVersion)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            memberAccess.GetLocation(),
            ImmutableDictionary.CreateRange(
                [
                    new KeyValuePair<string, string?>(MinimumVersionOptionKey, minimumVersion.ToString(CultureInfo.InvariantCulture))
                ]),
            versionName,
            minimumVersion));
    }

    private static bool TryMatchPattern(
        MemberAccessExpressionSyntax memberAccess,
        out string versionName,
        out int versionNumber)
    {
        versionName = "";
        versionNumber = 0;

        // Pattern: ReferenceAssemblies.Net.NetXX
        if (memberAccess.Name is not IdentifierNameSyntax versionIdentifier)
        {
            return false;
        }

        versionName = versionIdentifier.Identifier.ValueText;
        if (!KnownVersions.TryGetValue(versionName, out versionNumber))
        {
            return false;
        }

        // Check: the middle part is "Net"
        if (memberAccess.Expression is not MemberAccessExpressionSyntax netAccess ||
            !string.Equals(netAccess.Name.Identifier.ValueText, "Net", StringComparison.Ordinal))
        {
            return false;
        }

        // Check: the leftmost part is "ReferenceAssemblies"
        return netAccess.Expression is IdentifierNameSyntax refAssembliesIdentifier
               && string.Equals(refAssembliesIdentifier.Identifier.ValueText, "ReferenceAssemblies", StringComparison.Ordinal);
    }

    private static bool IsReferenceAssembliesNetProperty(
        MemberAccessExpressionSyntax memberAccess,
        SyntaxNodeAnalysisContext context)
    {
        var symbol = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol;
        return symbol is IPropertySymbol propertySymbol
               && propertySymbol.ContainingType is not null
               && string.Equals(propertySymbol.ContainingType.Name, "Net", StringComparison.Ordinal)
               && propertySymbol.ContainingType.ContainingType is not null
               && string.Equals(propertySymbol.ContainingType.ContainingType.Name, "ReferenceAssemblies", StringComparison.Ordinal);
    }

    internal static int GetMinimumVersion(SyntaxNodeAnalysisContext context)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);

        return options.TryGetValue(MinimumVersionOptionKey, out var rawValue)
               && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version)
               && version > 0
            ? version
            : DefaultMinimumVersion;
    }
}
