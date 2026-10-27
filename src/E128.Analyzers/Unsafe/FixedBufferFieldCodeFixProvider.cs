using System;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace E128.Analyzers.Unsafe;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FixedBufferFieldCodeFixProvider))]
[Shared]
public sealed class FixedBufferFieldCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [FixedBufferFieldAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not FieldDeclarationSyntax { Parent: TypeDeclarationSyntax type } field
            || field.Declaration.Variables.Count != 1)
        {
            return;
        }

        var variable = field.Declaration.Variables[0];
        if (!FixedBufferFieldAnalyzer.TryGetBufferSize(variable, model, context.CancellationToken, out var size)
            || model.GetDeclaredSymbol(variable, context.CancellationToken) is not IFieldSymbol symbol)
        {
            return;
        }

        // A fixed buffer is a pointer-like value. Any use of the field may stop compiling, so offer
        // the fix only for a field that nothing reads or writes.
        var references = await SymbolFinder
            .FindReferencesAsync(symbol, context.Document.Project.Solution, context.CancellationToken)
            .ConfigureAwait(false);
        if (references.Any(reference => reference.Locations.Any()))
        {
            return;
        }

        var bufferTypeName = variable.Identifier.ValueText + "Buffer";
        if (type.Members.Any(member => HasName(member, bufferTypeName)))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Replace with an inline array",
                _ => Task.FromResult(ApplyFix(context.Document, root, field, type, size, bufferTypeName)),
                nameof(FixedBufferFieldCodeFixProvider)),
            diagnostic);
    }

    private static Document ApplyFix(
        Document document,
        SyntaxNode root,
        FieldDeclarationSyntax field,
        TypeDeclarationSyntax type,
        int size,
        string bufferTypeName)
    {
        var variable = field.Declaration.Variables[0];
        var newField = BuildField(field, bufferTypeName, variable.Identifier.ValueText);
        var indent = GetIndent(field);
        var bufferStruct = BuildBufferStruct(field, bufferTypeName, size, indent);

        var index = type.Members.IndexOf(field);
        var members = type.Members.Replace(field, newField).Insert(index + 1, bufferStruct);

        return document.WithSyntaxRoot(root.ReplaceNode(type, type.WithMembers(members)));
    }

    private static bool HasName(MemberDeclarationSyntax member, string name)
    {
        return member switch
        {
            BaseTypeDeclarationSyntax typeMember => string.Equals(typeMember.Identifier.ValueText, name, StringComparison.Ordinal),
            FieldDeclarationSyntax fieldMember => fieldMember.Declaration.Variables.Any(variable =>
                string.Equals(variable.Identifier.ValueText, name, StringComparison.Ordinal)),
            BasePropertyDeclarationSyntax => false,
            MethodDeclarationSyntax method => string.Equals(method.Identifier.ValueText, name, StringComparison.Ordinal),
            _ => false,
        };
    }

    private static string GetIndent(FieldDeclarationSyntax field)
    {
        return field.GetLeadingTrivia()
            .LastOrDefault(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            .ToFullString();
    }

    private static FieldDeclarationSyntax BuildField(
        FieldDeclarationSyntax field,
        string bufferTypeName,
        string fieldName)
    {
        var parsed = SyntaxFactory.ParseMemberDeclaration(bufferTypeName + " " + fieldName + ";");

        if (parsed is not FieldDeclarationSyntax declaration)
        {
            return field;
        }

        var modifiers = SyntaxFactory.TokenList(
            field.Modifiers.Where(modifier => !modifier.IsKind(SyntaxKind.FixedKeyword)));

        return declaration.WithModifiers(modifiers).WithTriviaFrom(field);
    }

    // The buffer struct is nested in the containing type, so a name only has to be unique there.
    // It takes the accessibility of the field, so a public field never exposes a less visible type.
    private static MemberDeclarationSyntax BuildBufferStruct(
        FieldDeclarationSyntax field,
        string bufferTypeName,
        int size,
        string indent)
    {
        var accessibility = string.Join(
            " ",
            field.Modifiers
                .Where(modifier => modifier.IsKind(SyntaxKind.PublicKeyword)
                    || modifier.IsKind(SyntaxKind.InternalKeyword)
                    || modifier.IsKind(SyntaxKind.ProtectedKeyword)
                    || modifier.IsKind(SyntaxKind.PrivateKeyword))
                .Select(modifier => modifier.ValueText));
        var modifierPrefix = accessibility.Length == 0 ? string.Empty : accessibility + " ";

        var text = indent + "[System.Runtime.CompilerServices.InlineArray("
            + size.ToString(CultureInfo.InvariantCulture)
            + ")]\n"
            + indent
            + modifierPrefix
            + "struct "
            + bufferTypeName
            + "\n"
            + indent
            + "{\n"
            + indent
            + "    private "
            + field.Declaration.Type.WithoutTrivia().ToFullString()
            + " _element0;\n"
            + indent
            + "}";

        var parsed = SyntaxFactory.ParseMemberDeclaration(text) ?? SyntaxFactory.StructDeclaration(bufferTypeName);

        return parsed
            .WithLeadingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.LineFeed, SyntaxFactory.Whitespace(indent)))
            .WithTrailingTrivia(SyntaxFactory.LineFeed);
    }
}
