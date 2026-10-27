using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Unsafe;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FixedStringMutationAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128126";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not write to a string through a fixed pointer",
        "A string is immutable. Writing through a char* fixes it in place and breaks the runtime invariant. Build a new string instead.",
        "Unsafe",
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
        context.RegisterSyntaxNodeAction(AnalyzeFixed, SyntaxKind.FixedStatement);
    }

    private static void AnalyzeFixed(SyntaxNodeAnalysisContext context)
    {
        var fixedStatement = (FixedStatementSyntax)context.Node;

        if (fixedStatement.Declaration.Type is not PointerTypeSyntax
            {
                ElementType: PredefinedTypeSyntax { Keyword.ValueText: "char" },
            })
        {
            return;
        }

        foreach (var variable in fixedStatement.Declaration.Variables)
        {
            if (variable.Initializer?.Value is not { } value
                || context.SemanticModel.GetTypeInfo(value, context.CancellationToken).Type?.SpecialType
                    != SpecialType.System_String)
            {
                continue;
            }

            if (WritesThroughPointer(fixedStatement, variable, context))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, fixedStatement.GetLocation()));
                return;
            }
        }
    }

    // Reading a pinned string is the normal interop pattern. Only a write through the pointer
    // (assignment, increment, ref or out) breaks string immutability. A copy of the pointer
    // into another variable is not followed.
    private static bool WritesThroughPointer(
        FixedStatementSyntax fixedStatement,
        VariableDeclaratorSyntax variable,
        SyntaxNodeAnalysisContext context)
    {
        var pointer = context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken);
        if (pointer is null)
        {
            return false;
        }

        foreach (var name in fixedStatement.Statement.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (!SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol,
                    pointer))
            {
                continue;
            }

            if (GetDereference(name) is { } dereference && IsWriteTarget(dereference))
            {
                return true;
            }
        }

        return false;
    }

    // Walks from the pointer name through parentheses and `p + n` to the `p[i]` or `*p` that reads or writes memory.
    private static ExpressionSyntax? GetDereference(IdentifierNameSyntax name)
    {
        ExpressionSyntax current = name;

        while (current.Parent is ParenthesizedExpressionSyntax
               || (current.Parent is BinaryExpressionSyntax binary
                   && binary.IsKind(SyntaxKind.AddExpression)
                   && binary.Left == current))
        {
            current = (ExpressionSyntax)current.Parent;
        }

        return current.Parent switch
        {
            ElementAccessExpressionSyntax element when element.Expression == current => element,
            PrefixUnaryExpressionSyntax indirection when indirection.IsKind(SyntaxKind.PointerIndirectionExpression) => indirection,
            _ => null,
        };
    }

    private static bool IsWriteTarget(ExpressionSyntax access)
    {
        var current = access;
        while (current.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            current = parenthesized;
        }

        return current.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == current,
            PrefixUnaryExpressionSyntax prefix => prefix.IsKind(SyntaxKind.PreIncrementExpression)
                || prefix.IsKind(SyntaxKind.PreDecrementExpression),
            PostfixUnaryExpressionSyntax postfix => postfix.IsKind(SyntaxKind.PostIncrementExpression)
                || postfix.IsKind(SyntaxKind.PostDecrementExpression),
            ArgumentSyntax argument => argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword),
            _ => false,
        };
    }
}
