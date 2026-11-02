using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(HttpClientResponseHeadersReadCodeFixProvider))]
[Shared]
public sealed class HttpClientResponseHeadersReadCodeFixProvider : E128CodeFixProviderBase
{
    private const string Namespace = "System.Net.Http";
    private const string OptionTypeName = "System.Net.Http.HttpCompletionOption";
    private const string MemberName = "ResponseHeadersRead";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [HttpClientResponseHeadersReadAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax invocation)
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add HttpCompletionOption.ResponseHeadersRead",
                ct => InsertResponseHeadersReadAsync(context.Document, invocation, ct),
                nameof(HttpClientResponseHeadersReadCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
    }

    private static async Task<Document> InsertResponseHeadersReadAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        var semanticModel = editor.SemanticModel;
        var optionType = semanticModel.Compilation.GetTypeByMetadataName(OptionTypeName);
        if (optionType is null)
        {
            return document;
        }

        // The generator attaches the import annotation, so the code action adds the using and the
        // simplifier shortens the name. A hand-built name would need both steps written out.
        var argument = SyntaxFactory.Argument(
            (ExpressionSyntax)editor.Generator.MemberAccessExpression(
                editor.Generator.TypeExpression(optionType, addImport: true), MemberName));

        var arguments = invocation.ArgumentList.Arguments;
        var insertIndex = FindInsertionIndex(semanticModel, arguments, cancellationToken);
        var candidate = invocation.WithArgumentList(
            invocation.ArgumentList.WithArguments(arguments.Insert(insertIndex, argument)));

        if (!BindsAfterImport(editor, invocation, candidate, optionType, insertIndex))
        {
            return document;
        }

        editor.ReplaceNode(invocation, candidate);
        return editor.GetChangedDocument();
    }

    /// <summary>
    ///     Proves the call still binds once the argument is added. Overload resolution can move the
    ///     call to another member, so the fix declines unless a method takes the option type at the
    ///     inserted position. The bind runs against a copy of the compilation that already holds the
    ///     import, so the unqualified name resolves.
    /// </summary>
    private static bool BindsAfterImport(
        DocumentEditor editor,
        InvocationExpressionSyntax invocation,
        InvocationExpressionSyntax candidate,
        ITypeSymbol optionType,
        int insertIndex)
    {
        var marker = new SyntaxAnnotation();
        var tree = invocation.SyntaxTree;

        var candidateRoot = UsingDirectives.AddIfMissing(
            editor.OriginalRoot.ReplaceNode(invocation, candidate.WithAdditionalAnnotations(marker)),
            Namespace);

        var markedRoot = candidateRoot.GetAnnotatedNodes(marker)
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault();
        if (markedRoot is null)
        {
            return false;
        }

        var boundTree = tree.WithRootAndOptions(candidateRoot, tree.Options);
        var boundCompilation = editor.SemanticModel.Compilation.ReplaceSyntaxTree(tree, boundTree);
        var boundModel = boundCompilation.GetSemanticModel(boundTree);

        var bound = boundModel.GetSpeculativeSymbolInfo(
            markedRoot.SpanStart,
            SyntaxFactory.ParseExpression(markedRoot.WithoutTrivia().ToString()),
            SpeculativeBindingOption.BindAsExpression).Symbol as IMethodSymbol;

        return bound is not null
               && bound.Parameters.Length > insertIndex
               && SymbolEqualityComparer.Default.Equals(bound.Parameters[insertIndex].Type, optionType);
    }

    private static int FindInsertionIndex(
        SemanticModel semanticModel,
        SeparatedSyntaxList<ArgumentSyntax> args,
        CancellationToken cancellationToken)
    {
        if (!args.Any())
        {
            return 0;
        }

        var lastArg = args.Last();
        var lastArgType = semanticModel.GetTypeInfo(lastArg.Expression, cancellationToken).Type;
        return lastArgType is not null
               && string.Equals(lastArgType.ToDisplayString(), "System.Threading.CancellationToken", StringComparison.Ordinal)
            ? args.Count - 1
            : args.Count;
    }
}
