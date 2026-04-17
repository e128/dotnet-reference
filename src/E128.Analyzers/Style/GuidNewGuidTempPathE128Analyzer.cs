using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Style;

/// <summary>
///     E128025: Detects <c>Guid.NewGuid()</c> usage inside string interpolation
///     combined with <c>Path.Combine</c> or <c>Path.GetTempPath()</c> context.
///     Project standard is <c>Path.GetRandomFileName()</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GuidNewGuidTempPathE128Analyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128025";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Guid.NewGuid() used in temp file path — use Path.GetRandomFileName()",
        "Use Path.GetRandomFileName() instead of Guid.NewGuid() for temp file names",
        "Style",
        DiagnosticSeverity.Warning,
        true,
        "Guid.NewGuid() in temp file path construction should be replaced with " +
        "Path.GetRandomFileName() per project conventions. Use Path.ChangeExtension() " +
        "for cases that need a specific file extension.");

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
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!IsGuidNewGuid(invocation))
        {
            return;
        }

        if (!IsInsidePathCombineInterpolation(invocation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool IsGuidNewGuid(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess
               && string.Equals(memberAccess.Name.Identifier.Text, "NewGuid", StringComparison.Ordinal)
               && memberAccess.Expression is IdentifierNameSyntax identifier
               && string.Equals(identifier.Identifier.Text, "Guid", StringComparison.Ordinal);
    }

    private static bool IsInsidePathCombineInterpolation(SyntaxNode node)
    {
        var current = node.Parent;
        var foundInterpolation = false;

        while (current is not null)
        {
            if (current is InterpolationSyntax)
            {
                foundInterpolation = true;
            }

            if (foundInterpolation && current is ArgumentSyntax)
            {
                var argList = current.Parent;
                if (argList?.Parent is InvocationExpressionSyntax outerInvocation)
                {
                    return IsPathCombineOrGetTempPath(outerInvocation);
                }
            }

            current = current.Parent;
        }

        return false;
    }

    private static bool IsPathCombineOrGetTempPath(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var methodName = memberAccess.Name.Identifier.Text;
        return memberAccess.Expression is IdentifierNameSyntax receiver
               && string.Equals(receiver.Identifier.Text, "Path", StringComparison.Ordinal)
               && (string.Equals(methodName, "Combine", StringComparison.Ordinal)
                   || string.Equals(methodName, "GetTempPath", StringComparison.Ordinal));
    }
}
