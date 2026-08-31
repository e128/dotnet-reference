using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     Reports <c lang="csharp">.ConfigureAwait(false)</c> calls in executable application code
///     (console apps, Worker Service hosts). These hosts have no SynchronizationContext,
///     so <c lang="csharp">ConfigureAwait(false)</c> is unnecessary noise.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConfigureAwaitFalseE128Analyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128022";

    private const string BlazorWasmHostBuilderTypeName =
        "Microsoft.AspNetCore.Components.WebAssembly.Hosting.WebAssemblyHostBuilder";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Remove ConfigureAwait(false) in application code",
        "Remove '.ConfigureAwait(false)' — application code should not use ConfigureAwait",
        "Design",
        DiagnosticSeverity.Warning,
        true,
        "ConfigureAwait(false) should only be used in general-purpose library code. Application projects (console apps, ASP.NET Core hosts, WPF, WinForms, Worker Services) should not use it.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var outputKind = compilationContext.Compilation.Options.OutputKind;
            if (outputKind is not OutputKind.ConsoleApplication and not OutputKind.WindowsApplication)
            {
                return;
            }

            if (compilationContext.Compilation.GetTypeByMetadataName(BlazorWasmHostBuilderTypeName) is not null)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (!string.Equals(memberAccess.Name.Identifier.Text, "ConfigureAwait", StringComparison.Ordinal))
        {
            return;
        }

        var args = invocation.ArgumentList.Arguments;

        if (args.Count != 1)
        {
            return;
        }

        if (args[0].Expression is not LiteralExpressionSyntax literal)
        {
            return;
        }

        if (!literal.Token.IsKind(SyntaxKind.FalseKeyword))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }
}
