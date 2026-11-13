using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128225: Reports an internal abstract class that declares members while every type that
///     derives from it lives in the same assembly. The <c lang="csharp">closed</c> modifier names
///     that shape, and the compiler then reports CS8509 on a switch that misses a leaf. The
///     modifier implies abstract, so a base that declares abstract members keeps them.
/// </summary>
/// <remarks>
///     E128219 owns the memberless base. A public base stays silent, because an assembly outside
///     this compilation may derive from it. A record declaration stays silent, because the
///     compiler synthesizes members from the parameter list and from the equality contract.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AbstractBaseWithMembersAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128225";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Abstract base with members is a closed hierarchy",
        "Abstract base '{0}' declares members and all derivation stays in this assembly. Declare it closed so a switch over the hierarchy is exhaustive.",
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

        ClosedHierarchy.Register(context, Rule, memberless: false);
    }
}
