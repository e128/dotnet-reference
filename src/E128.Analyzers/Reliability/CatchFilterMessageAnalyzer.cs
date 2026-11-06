using System;
using System.Collections.Immutable;
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

        // Bind by symbol, so a lambda parameter or a local with the same name is not the catch variable.
        var variable = context.SemanticModel.GetDeclaredSymbol(catchClause.Declaration, context.CancellationToken);
        if (variable is null)
        {
            return;
        }

        foreach (var node in filter.FilterExpression.DescendantNodesAndSelf())
        {
            if (IsFormattedMemberAccess(node, variable, context, out var location, out var name))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, name));
            }
        }
    }

    private static bool IsFormattedMemberAccess(
        SyntaxNode node,
        ISymbol variable,
        SyntaxNodeAnalysisContext context,
        out Location location,
        out string name)
    {
        location = Location.None;
        name = string.Empty;

        ExpressionSyntax receiver;
        Location memberLocation;

        switch (node)
        {
            case MemberAccessExpressionSyntax memberAccess:
                name = memberAccess.Name.Identifier.ValueText;
                receiver = memberAccess.Expression;
                memberLocation = memberAccess.GetLocation();
                break;

            // A conditional access such as "ex?.Message" holds the receiver on the conditional node.
            case MemberBindingExpressionSyntax binding
                when binding.Parent is ConditionalAccessExpressionSyntax conditional:
                name = binding.Name.Identifier.ValueText;
                receiver = conditional.Expression;
                memberLocation = conditional.GetLocation();
                break;

            default:
                return false;
        }

        if (!FormattedMembers.Contains(name) || receiver is not IdentifierNameSyntax identifier)
        {
            return false;
        }

        if (!SymbolEqualityComparer.Default.Equals(
                context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol,
                variable))
        {
            return false;
        }

        location = memberLocation;
        return true;
    }
}
