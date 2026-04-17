using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128061: Flags <c>private static readonly T[]</c> and <c>internal static readonly T[]</c> fields
///     where <c>ImmutableArray&lt;T&gt;</c> should be used instead. Arrays are reference types — the
///     <see langword="readonly" /> modifier prevents reassigning the field but callers can still mutate content
///     via the indexer.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MutableStaticReadonlyArrayAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128061";
    internal const string ElementTypeKey = "ElementType";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use ImmutableArray<T> for static readonly arrays",
        "Static readonly array '{0}' should be ImmutableArray<{1}> — readonly prevents reassignment but not content mutation",
        "Design",
        DiagnosticSeverity.Warning,
        true,
        "A 'static readonly T[]' field is not truly immutable — readonly prevents reassignment, " +
        "but callers can still mutate the array via the indexer. Use ImmutableArray<T> from " +
        "System.Collections.Immutable for true immutability.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
    }

    private static void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context)
    {
        var fieldDeclaration = (FieldDeclarationSyntax)context.Node;

        if (!IsStaticReadonly(fieldDeclaration.Modifiers))
        {
            return;
        }

        // Only flag private and internal fields — public fields are covered by API surface concerns
        if (!IsPrivateOrInternal(fieldDeclaration.Modifiers))
        {
            return;
        }

        foreach (var variable in fieldDeclaration.Declaration.Variables)
        {
            if (variable.Initializer is null)
            {
                continue;
            }

            if (context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is not IFieldSymbol fieldSymbol)
            {
                continue;
            }

            if (fieldSymbol.Type is not IArrayTypeSymbol arrayType)
            {
                continue;
            }

            var elementType = arrayType.ElementType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                fieldDeclaration.Declaration.GetLocation(),
                ImmutableDictionary.CreateRange(
                    [
                        new KeyValuePair<string, string?>(ElementTypeKey, elementType)
                    ]),
                variable.Identifier.Text,
                elementType));
        }
    }

    private static bool IsStaticReadonly(SyntaxTokenList modifiers)
    {
        var hasStatic = false;
        var hasReadonly = false;

        foreach (var modifier in modifiers)
        {
            if (modifier.IsKind(SyntaxKind.StaticKeyword))
            {
                hasStatic = true;
            }
            else if (modifier.IsKind(SyntaxKind.ReadOnlyKeyword))
            {
                hasReadonly = true;
            }
        }

        return hasStatic && hasReadonly;
    }

    private static bool IsPrivateOrInternal(SyntaxTokenList modifiers)
    {
        // Default accessibility for class members with no modifier is private
        const int Private = 1;
        const int Internal = 2;
        const int Public = 4;
        const int Protected = 8;

        var access = 0;

        foreach (var modifier in modifiers)
        {
            if (modifier.IsKind(SyntaxKind.PrivateKeyword))
            {
                access |= Private;
            }
            else if (modifier.IsKind(SyntaxKind.InternalKeyword))
            {
                access |= Internal;
            }
            else if (modifier.IsKind(SyntaxKind.PublicKeyword))
            {
                access |= Public;
            }
            else if (modifier.IsKind(SyntaxKind.ProtectedKeyword))
            {
                access |= Protected;
            }
        }

        // No explicit access modifier means private for class members
        return access == 0 || (access & (Private | Internal)) != 0;
    }
}
