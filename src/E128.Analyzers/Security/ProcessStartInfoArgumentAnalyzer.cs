using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProcessStartInfoArgumentAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128091";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use ArgumentList instead of a built Arguments string on ProcessStartInfo",
        "ProcessStartInfo.Arguments is assigned a built string instead of using ArgumentList — prefer ArgumentList to avoid shell-quoting/injection risk",
        "Security",
        DiagnosticSeverity.Warning,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
    }

    /// <summary>
    ///   Handles the statement form, <c lang="csharp">psi.Arguments = value;</c>, which carries the same
    ///   hazard as the initializer form. Reading only the object initializer leaves this form
    ///   unreported, and it is the form a caller reaches for when the value is built at run time.
    /// </summary>
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        if (assignment.Left is not MemberAccessExpressionSyntax memberAccess
            || !string.Equals(memberAccess.Name.Identifier.ValueText, "Arguments", StringComparison.Ordinal))
        {
            return;
        }

        var receiverType = context.SemanticModel
            .GetTypeInfo(memberAccess.Expression, context.CancellationToken)
            .Type;

        if (receiverType is null || !IsProcessStartInfoType(receiverType))
        {
            return;
        }

        if (IsEmptyStringValue(context, assignment.Right))
        {
            return;
        }

        if (AssignsArgumentList(context, memberAccess.Expression))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, assignment.GetLocation()));
    }

    private static bool AssignsArgumentList(SyntaxNodeAnalysisContext context, ExpressionSyntax receiver)
    {
        var target = receiver.ToString();
        var scope = context.Node.FirstAncestorOrSelf<MemberDeclarationSyntax>();

        if (scope is null)
        {
            return false;
        }

        foreach (var node in scope.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (node.Left is MemberAccessExpressionSyntax memberAccess
                && string.Equals(memberAccess.Name.Identifier.ValueText, "ArgumentList", StringComparison.Ordinal)
                && string.Equals(memberAccess.Expression.ToString(), target, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;

        if (!IsProcessStartInfo(context, creation))
        {
            return;
        }

        var argumentsAssignment = FindMemberAssignment(creation, "Arguments");
        if (argumentsAssignment is null)
        {
            return;
        }

        if (FindMemberAssignment(creation, "ArgumentList") is not null)
        {
            return;
        }

        if (IsEmptyStringValue(context, argumentsAssignment.Right))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, argumentsAssignment.GetLocation()));
    }

    private static bool IsProcessStartInfo(SyntaxNodeAnalysisContext context, ObjectCreationExpressionSyntax creation)
    {
        var type = context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type;

        return type is not null && IsProcessStartInfoType(type);
    }

    private static bool IsProcessStartInfoType(ITypeSymbol type)
    {
        return string.Equals(type.Name, "ProcessStartInfo", StringComparison.Ordinal)
               && string.Equals(type.ContainingNamespace?.ToDisplayString(), "System.Diagnostics", StringComparison.Ordinal);
    }

    private static AssignmentExpressionSyntax? FindMemberAssignment(ObjectCreationExpressionSyntax creation, string memberName)
    {
        return creation.Initializer?.Expressions
            .OfType<AssignmentExpressionSyntax>()
            .FirstOrDefault(assignment =>
                assignment.Left is IdentifierNameSyntax identifier
                && string.Equals(identifier.Identifier.ValueText, memberName, StringComparison.Ordinal));
    }

    private static bool IsEmptyStringValue(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        var constant = context.SemanticModel.GetConstantValue(expression, context.CancellationToken);

        return (constant.HasValue && constant.Value is string constantText && constantText.Length == 0)
               || (expression is MemberAccessExpressionSyntax memberAccess
                   && string.Equals(memberAccess.Name.Identifier.ValueText, "Empty", StringComparison.Ordinal)
                   && string.Equals(memberAccess.Expression.ToString(), "string", StringComparison.Ordinal));
    }
}
