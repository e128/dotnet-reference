using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128031: Flags <c>services.AddSingleton(sp => ...)</c> factory-lambda registrations
///     where the return type implements <see cref="IDisposable" />.
///     The DI container does not auto-dispose factory-registered singletons in all host configurations.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DisposableSingletonFactoryAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128031";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "AddSingleton factory returns IDisposable",
        "Factory singleton '{0}' implements IDisposable but will not be disposed automatically by the DI container — register with AddSingleton<TService, TImpl>() or manage lifetime explicitly",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Factory-lambda AddSingleton registrations (services.AddSingleton(sp => ...)) " +
        "where the lambda returns an IDisposable type are not auto-disposed by the container. " +
        "Prefer AddSingleton<TService, TImpl>() so the container manages disposal, " +
        "or explicitly hook IHostApplicationLifetime to dispose on shutdown.");

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

        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax m => m.Name.Identifier.Text,
            IdentifierNameSyntax i => i.Identifier.Text,
            _ => null
        };

        if (!string.Equals(methodName, "AddSingleton", StringComparison.Ordinal))
        {
            return;
        }

        var args = invocation.ArgumentList.Arguments;
        if (args.Count != 1)
        {
            return;
        }

        var arg = args[0].Expression;
        if (arg is not (SimpleLambdaExpressionSyntax or ParenthesizedLambdaExpressionSyntax))
        {
            return;
        }

        var bodyExpr = arg switch
        {
            SimpleLambdaExpressionSyntax s => s.Body as ExpressionSyntax,
            ParenthesizedLambdaExpressionSyntax p => p.Body as ExpressionSyntax,
            _ => null
        };

        if (bodyExpr is null)
        {
            return;
        }

        var bodyTypeInfo = context.SemanticModel.GetTypeInfo(bodyExpr, context.CancellationToken);
        var returnType = bodyTypeInfo.Type;
        if (returnType is null)
        {
            return;
        }

        if (!ImplementsIDisposable(returnType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, arg.GetLocation(), returnType.Name));
    }

    private static bool ImplementsIDisposable(ITypeSymbol type)
    {
        return type.AllInterfaces.Any(i => string.Equals(i.ToDisplayString(), "System.IDisposable", StringComparison.Ordinal));
    }
}
