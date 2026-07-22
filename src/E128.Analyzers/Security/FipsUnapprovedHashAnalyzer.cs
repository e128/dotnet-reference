using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FipsUnapprovedHashAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128071";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use a FIPS-approved hash algorithm",
        "'{0}' is not FIPS 140-2 approved — use a SHA256/SHA384/SHA512-based equivalent",
        "Security",
        DiagnosticSeverity.Warning,
        true);

    private static readonly ImmutableHashSet<string> UnapprovedTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "MD5",
        "SHA1",
        "DES",
        "RC2",
        "TripleDES",
        "HMACMD5",
        "HMACSHA1");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        if (!string.Equals(methodName, "Create", StringComparison.Ordinal)
            && !string.Equals(methodName, "HashData", StringComparison.Ordinal))
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        var typeName = method.ContainingType?.Name;
        if (typeName is null || !UnapprovedTypes.Contains(typeName))
        {
            return;
        }

        var containingNamespace = method.ContainingType?.ContainingNamespace?.ToDisplayString();
        if (!string.Equals(containingNamespace, "System.Security.Cryptography", StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            $"{typeName}.{methodName}()"));
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;

        var symbolInfo = context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor } constructor)
        {
            return;
        }

        var constructedTypeName = constructor.ContainingType?.Name;
        if (constructedTypeName is null || !DerivesFromUnapprovedAlgorithm(constructor.ContainingType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            creation.GetLocation(),
            $"new {constructedTypeName}()"));
    }

    /// <summary>
    ///     Walks the base-type chain so legacy concrete subclasses (e.g. <c>MD5CryptoServiceProvider</c>,
    ///     <c>SHA1Managed</c>) are caught even though their own type name isn't in <see cref="UnapprovedTypes" />.
    /// </summary>
    private static bool DerivesFromUnapprovedAlgorithm(ITypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (UnapprovedTypes.Contains(current.Name)
                && string.Equals(
                    current.ContainingNamespace?.ToDisplayString(),
                    "System.Security.Cryptography",
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
