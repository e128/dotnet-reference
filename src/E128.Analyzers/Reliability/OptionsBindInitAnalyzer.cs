using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
/// E128033: Detects Options classes passed to <c>.Bind(config.GetSection(...))</c>
/// that have <c>init</c>-only property accessors instead of <c>set</c>.
/// The Microsoft.Extensions.Configuration binder requires mutable <c>set</c> properties
/// to populate options from appsettings — <c>init</c> breaks silently at runtime.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OptionsBindInitAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128033";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Options class bound via .Bind() has init-only property",
        messageFormat: "Property '{0}' on Bind()-target '{1}' uses 'init' — the config binder requires 'set'",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Options classes bound via .Bind(config.GetSection(...)) must use 'set' accessors. " +
            "The Microsoft.Extensions.Configuration binder cannot populate 'init'-only properties, " +
            "causing silent runtime failures with no compile-time error.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var optionsBuilderType = context.Compilation.GetTypeByMetadataName(
            "Microsoft.Extensions.Options.OptionsBuilder`1");

        if (optionsBuilderType is null)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeInvocation(ctx, optionsBuilderType),
            SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol optionsBuilderType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!IsBindCall(invocation))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (!IsOptionsBuilderBind(methodSymbol, optionsBuilderType))
        {
            return;
        }

        var targetType = GetBoundOptionsType(methodSymbol);
        if (targetType is null)
        {
            return;
        }

        ReportInitProperties(context, targetType);
    }

    private static bool IsBindCall(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => string.Equals(memberAccess.Name.Identifier.Text, "Bind", StringComparison.Ordinal),
            _ => false,
        };
    }

    private static bool IsOptionsBuilderBind(IMethodSymbol method, INamedTypeSymbol optionsBuilderType)
    {
        var containingType = method.ContainingType?.OriginalDefinition;
        return containingType is not null
            && SymbolEqualityComparer.Default.Equals(containingType, optionsBuilderType);
    }

    private static INamedTypeSymbol? GetBoundOptionsType(IMethodSymbol method)
    {
        var containingType = method.ContainingType;
        return containingType is not { IsGenericType: true, TypeArguments.Length: 1 }
            ? null
            : containingType.TypeArguments[0] as INamedTypeSymbol;
    }

    private static void ReportInitProperties(SyntaxNodeAnalysisContext context, INamedTypeSymbol type)
    {
        var properties = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.SetMethod is { IsInitOnly: true });

        foreach (var prop in properties)
        {
            foreach (var syntaxRef in prop.SetMethod!.DeclaringSyntaxReferences)
            {
                if (syntaxRef.GetSyntax(context.CancellationToken) is not AccessorDeclarationSyntax accessor)
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    accessor.Keyword.GetLocation(),
                    prop.Name,
                    type.Name));
            }
        }
    }
}
