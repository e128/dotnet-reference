using System;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace E128.Analyzers.Performance;

/// <summary>
///     Code fix for E128233: rewrites the call as a
///     <c lang="csharp">Parallel.ForEach</c> over <c lang="csharp">Partitioner.Create</c>, so one
///     delegate covers a range instead of one item.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ParallelForPerItemBodyCodeFixProvider))]
[Shared]
public sealed class ParallelForPerItemBodyCodeFixProvider : E128CodeFixProviderBase
{
    private const string ConcurrentNamespace = "System.Collections.Concurrent";
    private const string Title = "Use Parallel.ForEach over a Partitioner range";

    private const int ForArgumentCount = 3;
    private const int BodyArgumentIndex = 2;

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ParallelForPerItemBodyAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];

        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
                is not InvocationExpressionSyntax invocation
            || invocation.ArgumentList is null
            || invocation.Expression is not MemberAccessExpressionSyntax { Name: { } name } memberAccess
            || !string.Equals(
                name.Identifier.ValueText,
                ParallelForPerItemBodyAnalyzer.ForMethodName,
                StringComparison.Ordinal))
        {
            return;
        }

        var semanticModel = await context.Document
            .GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (semanticModel is null
            || semanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (TryMatchBody(
                invocation.ArgumentList.Arguments,
                method,
                semanticModel,
                context.CancellationToken) is not { } parts)
        {
            return;
        }

        if (ForeignPartitionerInScope(semanticModel, invocation))
        {
            return;
        }

        var replacementText = BuildReplacementText(
            invocation,
            memberAccess.Expression.ToString(),
            parts.From.Expression.ToString(),
            parts.To.Expression.ToString(),
            parts);

        var replacement = SyntaxFactory.ParseExpression(replacementText)
            .WithTriviaFrom(invocation)
            .WithAdditionalAnnotations(Formatter.Annotation);

        CodeFixRegistration.Register(
            context,
            diagnostic,
            Title,
            nameof(ParallelForPerItemBodyCodeFixProvider),
            _ => Task.FromResult(Apply(context.Document, root, invocation, replacement)));
    }

    private static LoopParts? TryMatchBody(
        SeparatedSyntaxList<ArgumentSyntax> candidates,
        IMethodSymbol method,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (candidates.Count is not ForArgumentCount)
        {
            return null;
        }

        var fromArgument = ParallelForPerItemBodyAnalyzer.FindArgument(candidates, method, 0);
        var toArgument = ParallelForPerItemBodyAnalyzer.FindArgument(candidates, method, 1);
        var bodyArgument = ParallelForPerItemBodyAnalyzer.FindArgument(candidates, method, BodyArgumentIndex);

        if (fromArgument is null || toArgument is null || bodyArgument is null)
        {
            return null;
        }

        var hasState = method.Parameters[BodyArgumentIndex].Type is INamedTypeSymbol bodyType
                       && bodyType.TypeArguments.Length == ParallelForPerItemBodyAnalyzer.StateBodyParameterCount;
        var itemTypeName = method.Parameters[0].Type.SpecialType is SpecialType.System_Int64 ? "long" : "int";

        return bodyArgument.Expression is AnonymousFunctionExpressionSyntax lambda
            ? MatchLambdaBody(lambda, fromArgument, toArgument, itemTypeName, hasState)
            : MatchMethodGroupBody(
                bodyArgument.Expression,
                semanticModel,
                fromArgument,
                toArgument,
                itemTypeName,
                hasState,
                cancellationToken);
    }

    private static LoopParts? MatchLambdaBody(
        AnonymousFunctionExpressionSyntax lambda,
        ArgumentSyntax from,
        ArgumentSyntax to,
        string itemTypeName,
        bool hasState)
    {
        if (ParallelForPerItemBodyAnalyzer.IsAsyncLambda(lambda))
        {
            return null;
        }

        return TryGetLoopParts(lambda, hasState, out var parameterName, out var stateName, out var statements)
            ? new LoopParts(from, to, itemTypeName, parameterName, stateName, statements)
            : null;
    }

    private static LoopParts? MatchMethodGroupBody(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        ArgumentSyntax from,
        ArgumentSyntax to,
        string itemTypeName,
        bool hasState,
        CancellationToken cancellationToken)
    {
        return TryGetMethodGroupParts(
                expression,
                semanticModel,
                hasState,
                cancellationToken,
                out var parameterName,
                out var stateName,
                out var statements)
            ? new LoopParts(from, to, itemTypeName, parameterName, stateName, statements)
            : null;
    }

    private static bool TryGetMethodGroupParts(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        bool hasState,
        CancellationToken cancellationToken,
        out string parameterName,
        out string? stateParameterName,
        out ImmutableArray<string> bodyStatements)
    {
        parameterName = string.Empty;
        stateParameterName = null;
        bodyStatements = [];

        if (semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol is not IMethodSymbol method
            || method.IsAsync
            || method.Parameters.Length != (hasState ? ParallelForPerItemBodyAnalyzer.StateBodyParameterCount : 1))
        {
            return false;
        }

        parameterName = UniqueLoopVariableName(semanticModel, expression.SpanStart, method.Parameters[0].Name);
        stateParameterName = hasState
            ? UniqueLoopVariableName(semanticModel, expression.SpanStart, "state")
            : null;

        var arguments = stateParameterName is null
            ? parameterName
            : parameterName + ", " + stateParameterName;
        bodyStatements = [expression.ToFullString().Trim() + "(" + arguments + ");"];
        return true;
    }

    private static string UniqueLoopVariableName(SemanticModel semanticModel, int position, string baseName)
    {
        if (!string.Equals(baseName, ParallelForPerItemBodyAnalyzer.RangeParameterName, StringComparison.Ordinal)
            && semanticModel.LookupSymbols(position, name: baseName).Length is 0)
        {
            return baseName;
        }

        var index = 1;
        string candidate;
        do
        {
            candidate = baseName + index.ToString(CultureInfo.InvariantCulture);
            index++;
        }
        while (semanticModel.LookupSymbols(position, name: candidate).Length > 0);

        return candidate;
    }

    private static Document Apply(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax replacement)
    {
        var newRoot = root.ReplaceNode(invocation, replacement);
        return document.WithSyntaxRoot(UsingDirectives.AddIfMissing(newRoot, ConcurrentNamespace));
    }

    private static bool ForeignPartitionerInScope(SemanticModel semanticModel, InvocationExpressionSyntax invocation)
    {
        foreach (var symbol in semanticModel.LookupSymbols(invocation.SpanStart, name: "Partitioner"))
        {
            if (symbol is not INamedTypeSymbol type
                || !Namespaces.Equals(type.ContainingNamespace, ConcurrentNamespace))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetLoopParts(
        AnonymousFunctionExpressionSyntax lambda,
        bool hasState,
        out string parameterName,
        out string? stateParameterName,
        out ImmutableArray<string> bodyStatements)
    {
        parameterName = string.Empty;
        stateParameterName = null;
        bodyStatements = [];

        var parameters = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => SyntaxFactory.SingletonSeparatedList(simple.Parameter),
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters,
            _ => default,
        };

        if (parameters.Count != (hasState ? ParallelForPerItemBodyAnalyzer.StateBodyParameterCount : 1))
        {
            return false;
        }

        foreach (var parameter in parameters)
        {
            if (string.Equals(
                    parameter.Identifier.ValueText,
                    ParallelForPerItemBodyAnalyzer.RangeParameterName,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        parameterName = parameters[0].Identifier.ValueText;
        stateParameterName = hasState ? parameters[1].Identifier.ValueText : null;

        switch (lambda.Body)
        {
            case BlockSyntax { Statements.Count: > 0 } block:
                var builder = ImmutableArray.CreateBuilder<string>(block.Statements.Count);
                foreach (var statement in block.Statements)
                {
                    builder.Add(statement.ToFullString().Trim());
                }

                bodyStatements = builder.ToImmutable();
                return true;
            case ExpressionSyntax expression when ParallelForPerItemBodyAnalyzer.IsStatementExpression(expression):
                bodyStatements = [expression.ToFullString().Trim() + ";"];
                return true;
            default:
                return false;
        }
    }

    private static string BuildReplacementText(
        InvocationExpressionSyntax invocation,
        string receiverText,
        string fromText,
        string toText,
        LoopParts parts)
    {
        var lineBreak = StatementLineBreak(invocation);
        var indent = FixSyntax.LastIndentation(invocation.GetLeadingTrivia());
        const string range = ParallelForPerItemBodyAnalyzer.RangeParameterName;

        var lambdaHeader = parts.StateParameterName is { } state
            ? "(" + range + ", " + state + ") =>"
            : range + " =>";

        var builder = new StringBuilder()
            .Append(receiverText)
            .Append(".ForEach(Partitioner.Create(").Append(fromText).Append(", ").Append(toText)
            .Append("), ").Append(lambdaHeader).Append(lineBreak)
            .Append(indent).Append('{').Append(lineBreak)
            .Append(indent).Append("    for (").Append(parts.ItemTypeName).Append(' ').Append(parts.ParameterName)
            .Append(" = ").Append(range).Append(".Item1; ").Append(parts.ParameterName)
            .Append(" < ").Append(range).Append(".Item2; ").Append(parts.ParameterName).Append("++)")
            .Append(lineBreak)
            .Append(indent).Append("    {").Append(lineBreak);

        foreach (var statement in parts.Statements)
        {
            builder.Append(indent).Append("        ").Append(statement).Append(lineBreak);
        }

        return builder
            .Append(indent).Append("    }").Append(lineBreak)
            .Append(indent).Append("});")
            .ToString();
    }

    private sealed class LoopParts
    {
        public LoopParts(
            ArgumentSyntax from,
            ArgumentSyntax to,
            string itemTypeName,
            string parameterName,
            string? stateParameterName,
            ImmutableArray<string> statements)
        {
            From = from;
            To = to;
            ItemTypeName = itemTypeName;
            ParameterName = parameterName;
            StateParameterName = stateParameterName;
            Statements = statements;
        }

        public ArgumentSyntax From { get; }

        public ArgumentSyntax To { get; }

        public string ItemTypeName { get; }

        public string ParameterName { get; }

        public string? StateParameterName { get; }

        public ImmutableArray<string> Statements { get; }
    }

    private static string StatementLineBreak(InvocationExpressionSyntax invocation)
    {
        var leading = invocation.GetLeadingTrivia();
        for (var i = leading.Count - 1; i >= 0; i--)
        {
            if (leading[i].IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return leading[i].ToFullString();
            }
        }

        return "\n";
    }
}
