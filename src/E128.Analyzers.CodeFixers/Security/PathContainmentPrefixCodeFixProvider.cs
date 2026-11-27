using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Security;

/// <summary>
///     Code fix for E128263: wraps the root argument with
///     <c lang="csharp">Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar</c>,
///     so a sibling name cannot pass the containment test.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PathContainmentPrefixCodeFixProvider))]
[Shared]
public sealed class PathContainmentPrefixCodeFixProvider : E128CodeFixProviderBase
{
    private const string Title = "Append a directory separator to the root";

    private const string PathTypeName = "System.IO.Path";

    private const string TrimMethodName = "TrimEndingDirectorySeparator";

    private const string SeparatorName = "DirectorySeparatorChar";

    private const string QualifiedPathExpression = "global::System.IO.Path";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [PathContainmentPrefixAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();

        if (invocation is null || invocation.ArgumentList.Arguments.Count is 0)
        {
            return;
        }

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (model is null
            || !HasTrimEndingDirectorySeparator(model.Compilation)
            || !IsNonNullableString(invocation, model, context.CancellationToken))
        {
            return;
        }

        CodeFixRegistration.Register(
            context,
            diagnostic,
            Title,
            nameof(PathContainmentPrefixCodeFixProvider),
            ct => ApplyFixAsync(context.Document, invocation, ct));
    }

    private static bool HasTrimEndingDirectorySeparator(Compilation compilation)
    {
        var path = compilation.GetTypeByMetadataName(PathTypeName);

        return path is not null && path.GetMembers(TrimMethodName).OfType<IMethodSymbol>().Any();
    }

    private static bool IsNonNullableString(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var expression = invocation.ArgumentList.Arguments[0].Expression;
        var type = SymbolType(model.GetSymbolInfo(expression, cancellationToken).Symbol)
                   ?? model.GetTypeInfo(expression, cancellationToken).Type;

        return type?.SpecialType is SpecialType.System_String
               && type.NullableAnnotation is not NullableAnnotation.Annotated;
    }

    private static ITypeSymbol? SymbolType(ISymbol? symbol)
    {
        return symbol switch
        {
            ILocalSymbol local => local.Type,
            IParameterSymbol parameter => parameter.Type,
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => null,
        };
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null || invocation.ArgumentList.Arguments.Count is 0)
        {
            return document;
        }

        var argument = invocation.ArgumentList.Arguments[0];
        var expression = argument.Expression;
        var trimmed = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ParseExpression(QualifiedPathExpression),
                SyntaxFactory.IdentifierName(TrimMethodName)),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(expression.WithoutTrivia()))));
        var separator = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.ParseExpression(QualifiedPathExpression),
            SyntaxFactory.IdentifierName(SeparatorName));
        var combined = SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, trimmed, separator);
        var newArgument = argument.WithExpression(combined.WithTriviaFrom(expression));
        var newInvocation = invocation.WithArgumentList(
            invocation.ArgumentList.WithArguments(
                invocation.ArgumentList.Arguments.Replace(argument, newArgument)));
        var newRoot = root.ReplaceNode(invocation, newInvocation);

        return document.WithSyntaxRoot(newRoot);
    }
}
