using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace E128.Analyzers.Testing;

/// <summary>
///     E128097: Detects <c>//</c> and <c>///</c> comments inside a type that declares at
///     least one xUnit <c>[Fact]</c> or <c>[Theory]</c> method. CLAUDE.md § C# Language
///     Rules bans comments in test code — well-named test methods and assertions already
///     say what they check.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestCodeCommentAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128097";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Comment inside test code",
        "Test code carries a comment — remove it, or express the intent through a better test or method name",
        "Testing",
        DiagnosticSeverity.Warning,
        true,
        "A type that declares an xUnit [Fact] or [Theory] method should carry no // or /// comments. " +
        "CLAUDE.md § C# Language Rules bans comments in test code — the test name and assertions already say what is checked.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        if (!HasXunitTestMethod(classDeclaration, context))
        {
            return;
        }

        foreach (var trivia in classDeclaration.DescendantTrivia())
        {
            if (!IsCommentTrivia(trivia))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, TrimmedLocation(trivia)));
        }
    }

    // A structured trivia's narrow Span excludes its own leading trivia — for a doc comment
    // that leading trivia IS the "///" exterior marker, so Span alone reports a location that
    // starts after it. FullSpan includes the marker; trimming only the trailing newline
    // (present on doc comments, absent on plain // comments) yields the visible comment text.
    private static Location TrimmedLocation(SyntaxTrivia trivia)
    {
        var sourceText = trivia.SyntaxTree!.GetText();
        var text = sourceText.ToString(trivia.FullSpan).TrimEnd('\r', '\n');
        return Location.Create(trivia.SyntaxTree, new TextSpan(trivia.FullSpan.Start, text.Length));
    }

    private static bool IsCommentTrivia(SyntaxTrivia trivia)
    {
        return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
               || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
               || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
               || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
    }

    private static bool HasXunitTestMethod(ClassDeclarationSyntax classDeclaration, SyntaxNodeAnalysisContext context)
    {
        foreach (var method in classDeclaration.Members.OfType<MethodDeclarationSyntax>())
        {
            foreach (var attrList in method.AttributeLists)
            {
                foreach (var attribute in attrList.Attributes)
                {
                    if (IsFactOrTheoryAttribute(attribute, context))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsFactOrTheoryAttribute(AttributeSyntax attribute, SyntaxNodeAnalysisContext context)
    {
        var name = attribute.Name.ToString();
        if (!string.Equals(name, "Fact", StringComparison.Ordinal)
            && !string.Equals(name, "Theory", StringComparison.Ordinal)
            && !string.Equals(name, "FactAttribute", StringComparison.Ordinal)
            && !string.Equals(name, "TheoryAttribute", StringComparison.Ordinal))
        {
            return false;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol;
        return symbol is IMethodSymbol ctor
               && ctor.ContainingType is { } containingType
               && (string.Equals(containingType.Name, "FactAttribute", StringComparison.Ordinal)
                   || string.Equals(containingType.Name, "TheoryAttribute", StringComparison.Ordinal))
               && IsXunitNamespace(containingType.ContainingNamespace);
    }

    private static bool IsXunitNamespace(INamespaceSymbol? ns)
    {
        return ns is not null && string.Equals(ns.ToDisplayString(), "Xunit", StringComparison.Ordinal);
    }
}
