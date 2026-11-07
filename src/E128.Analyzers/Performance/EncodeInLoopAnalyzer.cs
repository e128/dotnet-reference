using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EncodeInLoopAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128209";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Byte count computed through an allocation",
        "'GetBytes' inside a loop allocates a byte array only to read its length — use 'GetByteCount' instead",
        "E128-Performance",
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

        context.RegisterCompilationStartAction(startContext =>
        {
            var encoding = startContext.Compilation.GetTypeByMetadataName("System.Text.Encoding");
            if (encoding is null)
            {
                return;
            }

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, encoding),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol encoding)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || !string.Equals(access.Name.Identifier.ValueText, "GetBytes", StringComparison.Ordinal))
        {
            return;
        }

        if (invocation.Parent is not MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: "Length"
            })
        {
            return;
        }

        if (LoopContext.FindEnclosingLoopBody(invocation) is null)
        {
            return;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type;
        if (!TypeSymbols.DerivesFrom(receiverType, encoding))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }
}
