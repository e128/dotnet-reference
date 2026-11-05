using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CatchFilterMessageAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128188";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Exception filter reads the message text",
        "The filter reads '{0}', which formats text and can throw before any 'catch' clause runs",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true);

    private static readonly ImmutableHashSet<string> FormattedMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Message",
        "StackTrace",
        "ToString");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeCatchFilter, SyntaxKind.CatchFilterClause);
    }

    private static void AnalyzeCatchFilter(SyntaxNodeAnalysisContext context)
    {
        var filter = (CatchFilterClauseSyntax)context.Node;

        if (filter.Parent is not CatchClauseSyntax catchClause || catchClause.Declaration is null)
        {
            return;
        }

        var variableName = catchClause.Declaration.Identifier.ValueText;
        if (variableName.Length == 0)
        {
            return;
        }

        foreach (var memberAccess in filter.FilterExpression
                     .DescendantNodesAndSelf()
                     .OfType<MemberAccessExpressionSyntax>())
        {
            if (IsFormattedMemberAccess(memberAccess, variableName))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, memberAccess.GetLocation(), memberAccess.Name.Identifier.ValueText));
            }
        }
    }

    private static bool IsFormattedMemberAccess(MemberAccessExpressionSyntax memberAccess, string variableName)
    {
        return FormattedMembers.Contains(memberAccess.Name.Identifier.ValueText)
            && memberAccess.Expression is IdentifierNameSyntax receiver
            && string.Equals(receiver.Identifier.ValueText, variableName, StringComparison.Ordinal);
    }
}
