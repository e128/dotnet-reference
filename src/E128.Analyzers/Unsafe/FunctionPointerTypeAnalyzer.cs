using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Unsafe;

/// <summary>
///     E128200: Reports a <c lang="csharp">delegate*</c> function pointer type. A function pointer
///     carries no lifetime or calling-convention guarantee, and native code can call a collected
///     target. A delegate type keeps the target alive and makes the call visible to the runtime.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FunctionPointerTypeAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128200";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not declare a function pointer type",
        "Function pointer type '{0}' bypasses the managed call path. Use a delegate type instead.",
        "E128-Unsafe",
        DiagnosticSeverity.Warning,
        true,
        "A function pointer carries no lifetime or calling-convention guarantee, and native code " +
        "can call a collected target. A delegate type keeps the target alive and makes the call " +
        "visible to the runtime.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeFunctionPointer, SyntaxKind.FunctionPointerType);
    }

    private static void AnalyzeFunctionPointer(SyntaxNodeAnalysisContext context)
    {
        context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation(), context.Node.ToString()));
    }
}
