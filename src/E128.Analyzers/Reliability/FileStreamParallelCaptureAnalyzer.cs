using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128234: Reports a <c lang="csharp">System.IO.FileStream</c> that a parallel body captures, so
///     the parallel readers share one position on the stream.
/// </summary>
/// <remarks>
///     The rule resolves <c lang="csharp">Parallel.For</c>, <c lang="csharp">Parallel.ForEach</c>, and
///     <c lang="csharp">Parallel.Invoke</c> by symbol, so a user-defined <c lang="csharp">Parallel</c>
///     type stays silent. A local, a parameter, or a field counts as captured when the parallel body
///     references its identifier. A stream that the body itself declares, as in
///     <c lang="csharp">Parallel.ForEach(o =&gt; ReadAt(File.OpenRead(path), o))</c>, belongs to one
///     worker and stays silent. The report anchors on the first use of the stream inside the body and
///     fires once per parallel invocation. A nested lambda inside the body still counts as a use. The
///     walk skips a local function body, and a lambda outside the parallel call does not count.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FileStreamParallelCaptureAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128234";

    private const string ParallelMetadataName = "System.Threading.Tasks.Parallel";

    private const string FileStreamMetadataName = "System.IO.FileStream";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "A FileStream position is shared across parallel readers",
        "'{0}' is captured by a parallel body, so the readers share one position: open the file with 'File.OpenHandle' and read with 'RandomAccess.Read' at an explicit offset",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "A FileStream keeps one file position. Parallel readers that capture the same stream move " +
        "that position at the same time, so each worker reads the wrong bytes. Open the file with " +
        "File.OpenHandle and read with RandomAccess.Read at an explicit offset.");

    private static readonly ImmutableHashSet<string> ParallelMethodNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "For",
        "ForEach",
        "Invoke");

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
        {
            var parallelType = compilationContext.Compilation.GetTypeByMetadataName(ParallelMetadataName);
            var fileStreamType = compilationContext.Compilation.GetTypeByMetadataName(FileStreamMetadataName);

            if (parallelType is null || fileStreamType is null)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, parallelType, fileStreamType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol parallelType,
        INamedTypeSymbol fileStreamType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.ArgumentList is null || !IsParallelCall(context, invocation, parallelType))
        {
            return;
        }

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is not AnonymousFunctionExpressionSyntax lambda
                || FindFirstCapturedUse(context, lambda, fileStreamType) is not { } use)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, use.GetLocation(), use.Identifier.ValueText));
            return;
        }
    }

    private static bool IsParallelCall(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol parallelType)
    {
        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: { } memberName } => memberName.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null
        };

        if (name is null || !ParallelMethodNames.Contains(name))
        {
            return false;
        }

        return context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is IMethodSymbol { IsStatic: true } method
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, parallelType);
    }

    private static IdentifierNameSyntax? FindFirstCapturedUse(
        SyntaxNodeAnalysisContext context,
        AnonymousFunctionExpressionSyntax lambda,
        INamedTypeSymbol fileStreamType)
    {
        var localFunctions = new List<LocalFunctionStatementSyntax>(
            lambda.DescendantNodes().OfType<LocalFunctionStatementSyntax>());

        foreach (var identifier in lambda.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (localFunctions.Exists(function => function.Span.Contains(identifier.Span)))
            {
                continue;
            }

            if (context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol is not { } symbol)
            {
                continue;
            }

            var type = symbol switch
            {
                ILocalSymbol local => local.Type,
                IParameterSymbol parameter => parameter.Type,
                IFieldSymbol field => field.Type,
                _ => null
            };

            if (!TypeSymbols.DerivesFrom(type, fileStreamType) || IsDeclaredInside(symbol, lambda))
            {
                continue;
            }

            return identifier;
        }

        return null;
    }

    private static bool IsDeclaredInside(ISymbol symbol, SyntaxNode lambda)
    {
        return symbol.DeclaringSyntaxReferences.Any(reference =>
            reference.SyntaxTree is { } tree
            && ReferenceEquals(tree, lambda.SyntaxTree)
            && lambda.Span.Contains(reference.Span));
    }
}
