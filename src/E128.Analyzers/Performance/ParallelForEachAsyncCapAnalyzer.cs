using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ParallelForEachAsyncCapAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128187";

    private const string ParallelMetadataName = "System.Threading.Tasks.Parallel";
    private const string ParallelOptionsMetadataName = "System.Threading.Tasks.ParallelOptions";
    private const string ForEachAsyncMethodName = "ForEachAsync";
    private const string MaxDegreeOfParallelismMemberName = "MaxDegreeOfParallelism";

    private const int MaxResolveDepth = 5;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Parallel.ForEachAsync without a degree cap",
        "'Parallel.ForEachAsync' caps at the processor count because the call sets no 'MaxDegreeOfParallelism'",
        "E128-Performance",
        DiagnosticSeverity.Info,
        true,
        "Parallel.ForEachAsync runs at most Environment.ProcessorCount operations when " +
        "MaxDegreeOfParallelism is unset. An I/O-bound body can use a larger or smaller cap, so " +
        "set MaxDegreeOfParallelism to the value the workload needs.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationContext =>
            _ = MetadataTypes.TryRegisterForTypes(
                compilationContext,
                [ParallelMetadataName, ParallelOptionsMetadataName],
                static (nodeContext, types) => AnalyzeInvocation(nodeContext, types[0], types[1]),
                SyntaxKind.InvocationExpression));
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol parallelType,
        INamedTypeSymbol parallelOptionsType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (GetForEachAsyncMethod(context, invocation, parallelType) is not { } method)
        {
            return;
        }

        var optionsArgument = GetOptionsArgument(invocation, method, parallelOptionsType);

        if (optionsArgument is null)
        {
            Report(context, invocation);
            return;
        }

        ReportIfUncapped(context, invocation, optionsArgument, parallelOptionsType);
    }

    private static void ReportIfUncapped(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax optionsArgument,
        INamedTypeSymbol parallelOptionsType)
    {
        var resolved = ResolveOptionsExpression(optionsArgument, context, depth: 0);

        if (resolved is null)
        {
            return;
        }

        if (IsDefaultOptions(resolved))
        {
            Report(context, invocation);
            return;
        }

        if (resolved is not ObjectCreationExpressionSyntax creation
            || context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type is not { } createdType
            || !SymbolEqualityComparer.Default.Equals(createdType, parallelOptionsType))
        {
            return;
        }

        if (creation.Initializer is not null && SetsMaxDegreeOfParallelism(creation.Initializer))
        {
            return;
        }

        Report(context, invocation);
    }

    private static bool IsDefaultOptions(ExpressionSyntax expression)
    {
        return expression.IsKind(SyntaxKind.DefaultLiteralExpression) || expression is DefaultExpressionSyntax;
    }

    private static ExpressionSyntax? ResolveOptionsExpression(
        ExpressionSyntax expression,
        SyntaxNodeAnalysisContext context,
        int depth)
    {
        if (expression is ObjectCreationExpressionSyntax || IsDefaultOptions(expression))
        {
            return expression;
        }

        if (depth >= MaxResolveDepth)
        {
            return null;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol;
        if (symbol is null || symbol.DeclaringSyntaxReferences.Length is not 1)
        {
            return null;
        }

        var declaration = symbol.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken);

        return declaration switch
        {
            VariableDeclaratorSyntax { Initializer.Value: { } initializer } =>
                ResolveOptionsExpression(initializer, context, depth + 1),
            PropertyDeclarationSyntax { Initializer.Value: { } initializer } =>
                ResolveOptionsExpression(initializer, context, depth + 1),
            MethodDeclarationSyntax method => ResolveMethodReturn(method, context, depth),
            _ => null
        };
    }

    private static ExpressionSyntax? ResolveMethodReturn(
        MethodDeclarationSyntax method,
        SyntaxNodeAnalysisContext context,
        int depth)
    {
        if (method.ExpressionBody?.Expression is { } body)
        {
            return ResolveOptionsExpression(body, context, depth + 1);
        }

        if (method.Body is not { Statements.Count: 1 } block
            || block.Statements[0] is not ReturnStatementSyntax { Expression: { } returned })
        {
            return null;
        }

        return ResolveOptionsExpression(returned, context, depth + 1);
    }

    private static IMethodSymbol? GetForEachAsyncMethod(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol parallelType)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !string.Equals(
                memberAccess.Name.Identifier.ValueText,
                ForEachAsyncMethodName,
                StringComparison.Ordinal))
        {
            return null;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);

        return symbolInfo.Symbol is IMethodSymbol found
            && SymbolEqualityComparer.Default.Equals(found.ContainingType, parallelType)
            ? found
            : null;
    }

    private static ExpressionSyntax? GetOptionsArgument(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        INamedTypeSymbol parallelOptionsType)
    {
        var parameters = method.Parameters;
        var arguments = invocation.ArgumentList.Arguments;
        var positionalIndex = 0;

        foreach (var argument in arguments)
        {
            if (argument.NameColon is NameColonSyntax nameColon)
            {
                if (HasNamedParallelOptions(parameters, nameColon.Name.Identifier.ValueText, parallelOptionsType))
                {
                    return argument.Expression;
                }

                continue;
            }

            if (positionalIndex < parameters.Length
                && IsParallelOptions(parameters[positionalIndex].Type, parallelOptionsType))
            {
                return argument.Expression;
            }

            positionalIndex++;
        }

        return null;
    }

    private static bool HasNamedParallelOptions(
        ImmutableArray<IParameterSymbol> parameters,
        string name,
        INamedTypeSymbol parallelOptionsType)
    {
        for (var i = 0; i < parameters.Length; i++)
        {
            if (string.Equals(parameters[i].Name, name, StringComparison.Ordinal)
                && IsParallelOptions(parameters[i].Type, parallelOptionsType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsParallelOptions(ITypeSymbol type, INamedTypeSymbol parallelOptionsType)
    {
        return SymbolEqualityComparer.Default.Equals(type, parallelOptionsType);
    }

    private static bool SetsMaxDegreeOfParallelism(InitializerExpressionSyntax initializer)
    {
        foreach (var expression in initializer.Expressions)
        {
            if (expression is AssignmentExpressionSyntax assignment
                && assignment.Left is IdentifierNameSyntax identifier
                && string.Equals(
                    identifier.Identifier.ValueText,
                    MaxDegreeOfParallelismMemberName,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void Report(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }
}
