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
///     E128056: Detects TOCTOU (Time-Of-Check Time-Of-Use) races where code checks
///     <c>FileInfo.Exists</c> and then immediately reads the file without a try/catch guard.
///     Between the check and the read, another process can delete or replace the file.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FileInfoToctouAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128056";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "FileInfo.Exists TOCTOU race condition",
        "File read on '{0}' follows FileInfo.Exists check without try/catch guard — another process may delete the file between the check and the read",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Checking FileInfo.Exists before reading a file creates a TOCTOU race. Guard the read with try/catch(IOException) instead of relying on the existence check.");

    private static readonly HashSet<string> FileReadMethods = new(StringComparer.Ordinal)
    {
        "ReadAllBytes",
        "ReadAllBytesAsync",
        "ReadAllText",
        "ReadAllTextAsync",
        "ReadAllLines",
        "ReadAllLinesAsync",
        "OpenRead",
        "Open"
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

        var existsIdentifiers = CollectExistsIdentifiers(method);
        if (existsIdentifiers.Count == 0)
        {
            return;
        }

        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (!IsFileReadCall(invocation))
            {
                continue;
            }

            var argIdentifier = ExtractFirstArgIdentifier(invocation);
            if (argIdentifier is null || !existsIdentifiers.Contains(argIdentifier))
            {
                continue;
            }

            if (IsInsideTryCatch(invocation, method))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), argIdentifier));
        }
    }

    private static HashSet<string> CollectExistsIdentifiers(MethodDeclarationSyntax method)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in method.DescendantNodes())
        {
            if (node is MemberAccessExpressionSyntax memberAccess
                && string.Equals(memberAccess.Name.Identifier.ValueText, "Exists", StringComparison.Ordinal)
                && memberAccess.Expression is IdentifierNameSyntax identifier)
            {
                result.Add(identifier.Identifier.ValueText);
            }
        }

        return result;
    }

    private static bool IsFileReadCall(InvocationExpressionSyntax invocation)
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

    private static string? ExtractFirstArgIdentifier(InvocationExpressionSyntax invocation)
    {
        var firstArg = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        if (firstArg is null)
        {
            return null;
        }

        // fileInfo.FullName → "fileInfo"
        if (firstArg is MemberAccessExpressionSyntax argMember
            && argMember.Expression is IdentifierNameSyntax argId)
        {
            return argId.Identifier.ValueText;
        }

        // bare path variable
        return firstArg is IdentifierNameSyntax directId ? directId.Identifier.ValueText : null;
    }

    private static bool IsInsideTryCatch(SyntaxNode node, SyntaxNode boundary)
    {
        var current = node.Parent;
        while (current is not null && current != boundary)
        {
            if (current is TryStatementSyntax tryStatement && tryStatement.Catches.Any())
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }
}
