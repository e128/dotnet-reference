using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
/// E128057: Detects cleanup calls (<c>File.Delete</c>, <c>Directory.Delete</c>) inside
/// <see langword="finally"/> blocks that are not wrapped in their own <see langword="try"/>/<see langword="catch"/>.
/// If a cleanup throws, it masks the original exception from the enclosing <see langword="try"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FinallyBlockCleanupAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128057";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Unprotected cleanup in finally block",
        messageFormat: "Cleanup call '{0}' in finally block is not wrapped in try/catch — a thrown exception will mask the original exception",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Cleanup calls inside finally blocks should be wrapped in try/catch to prevent exceptions from masking the original exception that triggered the finally.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeFinally, SyntaxKind.FinallyClause);
    }

    private static void AnalyzeFinally(SyntaxNodeAnalysisContext context)
    {
        var finallyClause = (FinallyClauseSyntax)context.Node;

        foreach (var invocation in finallyClause.Block.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (!IsDangerousCleanupCall(invocation, out var callName))
            {
                continue;
            }

            if (IsProtectedByTryCatch(invocation, finallyClause))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), callName));
        }
    }

    private static bool IsDangerousCleanupCall(InvocationExpressionSyntax invocation, out string callName)
    {
        callName = string.Empty;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        if (!string.Equals(methodName, "Delete", StringComparison.Ordinal))
        {
            return false;
        }

        if (memberAccess.Expression is not IdentifierNameSyntax receiver)
        {
            return false;
        }

        var receiverName = receiver.Identifier.ValueText;
        var isFileOrDirectory = string.Equals(receiverName, "File", StringComparison.Ordinal)
            || string.Equals(receiverName, "Directory", StringComparison.Ordinal);

        if (!isFileOrDirectory)
        {
            return false;
        }

        callName = $"{receiverName}.{methodName}";
        return true;
    }

    private static bool IsProtectedByTryCatch(SyntaxNode node, FinallyClauseSyntax boundary)
    {
        var current = node.Parent;
        while (current is not null && current != boundary)
        {
            if (current is TryStatementSyntax tryStatement && tryStatement.Catches.Any())
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }
}
