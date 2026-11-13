using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128219: Reports an abstract class or record base that declares no members while every
///     type that derives from it lives in the same assembly. The <c lang="csharp">closed</c> modifier names
///     that shape, and a switch over the closed hierarchy then fails the build on a missing
///     case. A public base is never reported, because an assembly outside this compilation
///     may derive from it and a closed modifier would break that derivation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MemberlessAbstractBaseAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128219";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Memberless abstract base is a closed hierarchy",
        "Abstract base '{0}' declares no members and all derivation stays in this assembly — declare it closed instead of abstract so a switch over the hierarchy is exhaustive",
        "E128-Design",
        DiagnosticSeverity.Warning,
        true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        ClosedHierarchy.Register(context, Rule, memberless: true);
    }
}
