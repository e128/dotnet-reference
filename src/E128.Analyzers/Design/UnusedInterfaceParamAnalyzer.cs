using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
/// E128059: Detects interface method implementations where a parameter declared in the
/// interface contract is never referenced in the implementing method body. A silently
/// ignored parameter almost always indicates a missing implementation or an incorrect
/// interface contract.
/// <para>
/// CancellationToken parameters are excluded — propagation is handled by E128038.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnusedInterfaceParamAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128059";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Interface method parameter is unused in implementation",
        messageFormat: "Parameter '{0}' is declared in the interface contract but never referenced in the implementation — the contract promises callers that this input affects the result",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Every parameter declared by an interface contract should be consumed by the implementation. A silently ignored parameter breaks the behavioral contract with callers.");

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

        if (!IsInterfaceImplementation(method))
        {
            return;
        }

        if (method.Parameters.IsEmpty)
        {
            return;
        }

        var syntax = GetMethodSyntax(method, context.CancellationToken);
        if (syntax is null || (syntax.Body is null && syntax.ExpressionBody is null))
        {
            return;
        }

        foreach (var param in method.Parameters)
        {
            if (IsCancellationToken(param))
            {
                continue;
            }

            if (!IsParameterUsedInBody(param.Name, syntax))
            {
                var paramSyntax = FindParamSyntax(syntax, param.Name);
                if (paramSyntax is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, paramSyntax.GetLocation(), param.Name));
                }
            }
        }
    }

    private static bool IsInterfaceImplementation(IMethodSymbol method)
    {
        if (!method.ExplicitInterfaceImplementations.IsEmpty)
        {
            return true;
        }

        if (method.ContainingType is not INamedTypeSymbol containingType)
        {
            return false;
        }

        if (containingType.TypeKind == TypeKind.Interface || InheritsFromDelegate(containingType))
        {
            return false;
        }

        foreach (var iface in containingType.AllInterfaces)
        {
            foreach (var ifaceMember in iface.GetMembers(method.Name).OfType<IMethodSymbol>())
            {
                var impl = containingType.FindImplementationForInterfaceMember(ifaceMember);
                if (impl is not null
                    && SymbolEqualityComparer.Default.Equals(impl.OriginalDefinition, method.OriginalDefinition))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool InheritsFromDelegate(INamedTypeSymbol type)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            if (string.Equals(current.Name, "MulticastDelegate", StringComparison.Ordinal)
                || string.Equals(current.Name, "Delegate", StringComparison.Ordinal))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static bool IsCancellationToken(IParameterSymbol param) =>
        string.Equals(param.Type.Name, "CancellationToken", StringComparison.Ordinal);

    private static MethodDeclarationSyntax? GetMethodSyntax(
        IMethodSymbol method,
        System.Threading.CancellationToken cancellationToken)
    {
        var reference = method.DeclaringSyntaxReferences.FirstOrDefault();
        return reference?.GetSyntax(cancellationToken) as MethodDeclarationSyntax;
    }

    private static bool IsParameterUsedInBody(string paramName, MethodDeclarationSyntax method)
    {
        var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
        if (body is null)
        {
            return true;
        }

        foreach (var node in body.DescendantNodes())
        {
            if (node is IdentifierNameSyntax id
                && string.Equals(id.Identifier.ValueText, paramName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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
