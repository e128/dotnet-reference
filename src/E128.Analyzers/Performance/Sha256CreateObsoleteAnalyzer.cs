using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sha256CreateObsoleteAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128072";

    private const string Sha256MetadataName = "System.Security.Cryptography.SHA256";
    private const string HashDataMethodName = "HashData";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Prefer SHA256.HashData() over SHA256.Create()",
        "SHA256.Create() allocates — use SHA256.HashData() for single-shot hashing or SHA256.HashData(Stream) for streaming",
        "Performance",
        DiagnosticSeverity.Info,
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

        context.RegisterCompilationStartAction(startContext =>
        {
            var sha256Type = startContext.Compilation.GetTypeByMetadataName(Sha256MetadataName);

            // HashData arrived in .NET 5. Without it the suggestion has no remedy that compiles,
            // so an older target framework must get no report.
            if (sha256Type is null || !HasHashData(sha256Type))
            {
                return;
            }

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, sha256Type),
                SyntaxKind.InvocationExpression);
        });
    }

    private static bool HasHashData(INamedTypeSymbol sha256Type)
    {
        foreach (var member in sha256Type.GetMembers(HashDataMethodName))
        {
            if (member is IMethodSymbol)
            {
                return true;
            }
        }

        return false;
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol sha256Type)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (!string.Equals(memberAccess.Name.Identifier.ValueText, "Create", StringComparison.Ordinal))
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, sha256Type))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.GetLocation()));
    }
}
