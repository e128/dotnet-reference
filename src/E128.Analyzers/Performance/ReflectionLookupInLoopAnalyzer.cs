using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReflectionLookupInLoopAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128207";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Reflection lookup inside a loop",
        "'{0}' inside a loop resolves reflection metadata per iteration — hoist the call before the loop",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true);

    private static readonly ImmutableHashSet<string> LookupMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "GetProperty",
        "GetMethod",
        "GetField",
        "GetMember",
        "GetConstructor",
        "GetEvent",
        "GetNestedType",
        "GetInterface");

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
            MetadataTypes.RegisterForType(
                startContext,
                "System.Type",
                AnalyzeInvocation,
                SyntaxKind.InvocationExpression));
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol typeType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return;
        }

        var methodName = access.Name.Identifier.ValueText;
        if (!LookupMethods.Contains(methodName))
        {
            return;
        }

        var body = LoopContext.FindEnclosingLoopBody(invocation);
        if (body is null || LoopContext.HasConstantBound(invocation))
        {
            return;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type;
        if (!TypeSymbols.DerivesFrom(receiverType, typeType))
        {
            return;
        }

        if (LoopContext.ReferencesLoopVariable(invocation, body, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), methodName));
    }
}
