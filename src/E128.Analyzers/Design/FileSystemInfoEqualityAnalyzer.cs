using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FileSystemInfoEqualityAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128030";

    internal static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Do not compare FileSystemInfo types by reference",
        messageFormat: "'{0}' compares FileSystemInfo by reference — compare .FullName instead",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description:
            "FileInfo and DirectoryInfo do not override Equals or operator==. " +
            "Equality comparisons use reference equality from System.Object, which " +
            "means two FileInfo objects pointing to the same path are never equal. " +
            "Compare .FullName properties instead.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeBinaryExpression,
            SyntaxKind.EqualsExpression,
            SyntaxKind.NotEqualsExpression);
        context.RegisterSyntaxNodeAction(
            AnalyzeInvocation,
            SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeBinaryExpression(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;

        if (IsNullOrDefault(binary.Left) || IsNullOrDefault(binary.Right))
        {
            return;
        }

        var leftType = context.SemanticModel.GetTypeInfo(binary.Left, context.CancellationToken).Type;
        var rightType = context.SemanticModel.GetTypeInfo(binary.Right, context.CancellationToken).Type;

        if (leftType is null || rightType is null)
        {
            return;
        }

        if (!InheritsFromFileSystemInfo(leftType) && !InheritsFromFileSystemInfo(rightType))
        {
            return;
        }

        var operatorText = binary.OperatorToken.Text;
        context.ReportDiagnostic(
            Diagnostic.Create(Rule, binary.OperatorToken.GetLocation(), operatorText));
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (!string.Equals(memberAccess.Name.Identifier.ValueText, "Equals", StringComparison.Ordinal))
        {
            return;
        }

        if (invocation.ArgumentList.Arguments.Count != 1)
        {
            return;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(
            memberAccess.Expression, context.CancellationToken).Type;

        if (receiverType is null || !InheritsFromFileSystemInfo(receiverType))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, memberAccess.Name.GetLocation(), "Equals"));
    }

    private static bool IsNullOrDefault(ExpressionSyntax expression)
    {
        return expression.IsKind(SyntaxKind.NullLiteralExpression)
            || expression.IsKind(SyntaxKind.DefaultLiteralExpression)
            || expression.IsKind(SyntaxKind.DefaultExpression);
    }

    private static bool InheritsFromFileSystemInfo(ITypeSymbol type)
    {
        var current = type;
        while (current is not null)
        {
            if (IsFileSystemInfoType(current))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static bool IsFileSystemInfoType(ITypeSymbol type)
    {
        return string.Equals(type.Name, "FileSystemInfo", StringComparison.Ordinal)
            && type.ContainingNamespace is { Name: "IO" }
                and { ContainingNamespace: { Name: "System" } and { ContainingNamespace.IsGlobalNamespace: true } };
    }
}
