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
    private const string ParallelOptionsTypeName = "ParallelOptions";
    private const string ForEachAsyncMethodName = "ForEachAsync";
    private const string MaxDegreeOfParallelismMemberName = "MaxDegreeOfParallelism";

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

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var parallelType = compilationContext.Compilation.GetTypeByMetadataName(ParallelMetadataName);
            if (parallelType is null)
            {
                return;
            }

            var parallelOptionsType = compilationContext.Compilation.GetTypeByMetadataName(ParallelOptionsMetadataName);
            if (parallelOptionsType is null)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, parallelType, parallelOptionsType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol parallelType,
        INamedTypeSymbol parallelOptionsType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (!string.Equals(
            memberAccess.Name.Identifier.ValueText,
            ForEachAsyncMethodName,
            StringComparison.Ordinal))
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, parallelType))
        {
            return;
        }

        var optionsArgument = GetOptionsArgument(invocation, method, parallelOptionsType);
        if (optionsArgument is null)
        {
            Report(context, invocation);
            return;
        }

        // "default" or "default(ParallelOptions)" builds an options object with no degree cap.
        if (optionsArgument.IsKind(SyntaxKind.DefaultLiteralExpression)
            || optionsArgument is DefaultExpressionSyntax)
        {
            Report(context, invocation);
            return;
        }

        if (optionsArgument is not ObjectCreationExpressionSyntax creation)
        {
            // A variable, parameter, field, property, or factory call. The value is not visible here.
            return;
        }

        if (!string.Equals(GetTypeName(creation.Type), ParallelOptionsTypeName, StringComparison.Ordinal))
        {
            return;
        }

        if (creation.Initializer is null || !SetsMaxDegreeOfParallelism(creation.Initializer))
        {
            Report(context, invocation);
        }
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
                var name = nameColon.Name.Identifier.ValueText;
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (string.Equals(parameters[i].Name, name, StringComparison.Ordinal)
                        && IsParallelOptions(parameters[i].Type, parallelOptionsType))
                    {
                        return argument.Expression;
                    }
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

    private static bool IsParallelOptions(ITypeSymbol type, INamedTypeSymbol parallelOptionsType)
    {
        return SymbolEqualityComparer.Default.Equals(type, parallelOptionsType);
    }

    private static string GetTypeName(TypeSyntax type)
    {
        return type switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => type.ToString()
        };
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
