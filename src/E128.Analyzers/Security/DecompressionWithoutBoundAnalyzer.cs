using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

/// <summary>
///     E128267: A read loop over a decompression stream writes into a
///     <c lang="csharp">MemoryStream</c> with no running output bound. A crafted archive
///     then expands without limit.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DecompressionWithoutBoundAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128267";

    private const string MemoryStreamMetadataName = "System.IO.MemoryStream";

    private static readonly string[] DecompressionMetadataNames =
    [
        "System.IO.Compression.GZipStream",
        "System.IO.Compression.DeflateStream",
        "System.IO.Compression.BrotliStream",
    ];

    private static readonly string[] ReadNames = ["Read", "ReadAsync"];

    private static readonly string[] WriteNames = ["Write", "WriteAsync", "CopyTo", "CopyToAsync"];

    private static readonly string[] BoundWords = ["max", "limit", "cap", "bound"];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Decompression loop has no output bound",
        "Decompression loop writes into a MemoryStream with no output bound: track the total and stop at a cap",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true,
        "A decompression loop that writes into a MemoryStream with no running total and no entry length test expands an archive bomb without limit. Track the total and stop at a cap.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static start =>
        {
            var memoryStream = start.Compilation.GetTypeByMetadataName(MemoryStreamMetadataName);

            if (memoryStream is null)
            {
                return;
            }

            var decompression = ResolveDecompression(start.Compilation);

            if (decompression.Length is 0)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, decompression, memoryStream),
                SyntaxKind.WhileStatement,
                SyntaxKind.DoStatement,
                SyntaxKind.ForStatement);
        });
    }

    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> decompression,
        INamedTypeSymbol memoryStream)
    {
        if (context.Node is not StatementSyntax loop
            || !HasRead(loop, context, decompression)
            || !HasWrite(loop, context, memoryStream)
            || HasBound(loop))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, loop.GetLocation()));
    }

    private static bool HasRead(
        StatementSyntax loop,
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> decompression)
    {
        foreach (var invocation in loop.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (!IsOneOf(invocation, ReadNames)
                || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
                || ReceiverType(invocation, context) is not { } receiverType)
            {
                continue;
            }

            foreach (var type in decompression)
            {
                if (SymbolEqualityComparer.Default.Equals(receiverType, type))
                {
                    return true;
                }
            }

            if (method.ContainingType is not null && IsDerived(receiverType, method.ContainingType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasWrite(
        StatementSyntax loop,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol memoryStream)
    {
        foreach (var invocation in loop.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (IsOneOf(invocation, WriteNames)
                && ReceiverType(invocation, context) is { } receiverType
                && SymbolEqualityComparer.Default.Equals(receiverType, memoryStream))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasBound(StatementSyntax loop)
    {
        foreach (var condition in Conditions(loop))
        {
            foreach (var comparison in condition.DescendantNodesAndSelf().OfType<BinaryExpressionSyntax>())
            {
                if (IsBoundComparison(comparison))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<ExpressionSyntax> Conditions(StatementSyntax loop)
    {
        if (loop is WhileStatementSyntax whileStatement)
        {
            yield return whileStatement.Condition;
        }
        else if (loop is DoStatementSyntax doStatement)
        {
            yield return doStatement.Condition;
        }
        else if (loop is ForStatementSyntax forStatement && forStatement.Condition is { } condition)
        {
            yield return condition;
        }

        foreach (var ifStatement in loop.DescendantNodes().OfType<IfStatementSyntax>())
        {
            yield return ifStatement.Condition;
        }
    }

    private static bool IsBoundComparison(BinaryExpressionSyntax comparison)
    {
        if (comparison.Kind() is not (SyntaxKind.GreaterThanExpression
            or SyntaxKind.GreaterThanOrEqualExpression
            or SyntaxKind.LessThanExpression
            or SyntaxKind.LessThanOrEqualExpression))
        {
            return false;
        }

        return IsBoundSide(comparison.Left) || IsBoundSide(comparison.Right);
    }

    private static bool IsBoundSide(ExpressionSyntax expression)
    {
        var value = ExpressionUnwrapper.Parentheses(expression);

        if (value is MemberAccessExpressionSyntax access
            && (string.Equals(access.Name.Identifier.ValueText, "Length", StringComparison.Ordinal)
                || string.Equals(access.Name.Identifier.ValueText, "Position", StringComparison.Ordinal)))
        {
            return true;
        }

        if (value is not IdentifierNameSyntax identifier)
        {
            return false;
        }

        foreach (var word in BoundWords)
        {
            if (identifier.Identifier.ValueText.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOneOf(InvocationExpressionSyntax invocation, string[] names)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (string.Equals(access.Name.Identifier.ValueText, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static ITypeSymbol? ReceiverType(
        InvocationExpressionSyntax invocation,
        SyntaxNodeAnalysisContext context)
    {
        return invocation.Expression is MemberAccessExpressionSyntax access
            ? context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type
            : null;
    }

    private static bool IsDerived(ITypeSymbol type, INamedTypeSymbol baseType)
    {
        return TypeSymbols.WalkBaseTypes(
            type,
            current => SymbolEqualityComparer.Default.Equals(current, baseType),
            includeSelf: false);
    }

    private static ImmutableArray<INamedTypeSymbol> ResolveDecompression(Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        foreach (var name in DecompressionMetadataNames)
        {
            if (compilation.GetTypeByMetadataName(name) is { } type)
            {
                builder.Add(type);
            }
        }

        return builder.ToImmutable();
    }
}
