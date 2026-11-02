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
///     E128097: Detects <c lang="csharp">//</c> and <c lang="csharp">///</c> comments inside a type that declares at
///     least one xUnit <c lang="csharp">[Fact]</c> or <c lang="csharp">[Theory]</c> method. CLAUDE.md § C# Language
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
            // DescendantTrivia includes the declaration's own leading trivia. A license header, or a
            // doc comment on the type itself, sits outside the type body, so the rule leaves it alone.
            if (trivia.SpanStart < classDeclaration.OpenBraceToken.SpanStart || !IsCommentTrivia(trivia))
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
                    if (XunitTestHelper.IsFactOrTheoryAttribute(attribute, context.SemanticModel, context.CancellationToken))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

}
