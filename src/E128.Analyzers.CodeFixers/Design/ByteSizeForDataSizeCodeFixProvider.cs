using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ByteSizeForDataSizeCodeFixProvider))]
[Shared]
public sealed class ByteSizeForDataSizeCodeFixProvider : E128CodeFixProviderBase
{
    private const string DefaultByteSizeNamespace = "Pug.Core.Classes";
    private const string ByteSizeTypeName = "ByteSize";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ByteSizeForDataSizeAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var token = root.FindToken(diagnostic.Location.SourceSpan.Start);

        // A parameter default is a compile-time constant, and ByteSize.FromBytes is a method call, so
        // the rewritten parameter gives CS1736. The developer has to pick the unit, so no fix is offered.
        if (token.Parent?.FirstAncestorOrSelf<ParameterSyntax>() is { Default: not null })
        {
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(context, diagnostic, "Change type to ByteSize", nameof(ByteSizeForDataSizeCodeFixProvider), ct => ApplyFixAsync(context.Document, root, token, ct));

        return Task.CompletedTask;
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode root,
        SyntaxToken token,
        CancellationToken cancellationToken)
    {
        // The type moved namespaces before. Resolve it in the compilation instead of
        // hardcoding one namespace, so the fix still compiles after a move.
        var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        var (qualifiedTypeName, namespaceName) = ResolveByteSizeTypeName(compilation);

        var parent = token.Parent ?? root;
        var newRoot = parent switch
        {
            _ when parent.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>() is { } local =>
                FixLocalDeclaration(root, local, qualifiedTypeName),

            _ when parent.FirstAncestorOrSelf<PropertyDeclarationSyntax>() is { } property =>
                FixPropertyDeclaration(root, property, qualifiedTypeName),

            _ when parent.FirstAncestorOrSelf<ParameterSyntax>() is { } parameter =>
                FixParameterDeclaration(root, parameter, qualifiedTypeName),

            _ when parent.FirstAncestorOrSelf<FieldDeclarationSyntax>() is { } field =>
                FixFieldDeclaration(root, field, qualifiedTypeName),

            _ => root
        };

        if (namespaceName is not null)
        {
            newRoot = UsingDirectives.AddIfMissing(newRoot, namespaceName);
        }

        return document.WithSyntaxRoot(newRoot);
    }

    private static (string QualifiedTypeName, string? NamespaceName) ResolveByteSizeTypeName(
        Compilation? compilation)
    {
        var type = compilation is null ? null : ResolveByteSizeType(compilation);

        if (type is null)
        {
            return ($"{DefaultByteSizeNamespace}.{ByteSizeTypeName}", DefaultByteSizeNamespace);
        }

        if (type.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace)
        {
            return (type.ToDisplayString(), containingNamespace.ToDisplayString());
        }

        return (ByteSizeTypeName, null);
    }

    private static INamedTypeSymbol? ResolveByteSizeType(Compilation compilation)
    {
        return compilation.GetTypeByMetadataName($"{DefaultByteSizeNamespace}.{ByteSizeTypeName}")
               ?? FindByteSizeType(compilation.GlobalNamespace);
    }

    private static INamedTypeSymbol? FindByteSizeType(INamespaceSymbol namespaceSymbol)
    {
        foreach (var member in namespaceSymbol.GetMembers())
        {
            if (member is INamespaceSymbol child)
            {
                if (FindByteSizeType(child) is { } nested)
                {
                    return nested;
                }
            }
            else if (member is INamedTypeSymbol type
                     && string.Equals(type.Name, ByteSizeTypeName, StringComparison.Ordinal)
                     && type.GetMembers("FromBytes").OfType<IMethodSymbol>().Any(static method => method.IsStatic))
            {
                return type;
            }
        }

        return null;
    }

    private static SyntaxNode FixLocalDeclaration(
        SyntaxNode root,
        LocalDeclarationStatementSyntax local,
        string qualifiedTypeName)
    {
        var declaration = local.Declaration;
        var byteSizeType = CreateByteSizeTypeSyntax();

        var newDeclaration = WrapEveryInitializer(declaration, qualifiedTypeName)
            .WithType(byteSizeType.WithTrailingTrivia(SyntaxFactory.Space));

        var newLocal = local
            .WithModifiers(RemoveConstModifier(local.Modifiers))
            .WithDeclaration(newDeclaration);

        return root.ReplaceNode(local, newLocal);
    }

