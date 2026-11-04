using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

/// <summary>
///     E128147: Reports the deprecated <c lang="csharp">RNGCryptoServiceProvider</c>, and any class derived from it,
///     where the target framework marks it <c lang="csharp">[Obsolete]</c>. On .NET Framework and netstandard2.0 it
///     is the supported type.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FipsDeprecatedRngAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128147";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "RNGCryptoServiceProvider is deprecated",
        "RNGCryptoServiceProvider is obsolete (SYSLIB0023); use the static RandomNumberGenerator methods, such as GetBytes or GetInt32",
        "E128-Security",
        DiagnosticSeverity.Error,
        true,
        "RNGCryptoServiceProvider is obsolete in .NET 6 and later. Use the static RandomNumberGenerator " +
        "methods, such as GetBytes, Fill and GetInt32.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (FindCryptographyBase(creation.Type, "RNGCryptoServiceProvider") is { } rng && IsObsolete(rng))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, creation.Syntax.GetLocation()));
        }
    }

    private static ITypeSymbol? FindCryptographyBase(ITypeSymbol? type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (FipsSymbolHelpers.IsCryptographyType(current, name))
            {
                return current;
            }
        }

        return null;
    }

    private static bool IsObsolete(ITypeSymbol type)
    {
        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass is { Name: "ObsoleteAttribute" } attributeClass &&
                FipsSymbolHelpers.NamespaceEquals(attributeClass.ContainingNamespace, "System"))
            {
                return true;
            }
        }

        return false;
    }
}
