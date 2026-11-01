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

namespace E128.Analyzers.Design;

/// <summary>
///     Code fix for E128061. A <c lang="csharp">static readonly byte[]</c> field of constants becomes a
///     <c lang="csharp">static ReadOnlySpan&lt;byte&gt;</c> property, which the compiler stores as read-only
///     data. Every other element type becomes <c lang="csharp">ImmutableArray&lt;T&gt;</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MutableStaticReadonlyArrayCodeFixProvider))]
[Shared]
public sealed class MutableStaticReadonlyArrayCodeFixProvider : CodeFixProvider
{
    private const string ImmutableNamespace = "System.Collections.Immutable";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [MutableStaticReadonlyArrayAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var node = root.FindNode(context.Diagnostics[0].Location.SourceSpan, getInnermostNodeForTie: true);

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);

        if (semanticModel is not null
            && node.AncestorsAndSelf().OfType<FieldDeclarationSyntax>().FirstOrDefault() is { } field
            && BuildSpanProperties(field, semanticModel, context.CancellationToken) is { } properties)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Convert to ReadOnlySpan<byte>",
                    ct => ReplaceFieldWithPropertiesAsync(context.Document, field, properties, ct),
                    MutableStaticReadonlyArrayAnalyzer.DiagnosticId),
                context.Diagnostics[0]);

            return;
        }

        var declaration = node.AncestorsAndSelf().OfType<VariableDeclarationSyntax>().FirstOrDefault();

        if (declaration is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Convert to ImmutableArray<T>",
                ct => ConvertToImmutableArrayAsync(context.Document, declaration, ct),
                MutableStaticReadonlyArrayAnalyzer.DiagnosticId),
            context.Diagnostics[0]);
    }

    /// <summary>
    ///     Builds one <c lang="csharp">ReadOnlySpan&lt;byte&gt;</c> property per variable. Returns
    ///     null when any variable is not a constant byte sequence.
    /// </summary>
    private static ImmutableArray<PropertyDeclarationSyntax>? BuildSpanProperties(
        FieldDeclarationSyntax field,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var properties = ImmutableArray.CreateBuilder<PropertyDeclarationSyntax>(field.Declaration.Variables.Count);

        foreach (var variable in field.Declaration.Variables)
        {
            if (variable.Initializer is null
                || semanticModel.GetDeclaredSymbol(variable, cancellationToken) is not IFieldSymbol
                {
                    Type: IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte }
                }
                || TryGetElements(variable.Initializer.Value) is not { } elements
                || !elements.All(element => IsByteConstant(element, semanticModel, cancellationToken)))
            {
                return null;
            }

            properties.Add(CreateSpanProperty(field, variable, elements));
        }

        return properties.Count == 0 ? null : properties.MoveToImmutable();
    }

    private static PropertyDeclarationSyntax CreateSpanProperty(
        FieldDeclarationSyntax field,
        VariableDeclaratorSyntax variable,
        ImmutableArray<ExpressionSyntax> elements)
    {
        var modifiers = SyntaxFactory.TokenList(
            field.Modifiers.Where(modifier => !modifier.IsKind(SyntaxKind.ReadOnlyKeyword)));

        var collection = SyntaxFactory.CollectionExpression(
            SyntaxFactory.SeparatedList<CollectionElementSyntax>(
                elements.Select(element => SyntaxFactory.ExpressionElement(element.WithoutTrivia()))));

        return SyntaxFactory.PropertyDeclaration(
                SyntaxFactory.ParseTypeName("ReadOnlySpan<byte>"),
                variable.Identifier.WithoutTrivia().WithLeadingTrivia(SyntaxFactory.Space))
            .WithAttributeLists(field.AttributeLists)
            .WithModifiers(modifiers)
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(collection))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
    }

    private static async Task<Document> ReplaceFieldWithPropertiesAsync(
        Document document,
        FieldDeclarationSyntax field,
        ImmutableArray<PropertyDeclarationSyntax> properties,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var replacement = properties
            .Select((property, index) => index == properties.Length - 1
                ? property.WithTrailingTrivia(field.GetTrailingTrivia())
                : property.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed))
            .ToArray();

        replacement[0] = replacement[0].WithLeadingTrivia(field.GetLeadingTrivia());

        return document.WithSyntaxRoot(
            UsingDirectives.AddIfMissing(root.ReplaceNode(field, replacement), "System"));
    }

    private static ImmutableArray<ExpressionSyntax>? TryGetElements(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case CollectionExpressionSyntax collection
                when collection.Elements.All(element => element is ExpressionElementSyntax):
                return [.. collection.Elements.Cast<ExpressionElementSyntax>().Select(element => element.Expression)];
            case ImplicitArrayCreationExpressionSyntax { Initializer: { } implicitInitializer }:
                return [.. implicitInitializer.Expressions];
            case ArrayCreationExpressionSyntax { Initializer: { } explicitInitializer }:
                return [.. explicitInitializer.Expressions];
            default:
                return null;
        }
    }

    private static bool IsByteConstant(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var constant = semanticModel.GetConstantValue(expression, cancellationToken);

        return constant.HasValue
            && constant.Value switch
            {
                byte => true,
                int value => value is >= 0 and <= 255,
                _ => false
            };
    }

    private static async Task<Document> ConvertToImmutableArrayAsync(
        Document document,
        VariableDeclarationSyntax declaration,
        CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null || root is null)
        {
            return document;
        }

        // Get the element type from the array type
        if (declaration.Type is not ArrayTypeSyntax arrayType)
        {
            return document;
        }

        var elementTypeSyntax = arrayType.ElementType;

        // Build ImmutableArray<T> type syntax
        var immutableArrayType = SyntaxFactory.GenericName(
            SyntaxFactory.Identifier("ImmutableArray"),
            SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList([elementTypeSyntax])));

        // Transform initializers: convert new T[] { ... } / new[] { ... } to collection expressions
        var newVariables = declaration.Variables.Select(TransformVariable).ToList();

        var newDeclaration = declaration
            .WithType(immutableArrayType.WithTriviaFrom(arrayType))
            .WithVariables(SyntaxFactory.SeparatedList(newVariables));

        var newRoot = UsingDirectives.AddIfMissing(
            root.ReplaceNode(declaration, newDeclaration), ImmutableNamespace);
        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    ///     Converts the variable initializer to a collection expression if needed.
    ///     Collection expressions (["a", "b"]) work directly with ImmutableArray.
    ///     Explicit array construction (new T[] { ... } or new[] { ... }) is unwrapped to collection expressions.
    /// </summary>
    private static VariableDeclaratorSyntax TransformVariable(VariableDeclaratorSyntax variable)
    {
        if (variable.Initializer is null)
        {
            return variable;
        }

        var expression = variable.Initializer.Value;

        // Collection expression ["a", "b"] → works as-is with ImmutableArray<T>
        if (expression is CollectionExpressionSyntax)
        {
            return variable;
        }

        // new T[] { "a", "b" } or new[] { "a", "b" } → unwrap to collection expression
        var unwrapped = UnwrapArrayCreation(expression);
        return unwrapped is not null
            ? variable.WithInitializer(variable.Initializer.WithValue(unwrapped))
            : variable;
    }

    /// <summary>
    ///     Unwraps array creation expressions to collection expressions.
    ///     new T[] { "a", "b" } → ["a", "b"]
    ///     new[] { "a", "b" } → ["a", "b"]
    /// </summary>
    private static CollectionExpressionSyntax? UnwrapArrayCreation(ExpressionSyntax expression)
    {
        // Implicit: new[] { "a", "b" } or Explicit: new T[] { "a", "b" }
        var initializer = expression is ImplicitArrayCreationExpressionSyntax implicitArray
            ? implicitArray.Initializer
            : expression is ArrayCreationExpressionSyntax explicitArray
                ? explicitArray.Initializer
                : null;

        return initializer is not null
            ? CollectionExpressionFromInitializers(initializer)
            : null;
    }

    private static CollectionExpressionSyntax? CollectionExpressionFromInitializers(InitializerExpressionSyntax? initializer)
    {
        if (initializer is null || !initializer.Expressions.Any())
        {
            return null;
        }

        var elements = initializer.Expressions.Select(expr =>
            SyntaxFactory.ExpressionElement(expr.WithoutTrailingTrivia())).ToList();

        return SyntaxFactory.CollectionExpression(
            SyntaxFactory.SeparatedList<CollectionElementSyntax>(elements));
    }
}
