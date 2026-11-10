using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RegexInstanceInLoopAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128210";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Regex instance created inside a loop",
        "'new Regex' inside a loop compiles the pattern per iteration — hoist the instance or use 'GeneratedRegex'",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startContext =>
            MetadataTypes.RegisterForType(
                startContext,
                "System.Text.RegularExpressions.Regex",
                AnalyzeCreation,
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression));
    }

    private static void AnalyzeCreation(SyntaxNodeAnalysisContext context, INamedTypeSymbol regex)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (creation.ArgumentList is not { Arguments.Count: > 0 })
        {
            return;
        }

        var createdType = context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type;
        if (!SymbolEqualityComparer.Default.Equals(createdType, regex))
        {
            return;
        }

        var body = LoopContext.FindEnclosingLoopBody(creation);
        if (body is null)
        {
            return;
        }

        if (LoopContext.ReferencesLoopVariable(creation, body, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, creation.GetLocation()));
    }
}
