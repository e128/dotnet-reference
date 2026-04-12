using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DateTimeDirectUseAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128003";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Use TimeProvider instead of DateTime/DateTimeOffset direct access",
        messageFormat: "Use TimeProvider instead of {0}.{1} — inject TimeProvider via DI",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        var memberName = memberAccess.Name.Identifier.ValueText;

        // Quick name filter before semantic work.
        if (!IsTargetMemberName(memberName))
        {
            return;
        }

        // Skip if the access is inside a static field initializer — TimeProvider cannot be
        // injected there and the pattern is intentional (e.g., static readonly baseline fields).
        if (IsInStaticFieldInitializer(memberAccess))
        {
            return;
        }

        // Semantic verification: confirm the containing type is System.DateTime or System.DateTimeOffset.
        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken);
        if (symbolInfo.Symbol is not IPropertySymbol property)
        {
            return;
        }

        var containingType = property.ContainingType;
        if (containingType is null)
        {
            return;
        }

        if (containingType.SpecialType != SpecialType.System_DateTime
            && !IsDateTimeOffset(containingType))
        {
            return;
        }

        var typeName = containingType.Name;
        context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation(), typeName, memberName));
    }

    private static bool IsTargetMemberName(string name) =>
        string.Equals(name, "Now", StringComparison.Ordinal)
        || string.Equals(name, "UtcNow", StringComparison.Ordinal)
        || string.Equals(name, "Today", StringComparison.Ordinal);

    private static bool IsDateTimeOffset(INamedTypeSymbol type) =>
        string.Equals(type.Name, "DateTimeOffset", StringComparison.Ordinal)
        && type.ContainingNamespace is { Name: "System" };

    // Returns true if the node is inside a static field declaration's initializer.
    // Covers patterns such as: private static readonly DateTime _baseline = DateTime.UtcNow
    private static bool IsInStaticFieldInitializer(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is EqualsValueClauseSyntax equalsClause
                && equalsClause.Parent is VariableDeclaratorSyntax
                && equalsClause.Parent.Parent is VariableDeclarationSyntax
                && equalsClause.Parent.Parent.Parent is FieldDeclarationSyntax fieldDecl
                && fieldDecl.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                return true;
            }
        }

        return false;
    }
}
