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
///     <c lang="csharp">FileInfo.Exists</c> or <c lang="csharp">File.Exists(path)</c> and then reads the file
///     without a try/catch guard. Between the check and the read, another process can delete or replace the file.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FileInfoToctouAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128056";

    private const string FileMetadataName = "System.IO.File";
    private const string FileSystemInfoMetadataName = "System.IO.FileSystemInfo";

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

        context.RegisterCompilationStartAction(startContext =>
        {
            // The rule names System.IO types. Without them the members below belong to some other
            // type, so the rule must not run at all.
            var compilation = startContext.Compilation;
            var fileType = compilation.GetTypeByMetadataName(FileMetadataName);
            var fileSystemInfoType = compilation.GetTypeByMetadataName(FileSystemInfoMetadataName);
            if (fileType is null || fileSystemInfoType is null)
            {
                return;
            }

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeMethod(nodeContext, fileType, fileSystemInfoType),
                SyntaxKind.MethodDeclaration);
        });
    }

    private static void AnalyzeMethod(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol fileType,
        INamedTypeSymbol fileSystemInfoType)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        var guardedValues = CollectGuardedValues(context, method, fileType, fileSystemInfoType);
        if (guardedValues.Count == 0)
        {
            return;
        }

        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (!IsFileReadCall(context, invocation, fileType))
            {
                continue;
            }

            var guardedValue = GetGuardedValue(context, invocation);
            if (guardedValue is null || !guardedValues.Contains(guardedValue))
            {
                continue;
            }

            if (IsInsideTryCatch(invocation, method))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), guardedValue.Name));
        }
    }

    private static HashSet<ISymbol> CollectGuardedValues(
        SyntaxNodeAnalysisContext context,
        MethodDeclarationSyntax method,
        INamedTypeSymbol fileType,
        INamedTypeSymbol fileSystemInfoType)
    {
        var result = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var memberAccess in method.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            if (!string.Equals(memberAccess.Name.Identifier.ValueText, "Exists", StringComparison.Ordinal))
            {
                continue;
            }

            // File.Exists(path) guards the path value passed in. fileInfo.Exists is a property read
            // that guards the instance, so the receiver names the guarded thing.
            if (memberAccess.Parent is InvocationExpressionSyntax invocation)
            {
                if (ResolvesTo(context, invocation, fileType))
                {
                    AddSymbol(result, GetGuardedValue(context, invocation));
                }

                continue;
            }

            if (IsFileSystemInfoExists(context, memberAccess, fileSystemInfoType))
            {
                AddSymbol(result, GetGuardedValue(context, memberAccess.Expression));
            }
        }

        return result;
    }

    private static void AddSymbol(HashSet<ISymbol> target, ISymbol? symbol)
    {
        if (symbol is not null)
        {
            target.Add(symbol);
        }
    }

    private static bool IsFileSystemInfoExists(
        SyntaxNodeAnalysisContext context,
        MemberAccessExpressionSyntax memberAccess,
        INamedTypeSymbol fileSystemInfoType)
    {
        return context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol
                   is IPropertySymbol property
               && DerivesFrom(property.ContainingType, fileSystemInfoType);
    }

    private static bool IsFileReadCall(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol fileType)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess
               && FileReadMethods.Contains(memberAccess.Name.Identifier.ValueText)
               && ResolvesTo(context, invocation, fileType);
    }

    // The guarded value for File.Exists(path) and for File.ReadAllText(path) is the path argument.
    private static ISymbol? GetGuardedValue(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        return invocation.ArgumentList.Arguments.Count == 0
            ? null
            : GetGuardedValue(context, invocation.ArgumentList.Arguments[0].Expression);
    }

    private static ISymbol? GetGuardedValue(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        // fileInfo.FullName names the same file as fileInfo, so the receiver is the guarded value.
        var target = expression is MemberAccessExpressionSyntax member ? member.Expression : expression;
        return context.SemanticModel.GetSymbolInfo(target, context.CancellationToken).Symbol;
    }

    private static bool ResolvesTo(SyntaxNodeAnalysisContext context, SyntaxNode node, INamedTypeSymbol target)
    {
        return context.SemanticModel.GetSymbolInfo(node, context.CancellationToken).Symbol is IMethodSymbol method
               && SymbolEqualityComparer.Default.Equals(method.ContainingType, target);
    }

    private static bool DerivesFrom(INamedTypeSymbol? type, INamedTypeSymbol baseType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
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
