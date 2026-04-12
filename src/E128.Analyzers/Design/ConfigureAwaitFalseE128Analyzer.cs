using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
/// Reports <c>.ConfigureAwait(false)</c> calls in executable application code
/// (console apps, Worker Service hosts). These hosts have no SynchronizationContext,
/// so <c>ConfigureAwait(false)</c> is unnecessary noise.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConfigureAwaitFalseE128Analyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128022";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Remove ConfigureAwait(false)",
        messageFormat: "Remove '.ConfigureAwait(false)' — this host has no SynchronizationContext",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "ConfigureAwait(false) is unnecessary in ASP.NET Core and Worker Service hosts because there is no SynchronizationContext to avoid marshalling back to.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    private const string BlazorWasmHostBuilderTypeName =
        "Microsoft.AspNetCore.Components.WebAssembly.Hosting.WebAssemblyHostBuilder";

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
            if (compilationContext.Compilation.Options.OutputKind != OutputKind.ConsoleApplication)
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