    private static SyntaxNode FixPropertyDeclaration(
        SyntaxNode root,
        PropertyDeclarationSyntax property,
        string qualifiedTypeName)
    {
        var byteSizeType = CreateByteSizeTypeSyntax();
        var newProperty = property.WithType(byteSizeType.WithTrailingTrivia(SyntaxFactory.Space));

        if (property.Initializer is { } initializer)
        {
            var wrapped = WrapInFromBytes(initializer.Value, qualifiedTypeName);
            newProperty = newProperty.WithInitializer(initializer.WithValue(wrapped));
        }

        return root.ReplaceNode(property, newProperty);
    }

    private static SyntaxNode FixParameterDeclaration(
        SyntaxNode root,
        ParameterSyntax parameter,
        string qualifiedTypeName)
    {
        if (parameter.Type is null)
        {
            return root;
        }

        var byteSizeType = CreateByteSizeTypeSyntax();
        var newParameter = parameter.WithType(byteSizeType.WithTrailingTrivia(SyntaxFactory.Space));

        if (parameter.Default is { } defaultValue)
        {
            var wrapped = WrapInFromBytes(defaultValue.Value, qualifiedTypeName);
            newParameter = newParameter.WithDefault(defaultValue.WithValue(wrapped));
        }

        return root.ReplaceNode(parameter, newParameter);
    }

    private static SyntaxNode FixFieldDeclaration(
        SyntaxNode root,
        FieldDeclarationSyntax field,
        string qualifiedTypeName)
    {
        var declaration = field.Declaration;
        var byteSizeType = CreateByteSizeTypeSyntax();
        var newDeclaration = WrapEveryInitializer(declaration, qualifiedTypeName)
            .WithType(byteSizeType.WithTrailingTrivia(SyntaxFactory.Space));

        var newModifiers = field.Modifiers;
        if (newModifiers.Any(SyntaxKind.ConstKeyword))
        {
            newModifiers = RemoveConstModifier(newModifiers);
            newModifiers = InModifierHelper.AddModifier(newModifiers, SyntaxKind.StaticKeyword);
            newModifiers = InModifierHelper.AddModifier(newModifiers, SyntaxKind.ReadOnlyKeyword);
        }

        var newField = field
            .WithModifiers(newModifiers)
            .WithDeclaration(newDeclaration);

        return root.ReplaceNode(field, newField);
    }

    // Every declarator shares the new type, so every initializer must carry the conversion. A field
    // or local with more than one declarator would otherwise assign an int to ByteSize.
    private static VariableDeclarationSyntax WrapEveryInitializer(
        VariableDeclarationSyntax declaration,
        string qualifiedTypeName)
    {
        var variables = declaration.Variables.Select(variable =>
            variable.Initializer is { } initializer
                ? variable.WithInitializer(initializer.WithValue(WrapInFromBytes(initializer.Value, qualifiedTypeName)))
                : variable);

        return declaration.WithVariables(SyntaxFactory.SeparatedList(variables));
    }

    private static InvocationExpressionSyntax WrapInFromBytes(
        ExpressionSyntax expression,
        string qualifiedTypeName)
    {
        var qualifiedName = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.ParseExpression(qualifiedTypeName),
            SyntaxFactory.IdentifierName("FromBytes"));

        return SyntaxFactory.InvocationExpression(
                qualifiedName,
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(expression.WithoutTrivia()))))
            .WithTriviaFrom(expression);
    }

    private static IdentifierNameSyntax CreateByteSizeTypeSyntax()
    {
        return SyntaxFactory.IdentifierName(ByteSizeTypeName);
    }

    private static SyntaxTokenList RemoveConstModifier(SyntaxTokenList modifiers)
    {
        return SyntaxFactory.TokenList(modifiers.Where(static m => !m.IsKind(SyntaxKind.ConstKeyword)));
    }
}
