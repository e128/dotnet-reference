using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
        "File existence check TOCTOU race condition",
        "File read on '{0}' follows an existence check without a try/catch guard. Another process may delete the file between the check and the read.",
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

            var guardedValue = GetGuardedValue(context, invocation, fileSystemInfoType);
            if (guardedValue is null || !guardedValues.Contains(guardedValue))
            {
                continue;
            }

            if (IsInsideTryCatch(invocation, method))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), DisplayName(guardedValue)));
        }
    }

    private static HashSet<GuardedValue> CollectGuardedValues(
        SyntaxNodeAnalysisContext context,
        MethodDeclarationSyntax method,
        INamedTypeSymbol fileType,
        INamedTypeSymbol fileSystemInfoType)
    {
        var result = new HashSet<GuardedValue>(GuardedValueComparer.Instance);

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
                    AddSymbol(result, GetGuardedValue(context, invocation, fileSystemInfoType));
                }

                continue;
            }

            if (IsFileSystemInfoExists(context, memberAccess, fileSystemInfoType))
            {
                AddSymbol(result, GetGuardedValue(context, memberAccess.Expression, fileSystemInfoType));
            }
        }

        return result;
    }

    private static void AddSymbol(HashSet<GuardedValue> target, GuardedValue? value)
    {
        if (value is not null)
        {
            target.Add(value);
        }
    }

    private static bool IsFileSystemInfoExists(
        SyntaxNodeAnalysisContext context,
        MemberAccessExpressionSyntax memberAccess,
        INamedTypeSymbol fileSystemInfoType)
    {
        return context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol
                   is IPropertySymbol property
               && TypeSymbols.DerivesFrom(property.ContainingType, fileSystemInfoType);
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

    /// <summary>
    ///     The guarded storage location and, for a member, the member that holds it. A plain class
    ///     because netstandard2.0 analysis assemblies have no IsExternalInit type for records.
    /// </summary>
    private sealed class GuardedValue
    {
        private readonly int cachedHash;

        public GuardedValue(ISymbol? receiver, ISymbol? member)
        {
            Receiver = receiver;
            Member = member;

            // Compute the hash here instead of in GetHashCode(). A seal-side rule bans a custom
            // GetHashCode body from reading SymbolEqualityComparer.Default.
            var receiverHash = receiver is null ? 0 : SymbolEqualityComparer.Default.GetHashCode(receiver);
            var memberHash = member is null ? 0 : SymbolEqualityComparer.Default.GetHashCode(member);
            cachedHash = unchecked((receiverHash * 397) ^ memberHash);
        }

        public ISymbol? Receiver { get; }

        public ISymbol? Member { get; }

        public string Name => Member?.Name ?? Receiver?.Name ?? "<unknown>";

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is GuardedValue other
                   && SymbolEqualityComparer.Default.Equals(other.Receiver, Receiver)
                   && SymbolEqualityComparer.Default.Equals(other.Member, Member);
        }

        public override int GetHashCode()
        {
            return cachedHash;
        }
    }

    private sealed class GuardedValueComparer : IEqualityComparer<GuardedValue>
    {
        public static readonly GuardedValueComparer Instance = new();

        public bool Equals(GuardedValue? x, GuardedValue? y)
        {
            return SymbolEqualityComparer.Default.Equals(x?.Receiver, y?.Receiver)
                   && SymbolEqualityComparer.Default.Equals(x?.Member, y?.Member);
        }

        public int GetHashCode(GuardedValue obj)
        {
            return obj.GetHashCode();
        }
    }

    private static string DisplayName(GuardedValue value)
    {
        return value.Name;
    }

    // The guarded value for File.Exists(path) and for File.ReadAllText(path) is the path argument.
    private static GuardedValue? GetGuardedValue(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol fileSystemInfoType)
    {
        return invocation.ArgumentList.Arguments.Count == 0
            ? null
            : GetGuardedValue(context, invocation.ArgumentList.Arguments[0].Expression, fileSystemInfoType);
    }

    private static GuardedValue? GetGuardedValue(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression,
        INamedTypeSymbol fileSystemInfoType)
    {
        // A call names no storage location. Two calls to the same helper bind to one method symbol,
        // so a call argument cannot identify the guarded value.
        if (expression is InvocationExpressionSyntax)
        {
            return null;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol;
        if (symbol is null)
        {
            return null;
        }

        var receiverSymbol = expression is MemberAccessExpressionSyntax member
            ? context.SemanticModel.GetSymbolInfo(member.Expression, context.CancellationToken).Symbol
            : null;

        // fileInfo.FullName names the same file as fileInfo, so a member of a FileSystemInfo reduces
        // to the same guarded value that a read of the receiver itself produces. Any other member
        // keys on the (receiver, member) pair, so config.Input, config.Output, and the same member
        // on a sibling instance all stay distinct.
        return symbol is IPropertySymbol property
               && TypeSymbols.DerivesFrom(property.ContainingType, fileSystemInfoType)
               && expression is MemberAccessExpressionSyntax fsMember
            ? GetGuardedValue(context, fsMember.Expression, fileSystemInfoType)
            : new GuardedValue(receiverSymbol, symbol);
    }

    private static bool ResolvesTo(SyntaxNodeAnalysisContext context, SyntaxNode node, INamedTypeSymbol target)
    {
        return context.SemanticModel.GetSymbolInfo(node, context.CancellationToken).Symbol is IMethodSymbol method
               && SymbolEqualityComparer.Default.Equals(method.ContainingType, target);
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
