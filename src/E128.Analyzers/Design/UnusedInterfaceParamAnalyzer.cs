using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128059: Detects interface method implementations where a parameter declared in the
///     interface contract is never referenced in the implementing method body. A silently
///     ignored parameter almost always indicates a missing implementation or an incorrect
///     interface contract.
///     <para>
///         CancellationToken parameters are excluded — propagation is handled by E128038.
///     </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnusedInterfaceParamAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128059";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Interface method parameter is unused in implementation",
        "Parameter '{0}' is declared in the interface contract but never referenced in the implementation — the contract promises callers that this input affects the result",
        "E128-Design",
        DiagnosticSeverity.Error,
        true,
        "Every parameter declared by an interface contract should be consumed by the implementation. A silently ignored parameter breaks the behavioral contract with callers.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        // Cheap gates first. The interface-implementation walk below is the expensive one, so
        // only methods that can actually report reach it.
        if (method.Parameters.IsEmpty)
        {
            return;
        }

        var syntax = GetMethodSyntax(method, context.CancellationToken);
        if (!HasBody(syntax))
        {
            return;
        }

        if (!InterfaceMembers.Implements(method))
        {
            return;
        }

        // One body walk collects every referenced identifier. Each parameter then costs a
        // hash lookup instead of a fresh subtree walk.
        var referencedNames = CollectReferencedNames(syntax);

        foreach (var param in method.Parameters)
        {
            // An underscore-prefixed name is the discard convention the code fix would enforce on
            // itself, so the parameter needs no report and no fix.
            if (IsExemptParameter(param, referencedNames))
            {
                continue;
            }

            var paramSyntax = FindParamSyntax(syntax, param.Name);
            if (paramSyntax is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, paramSyntax.GetLocation(), param.Name));
            }
        }
    }

    private static bool HasBody([NotNullWhen(true)] MethodDeclarationSyntax? syntax)
    {
        return syntax is not null && (syntax.Body is not null || syntax.ExpressionBody is not null);
    }

    private static bool IsExemptParameter(IParameterSymbol param, HashSet<string> referencedNames)
    {
        return IsCancellationToken(param)
               || param.Name.StartsWith("_", StringComparison.Ordinal)
               || referencedNames.Contains(param.Name);
    }

    private static HashSet<string> CollectReferencedNames(MethodDeclarationSyntax method)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
        if (body is null)
        {
            return names;
        }

        foreach (var node in body.DescendantNodes())
        {
            if (node is IdentifierNameSyntax identifier)
            {
                names.Add(identifier.Identifier.ValueText);
            }
        }

        return names;
    }

    private static bool IsCancellationToken(IParameterSymbol param)
    {
        return param.Type is INamedTypeSymbol type
               && string.Equals(type.Name, "CancellationToken", StringComparison.Ordinal)
               && Namespaces.Equals(type.ContainingNamespace, "System.Threading");
    }

    private static MethodDeclarationSyntax? GetMethodSyntax(
        IMethodSymbol method,
        CancellationToken cancellationToken)
    {
        var reference = method.DeclaringSyntaxReferences.FirstOrDefault();
        return reference?.GetSyntax(cancellationToken) as MethodDeclarationSyntax;
    }

    private static ParameterSyntax? FindParamSyntax(MethodDeclarationSyntax method, string paramName)
    {
        foreach (var param in method.ParameterList.Parameters)
        {
            if (string.Equals(param.Identifier.ValueText, paramName, StringComparison.Ordinal))
            {
                return param;
            }
        }

        return null;
    }
}
