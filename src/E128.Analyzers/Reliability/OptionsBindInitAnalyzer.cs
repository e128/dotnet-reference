using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128033: Detects Options classes passed to <c lang="csharp">.Bind(config.GetSection(...))</c>
///     that have <c lang="csharp">init</c>-only property accessors instead of <c lang="csharp">set</c>.
///     The Microsoft.Extensions.Configuration binder requires mutable <c lang="csharp">set</c> properties
///     to populate options from appsettings — <c lang="csharp">init</c> breaks silently at runtime.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OptionsBindInitAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128033";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Options type bound from configuration has init-only property",
        "Property '{0}' on bind-target '{1}' uses 'init' — the config binder requires 'set'",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Options classes bound via .Bind(config.GetSection(...)) must use 'set' accessors. " +
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

        if (!IsBindCall(invocation) || ReceiverOf(invocation) is not { } receiver)
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(receiver, context.CancellationToken).Type
            is not INamedTypeSymbol receiverType)
        {
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(receiverType.OriginalDefinition, optionsBuilderType)
            || !receiverType.IsGenericType
            || receiverType.TypeArguments.Length is not 1
            || receiverType.TypeArguments[0] is not INamedTypeSymbol targetType)
        {
            return;
        }

        ReportInitProperties(context, targetType);
    }

    private static bool IsBindCall(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => IsBindName(memberAccess.Name.Identifier.ValueText),
            MemberBindingExpressionSyntax binding => IsBindName(binding.Name.Identifier.ValueText),
            _ => false
        };
    }

    private static bool IsBindName(string name)
    {
        return string.Equals(name, "Bind", StringComparison.Ordinal)
               || string.Equals(name, "BindConfiguration", StringComparison.Ordinal);
    }

    private static ExpressionSyntax? ReceiverOf(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Expression,
            MemberBindingExpressionSyntax when invocation.Parent is ConditionalAccessExpressionSyntax conditional
                => conditional.Expression,
            _ => null
        };
    }

    private static void ReportInitProperties(SyntaxNodeAnalysisContext context, INamedTypeSymbol type)
    {
        var properties = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(static p => p.SetMethod is { IsInitOnly: true });

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
