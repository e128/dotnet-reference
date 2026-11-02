using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Testing;

/// <summary>
///     E128054: Flags classes that call <c lang="csharp">Path.GetTempPath()</c> in a field initializer,
///     property initializer, or constructor without implementing <c lang="csharp">IDisposable</c>,
///     <c lang="csharp">IAsyncDisposable</c>, or xUnit's <c lang="csharp">IAsyncLifetime</c>.
///     Temp directories allocated at class level leak without a cleanup interface.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TempDirCleanupAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128054";

    internal const string PropMemberName = "MemberName";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Class creates temp directory without cleanup interface",
        "Class '{0}' calls Path.GetTempPath() but does not implement IDisposable, IAsyncDisposable, or IAsyncLifetime",
        "Testing",
        DiagnosticSeverity.Warning,
        true,
        "Classes that allocate temp directories in field initializers or constructors must implement a cleanup interface to avoid resource leaks.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!IsGetTempPathCall(invocation, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        if (!IsInInitializerOrConstructor(invocation))
        {
            return;
        }

        var classDecl = GetContainingClass(invocation);
        if (classDecl is null)
        {
            return;
        }

        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl, context.CancellationToken);
        if (classSymbol is null)
        {
            return;
        }

        if (HasCleanupInterface(classSymbol))
        {
            return;
        }

        // The fix names this member in the generated cleanup call, so the name travels with the
        // diagnostic instead of a hardcoded guess.
        var properties = ImmutableDictionary<string, string?>.Empty;
        if (GetAssignedMemberName(context, invocation) is { } memberName)
        {
            properties = properties.Add(PropMemberName, memberName);
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, classDecl.Identifier.GetLocation(), properties, classSymbol.Name));
    }

    private static string? GetAssignedMemberName(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        var symbol = invocation.FirstAncestorOrSelf<EqualsValueClauseSyntax>()?.Parent switch
        {
            PropertyDeclarationSyntax property => context.SemanticModel.GetDeclaredSymbol(property, context.CancellationToken),
            VariableDeclaratorSyntax declarator => context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken),
            _ => GetAssignedTargetSymbol(context, invocation)
        };

        // A local variable or a parameter is not a member. The generated Dispose call cannot reach
        // it, so the fix would not compile.
        return symbol is IFieldSymbol or IPropertySymbol ? symbol.Name : null;
    }

    private static ISymbol? GetAssignedTargetSymbol(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        return invocation.FirstAncestorOrSelf<AssignmentExpressionSyntax>()?.Left is { } target
            ? context.SemanticModel.GetSymbolInfo(target, context.CancellationToken).Symbol
            : null;
    }

    private static bool IsGetTempPathCall(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellationToken);

        return symbolInfo.Symbol is IMethodSymbol method
               && string.Equals(method.Name, "GetTempPath", StringComparison.Ordinal)
               && method.ContainingType is { } containingType
               && string.Equals(containingType.Name, "Path", StringComparison.Ordinal)
               && string.Equals(containingType.ContainingNamespace?.ToDisplayString(), "System.IO", StringComparison.Ordinal);
    }

    private static bool IsInInitializerOrConstructor(SyntaxNode node)
    {
        var current = node.Parent;
        while (current is not null)
        {
            if (current is ConstructorDeclarationSyntax)
            {
                return true;
            }

            if (current is EqualsValueClauseSyntax
                && (current.Parent is PropertyDeclarationSyntax
                    || current.Parent is VariableDeclaratorSyntax
                    {
                        Parent: VariableDeclarationSyntax { Parent: FieldDeclarationSyntax }
                    }))
            {
                return true;
            }

            if (current is MethodDeclarationSyntax
                or LocalFunctionStatementSyntax
                or AccessorDeclarationSyntax)
            {
                return false;
            }

            current = current.Parent;
        }

        return false;
    }

    private static ClassDeclarationSyntax? GetContainingClass(SyntaxNode node)
    {
        var current = node.Parent;
        while (current is not null)
        {
            if (current is ClassDeclarationSyntax classDecl)
            {
                return classDecl;
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool HasCleanupInterface(INamedTypeSymbol classSymbol)
    {
        foreach (var iface in classSymbol.AllInterfaces)
        {
            var name = iface.Name;
            if (string.Equals(name, "IDisposable", StringComparison.Ordinal)
                || string.Equals(name, "IAsyncDisposable", StringComparison.Ordinal)
                || string.Equals(name, "IAsyncLifetime", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
