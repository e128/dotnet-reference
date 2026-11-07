using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DbCommandInLoopAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128204";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Database command inside a loop runs one round trip per iteration",
        "'{0}' inside a loop runs one database round trip per iteration — batch the rows into one command",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true);

    private static readonly ImmutableHashSet<string> ExecuteMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ExecuteNonQueryAsync",
        "ExecuteScalarAsync",
        "ExecuteReaderAsync");

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
            var dbCommand = startContext.Compilation.GetTypeByMetadataName("System.Data.Common.DbCommand");
            if (dbCommand is null)
            {
                return;
            }

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, dbCommand),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol dbCommand)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return;
        }

        var methodName = access.Name.Identifier.ValueText;
        if (!ExecuteMethods.Contains(methodName))
        {
            return;
        }

        var body = LoopContext.FindEnclosingLoopBody(invocation);
        if (body is null || LoopContext.HasConstantBound(invocation))
        {
            return;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type;
        if (!TypeSymbols.DerivesFrom(receiverType, dbCommand))
        {
            return;
        }

        if (LoopContext.ContainsWaitCall(body, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), methodName));
    }
}
