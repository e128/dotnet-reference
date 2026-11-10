using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Code fix for E128159: adds
///     <c lang="csharp">TaskCreationOptions.RunContinuationsAsynchronously</c> to the options
///     argument of a <c lang="csharp">TaskCompletionSource</c> creation.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TaskCompletionSourceOptionsCodeFixProvider))]
[Shared]
public sealed class TaskCompletionSourceOptionsCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [TaskCompletionSourceOptionsAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var creation = node.FirstAncestorOrSelf<BaseObjectCreationExpressionSyntax>(candidate =>
            candidate.Span == diagnostic.Location.SourceSpan);

        if (creation?.ArgumentList is not { } argumentList)
        {
            return Task.CompletedTask;
        }

        // A named argument moves the options value out of the position the fix expects, so the
        // provider declines rather than rewrite the wrong argument.
        foreach (var argument in argumentList.Arguments)
        {
            if (argument.NameColon is not null)
            {
                return Task.CompletedTask;
            }
        }

        CodeFixRegistration.Register(context, diagnostic, "Run continuations asynchronously", nameof(TaskCompletionSourceOptionsCodeFixProvider), ct => AddFlagAsync(context.Document, creation, ct));

        return Task.CompletedTask;
    }

    private static async Task<Document> AddFlagAsync(
        Document document,
        BaseObjectCreationExpressionSyntax creation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        if (root is null
            || semanticModel is null
            || creation.ArgumentList is not { } argumentList)
        {
            return document;
        }

        var optionsIndex = TaskCompletionSourceOptionsAnalyzer.GetOptionsParameterIndex(
            creation,
            semanticModel,
            cancellationToken);

        if (optionsIndex is not { } index)
        {
            return document;
        }

        const string flagText = TaskCompletionSourceOptionsAnalyzer.RunContinuationsFlag;
        var flag = SyntaxFactory.ParseExpression(flagText);

        var arguments = argumentList.Arguments;
        SeparatedSyntaxList<ArgumentSyntax> newArguments;

        if (index >= 0 && index < arguments.Count)
        {
            newArguments = arguments.Replace(arguments[index], WithFlag(arguments[index], flag));
        }
        else if (IsContinuationOptionsState(arguments, semanticModel, cancellationToken))
        {
            // The enum binds to the object state parameter. Replace it, because a second argument would
            // leave the wrong enum in place as the state.
            newArguments = arguments.Replace(
                arguments[0],
                arguments[0].WithExpression(flag.WithTriviaFrom(arguments[0].Expression)));
        }
        else
        {
            newArguments = arguments.Add(SyntaxFactory.Argument(flag));
        }

        var newCreation = creation.WithArgumentList(argumentList.WithArguments(newArguments));
        var newRoot = UsingDirectives.AddIfMissing(
            root.ReplaceNode(creation, newCreation),
            "System.Threading.Tasks");

        return document.WithSyntaxRoot(newRoot);
    }

    private static bool IsContinuationOptionsState(
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (arguments.Count is not 1)
        {
            return false;
        }

        var continuationOptions = semanticModel.Compilation.GetTypeByMetadataName(
            "System.Threading.Tasks.TaskContinuationOptions");
        var argumentType = semanticModel.GetTypeInfo(arguments[0].Expression, cancellationToken).Type;

        return continuationOptions is not null
               && SymbolEqualityComparer.Default.Equals(argumentType, continuationOptions);
    }

    private static ArgumentSyntax WithFlag(ArgumentSyntax argument, ExpressionSyntax flag)
    {
        var original = argument.Expression;
        var left = original is BinaryExpressionSyntax or ConditionalExpressionSyntax or AssignmentExpressionSyntax
            ? SyntaxFactory.ParenthesizedExpression(original.WithoutTrivia())
            : original.WithoutTrivia();
        var combined = SyntaxFactory.BinaryExpression(
                SyntaxKind.BitwiseOrExpression,
                left,
                flag.WithoutTrivia())
            .WithTriviaFrom(original);

        return argument.WithExpression(combined);
    }
}
