using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text;
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

    // The overload shape the fix rewrites: fromInclusive, toExclusive, body.
    private const int ForArgumentCount = 3;
    private const int BodyArgumentIndex = 2;

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ParallelForPerItemBodyAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];

        // Keep whatever receiver names the type: Parallel, an alias, or the qualified
        // System.Threading.Tasks.Parallel.
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
                is not InvocationExpressionSyntax invocation
            || invocation.ArgumentList is null
            || invocation.Expression is not MemberAccessExpressionSyntax { Name: { } name } memberAccess
            || !string.Equals(
                name.Identifier.ValueText,
                ParallelForPerItemBodyAnalyzer.ForMethodName,
                StringComparison.Ordinal)
            || !TryMatchBody(invocation.ArgumentList.Arguments, out var arguments, out var parameterName, out var bodyStatements))
        {
            return;
        }

        var semanticModel = await context.Document
            .GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        // The rewrite writes the name Partitioner unqualified. Decline when another symbol with
        // that name is in scope, because the added using would make the call ambiguous.
        if (ForeignPartitionerInScope(semanticModel, invocation))
        {
            return;
        }

        var replacementText = BuildReplacementText(
            invocation,
            memberAccess.Expression.ToString(),
            arguments[0].Expression.ToString(),
            arguments[1].Expression.ToString(),
            parameterName,
            bodyStatements);

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

    private static bool TryMatchBody(
        SeparatedSyntaxList<ArgumentSyntax> candidates,
        out SeparatedSyntaxList<ArgumentSyntax> arguments,
        out string parameterName,
        out ImmutableArray<string> bodyStatements)
    {
        arguments = candidates;
        parameterName = string.Empty;
        bodyStatements = [];

        if (candidates.Count is not ForArgumentCount)
        {
            return false;
        }

        // The fix emits positional arguments only, so a named-argument call offers no fix.
        if (candidates.Any(static argument => argument.NameColon is not null))
        {
            return false;
        }

        if (candidates[BodyArgumentIndex].Expression is not AnonymousFunctionExpressionSyntax lambda
            || !TryGetLoopParts(lambda, out parameterName, out bodyStatements))
        {
            return false;
        }

        return true;
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

    // True when a symbol named Partitioner is in scope and the rewrite cannot use it. The real
    // Partitioner counts as usable, because the rewrite names its own namespace.
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
        out string parameterName,
        out ImmutableArray<string> bodyStatements)
    {
        parameterName = string.Empty;
        bodyStatements = [];

        var parameter = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters: { Count: 1 } parameters }
                => parameters[0],
            _ => null,
        };

        if (parameter is null)
        {
            return false;
        }

        parameterName = parameter.Identifier.ValueText;

        // The rewrite declares its range parameter with this name, so a body parameter with the
        // same name would collide.
        if (string.Equals(parameterName, ParallelForPerItemBodyAnalyzer.RangeParameterName, StringComparison.Ordinal))
        {
            return false;
        }

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
        string parameterName,
        ImmutableArray<string> bodyStatements)
    {
        var lineBreak = StatementLineBreak(invocation);
        var indent = FixSyntax.LastIndentation(invocation.GetLeadingTrivia());
        const string range = ParallelForPerItemBodyAnalyzer.RangeParameterName;

        var builder = new StringBuilder()
            .Append(receiverText)
            .Append(".ForEach(Partitioner.Create(").Append(fromText).Append(", ").Append(toText)
            .Append("), ").Append(range).Append(" =>").Append(lineBreak)
            .Append(indent).Append('{').Append(lineBreak)
            .Append(indent).Append("    for (int ").Append(parameterName)
            .Append(" = ").Append(range).Append(".Item1; ").Append(parameterName)
            .Append(" < ").Append(range).Append(".Item2; ").Append(parameterName).Append("++)")
            .Append(lineBreak)
            .Append(indent).Append("    {").Append(lineBreak);

        foreach (var statement in bodyStatements)
        {
            builder.Append(indent).Append("        ").Append(statement).Append(lineBreak);
        }

        return builder
            .Append(indent).Append("    }").Append(lineBreak)
            .Append(indent).Append("});")
            .ToString();
    }

    // The end-of-line trivia that ended the previous source line. The rewrite keeps the line
    // endings the file already uses.
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
