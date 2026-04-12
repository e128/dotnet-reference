using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace E128.Analyzers.Reliability;

/// <summary>
/// E128051: Flags <see langword="async"/> methods that call an <c>HttpClient</c> method
/// and contain a broad <c>catch (Exception)</c> clause without a preceding
/// <c>catch (OperationCanceledException)</c> in the same try block.
/// Swallowing cancellation silently breaks cooperative cancellation.
/// </summary>
/// <remarks>
/// No code fix is provided — adding a catch clause with correct handler logic is too
/// context-dependent (rethrow vs log-and-rethrow vs propagate cancellation token).
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HttpClientMissingOceCatchAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128051";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Broad catch in async HttpClient method missing OperationCanceledException handler",
        messageFormat: "Broad catch (Exception) swallows OperationCanceledException — add catch (OperationCanceledException) before this catch to handle cancellation explicitly",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An async method that calls HttpClient methods and uses a broad catch (Exception) " +
            "must also catch OperationCanceledException before the broad catch. " +
            "Without it, task cancellation is silently swallowed instead of propagated to the caller.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        if (!method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)))
        {
            return;
        }

        if (method.Body is null)
        {
            return;
        }

        foreach (var tryStatement in method.Body.DescendantNodes().OfType<TryStatementSyntax>())
        {
            if (!BlockContainsHttpClientCall(tryStatement.Block))
            {
                continue;
            }

            for (var i = 0; i < tryStatement.Catches.Count; i++)
            {
                var catchClause = tryStatement.Catches[i];

                if (!IsBroadExceptionCatch(catchClause))
                {
                    continue;
                }

                var precedingCatches = tryStatement.Catches.Take(i);
                if (precedingCatches.Any(IsOperationCanceledExceptionCatch))
                {
                    continue;
                }

                var span = TextSpan.FromBounds(
                    catchClause.CatchKeyword.SpanStart,
                    catchClause.Declaration?.CloseParenToken.Span.End ?? catchClause.CatchKeyword.Span.End);

                context.ReportDiagnostic(Diagnostic.Create(Rule, Location.Create(catchClause.SyntaxTree, span)));
            }
        }
    }

    private static bool BlockContainsHttpClientCall(SyntaxNode block) =>
        block.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(inv => inv.Expression is MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name }
                && IsHttpClientMethod(name.Identifier.Text));

    private static bool IsHttpClientMethod(string name) => name switch
    {
        "GetAsync" or "PostAsync" or "SendAsync" or "PutAsync" or "DeleteAsync" => true,
        _ => false,
    };

    private static bool IsBroadExceptionCatch(CatchClauseSyntax catchClause)
    {
        // A when-filtered catch is intentionally restricted — not considered broad
        if (catchClause.Filter is not null)
        {
            return false;
        }

        if (catchClause.Declaration is null)
        {
            return true; // bare catch { } catches everything
        }

        return IsExceptionTypeName(catchClause.Declaration.Type);
    }

    private static bool IsOperationCanceledExceptionCatch(CatchClauseSyntax catchClause) =>
        catchClause.Declaration is not null
        && IsNamedType(catchClause.Declaration.Type, "OperationCanceledException");

    private static bool IsExceptionTypeName(TypeSyntax typeSyntax) =>
        IsNamedType(typeSyntax, "Exception");

    private static bool IsNamedType(TypeSyntax typeSyntax, string simpleName) =>
        typeSyntax switch
        {
            IdentifierNameSyntax id => string.Equals(id.Identifier.Text, simpleName, StringComparison.Ordinal),
            QualifiedNameSyntax qualified => string.Equals(qualified.Right.Identifier.Text, simpleName, StringComparison.Ordinal),
            _ => false,
        };
}
