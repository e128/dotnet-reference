using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Unsafe;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullByRefAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128127";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not create a null managed reference",
        "A null byref has no valid referent. Use a null object, an empty span, or a nullable value instead.",
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
        context.RegisterCompilationStartAction(start =>
        {
            var unsafeType = MetadataTypes.Find(start.Compilation, "System.Runtime.CompilerServices.Unsafe");
            if (unsafeType.IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, unsafeType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> unsafeType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (InvocationNames.GetSimpleName(invocation) is not { } memberAccess)
        {
            return;
        }

        var name = memberAccess.Identifier.ValueText;
        var isNullRef = string.Equals(name, "NullRef", StringComparison.Ordinal);
        var isNullAsRef = string.Equals(name, "AsRef", StringComparison.Ordinal) && HasNullPointer(invocation);

        if (!isNullRef && !isNullAsRef)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !MetadataTypes.Contains(unsafeType, method.ContainingType))
        {
            return;
        }

        if (isNullRef && IsReturnedAsSentinel(invocation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    // `return ref Unsafe.NullRef<T>();` paired with Unsafe.IsNullRef is the documented "not found" idiom
    // (see CollectionsMarshal.GetValueRefOrNullRef), so a ref return of NullRef is not a defect.
    private static bool IsReturnedAsSentinel(InvocationExpressionSyntax invocation)
    {
        var reference = invocation.FirstAncestorOrSelf<RefExpressionSyntax>();

        return reference?.Parent is ReturnStatementSyntax or ArrowExpressionClauseSyntax;
    }

    private static bool HasNullPointer(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        var expression = invocation.ArgumentList.Arguments[0].Expression;

        return expression switch
        {
            LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.NullLiteralExpression)
                || literal.IsKind(SyntaxKind.DefaultLiteralExpression),
            DefaultExpressionSyntax => true,
            CastExpressionSyntax { Type: PointerTypeSyntax, Expression: LiteralExpressionSyntax value } =>
                value.IsKind(SyntaxKind.NullLiteralExpression) || value.Token.Value is 0 or 0u or 0L or 0UL,
            _ => false,
        };
    }
}
