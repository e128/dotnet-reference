using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InsecureRandomInCryptoContextAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128075";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use RandomNumberGenerator instead of Random in security-adjacent code",
        "Non-cryptographic Random detected in a file that uses System.Security.Cryptography — use RandomNumberGenerator instead",
        "Security",
        DiagnosticSeverity.Warning,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;

        var typeInfo = context.SemanticModel.GetTypeInfo(creation, context.CancellationToken);
        if (!IsSystemRandom(typeInfo.Type))
        {
            return;
        }

        if (!HasCryptoUsingDirective(context))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, creation.GetLocation()));
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        if (!string.Equals(memberAccess.Name.Identifier.ValueText, "Shared", StringComparison.Ordinal))
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken);
        if (symbolInfo.Symbol is not IPropertySymbol property)
        {
            return;
        }

        if (!IsSystemRandom(property.ContainingType))
        {
            return;
        }

        if (!HasCryptoUsingDirective(context))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation()));
    }

    private static bool IsSystemRandom(ITypeSymbol? type)
    {
        return type is not null
               && string.Equals(type.Name, "Random", StringComparison.Ordinal)
               && string.Equals(type.ContainingNamespace?.ToDisplayString(), "System", StringComparison.Ordinal);
    }

    private static bool HasCryptoUsingDirective(SyntaxNodeAnalysisContext context)
    {
        var root = context.Node.SyntaxTree.GetRoot(context.CancellationToken);

        return root is CompilationUnitSyntax compilationUnit
               && compilationUnit.Usings.Any(u =>
                   string.Equals(u.Name?.ToString(), "System.Security.Cryptography", StringComparison.Ordinal));
    }
}
