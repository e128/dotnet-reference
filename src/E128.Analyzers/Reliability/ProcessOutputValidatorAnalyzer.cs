using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128101: Detects reading a declared output file after Process.WaitForExit without
///     verifying the file was created. A failed binary, wrong CLI flag, or disk-full
///     condition leaves the output missing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProcessOutputValidatorAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128101";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Verify process output file exists before use",
        "Output file '{0}' is read after process exit without an existence check — the process may have failed to create it",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "After Process.WaitForExit, verify the declared output file exists before reading it.");

    private static readonly HashSet<string> FileReadMethods = new(StringComparer.Ordinal)
    {
        "ReadAllBytes",
        "ReadAllBytesAsync",
        "ReadAllText",
        "ReadAllTextAsync",
        "ReadAllLines",
        "ReadAllLinesAsync",
        "OpenRead",
        "Open",
        "OpenText"
    };

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

        var waitPositions = new List<int>();
        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (IsProcessWaitCall(context, invocation))
            {
                waitPositions.Add(invocation.SpanStart);
            }
        }

        if (waitPositions.Count == 0)
        {
            return;
        }

        var lastWait = waitPositions.Max();
        var checkedPositions = CollectExistsCheckPositions(method);

        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (!IsFileReadCall(invocation) || invocation.SpanStart <= lastWait)
            {
                continue;
            }

            var pathRoot = ExtractPathRoot(invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression);
            if (pathRoot is null
                || (checkedPositions.TryGetValue(pathRoot, out var checkPosition) && checkPosition < invocation.SpanStart))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), pathRoot));
        }
    }

    private static bool IsProcessWaitCall(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        return context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method
               && (string.Equals(method.Name, "WaitForExit", StringComparison.Ordinal)
                   || string.Equals(method.Name, "WaitForExitAsync", StringComparison.Ordinal))
               && string.Equals(method.ContainingType?.Name, "Process", StringComparison.Ordinal)
               && string.Equals(method.ContainingType?.ContainingNamespace?.ToDisplayString(), "System.Diagnostics", StringComparison.Ordinal);
    }

    private static Dictionary<string, int> CollectExistsCheckPositions(MethodDeclarationSyntax method)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var node in method.DescendantNodes())
        {
            if (node is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Exists" } memberAccess
                && memberAccess.Expression is IdentifierNameSyntax identifier)
            {
                // outputFile.Exists (FileInfo style)
                result[identifier.Identifier.ValueText] = memberAccess.SpanStart;
            }
            else if (node is InvocationExpressionSyntax invocation
                     && invocation.Expression is MemberAccessExpressionSyntax access
                     && string.Equals(access.Name.Identifier.ValueText, "Exists", StringComparison.Ordinal)
                     && access.Expression is IdentifierNameSyntax { Identifier.ValueText: "File" })
            {
                // File.Exists(outputFile)
                var root = ExtractPathRoot(invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression);
                if (root is not null)
                {
                    result[root] = invocation.SpanStart;
                }
            }
        }

        return result;
    }

    internal static bool IsFileReadCall(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess
               && FileReadMethods.Contains(memberAccess.Name.Identifier.ValueText)
               && (memberAccess.Expression is IdentifierNameSyntax { Identifier.ValueText: "File" }
                   || IsQualifiedFileAccess(memberAccess.Expression));
    }

    private static bool IsQualifiedFileAccess(ExpressionSyntax expression)
    {
        return expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "File" };
    }

    internal static string? ExtractPathRoot(ExpressionSyntax? expression)
    {
        if (expression is null)
        {
            return null;
        }

        // outputFile.FullName → "outputFile"
        if (expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax memberIdentifier })
        {
            return memberIdentifier.Identifier.ValueText;
        }

        // bare path variable
        return expression is IdentifierNameSyntax directIdentifier ? directIdentifier.Identifier.ValueText : null;
    }
}
