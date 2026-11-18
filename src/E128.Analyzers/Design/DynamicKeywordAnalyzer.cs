using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128232: The contextual keyword <c lang="csharp">dynamic</c> in a type
///     position defeats compile-time member checks, trimming, and AOT safety.
/// </summary>
/// <remarks>
///     The rule reports an identifier named <c lang="csharp">dynamic</c> that occupies a
///     type position. <c lang="csharp">SyntaxFacts.IsInNamespaceOrTypeContext</c> answers
///     the type-position question, so no parent-kind list runs. The semantic model keeps
///     the rule silent when the identifier binds to a user-defined type named
///     <c lang="csharp">dynamic</c>, a using alias, a namespace, or a variable. No code fix
///     ships, because a replacement type carries application intent.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DynamicKeywordAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128232";

    private const string DynamicKeyword = "dynamic";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "The dynamic keyword defeats compile-time type checking",
        "The dynamic keyword defeats compile-time checking, trimming, and AOT. Use a concrete type so the compiler checks each member.",
        "E128-Design",
        DiagnosticSeverity.Warning,
        true,
        "dynamic dispatches member access at runtime through the binder, so the compiler checks nothing. " +
        "A concrete type restores compile-time checks and keeps trimming and AOT sound. " +
        "No code fix applies, because the replacement type carries application intent.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeIdentifier, SyntaxKind.IdentifierName);
    }

    private static void AnalyzeIdentifier(SyntaxNodeAnalysisContext context)
    {
        var identifier = (IdentifierNameSyntax)context.Node;

        if (!string.Equals(identifier.Identifier.ValueText, DynamicKeyword, StringComparison.Ordinal)
            || !IsTypePosition(identifier))
        {
            return;
        }

        var type = context.SemanticModel.GetTypeInfo(identifier, context.CancellationToken).Type;

        if (type is null
            || !SymbolEqualityComparer.Default.Equals(type, context.Compilation.DynamicType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation()));
    }

    /// <summary>
    ///     True when the identifier occupies a type position. The left side of a qualified
    ///     name and a using directive with no alias never hold the keyword form.
    /// </summary>
    internal static bool IsTypePosition(IdentifierNameSyntax identifier)
    {
        return identifier.Parent is not (QualifiedNameSyntax or AliasQualifiedNameSyntax)
and not UsingDirectiveSyntax { Alias: null }
&& SyntaxFacts.IsInNamespaceOrTypeContext(identifier);
    }
}
