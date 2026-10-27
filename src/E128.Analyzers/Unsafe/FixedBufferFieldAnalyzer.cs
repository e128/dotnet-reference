using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Unsafe;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FixedBufferFieldAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128119";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use an inline array instead of a fixed-size buffer",
        "A fixed-size buffer has no bounds checks and no default zeroing. Use an [InlineArray] struct instead.",
        "Unsafe",
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
        context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
    }

    private static void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;

        // Inline arrays need C# 12 and a runtime that defines InlineArrayAttribute (.NET 8).
        if (MetadataTypes.Find(context.SemanticModel.Compilation, "System.Runtime.CompilerServices.InlineArrayAttribute").IsEmpty
            || context.Node.SyntaxTree.Options is not CSharpParseOptions parseOptions
            || parseOptions.LanguageVersion.MapSpecifiedToEffectiveVersion() < LanguageVersion.CSharp12)
        {
            return;
        }

        if (!HasFixedModifier(field))
        {
            return;
        }

        // A declaration with several buffers is reported once. Only the code fix needs a single declarator.
        var hasBuffer = field.Declaration.Variables.Any(variable =>
            variable.Initializer is null
            && TryGetBufferSize(variable, context.SemanticModel, context.CancellationToken, out _)
            && context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is IFieldSymbol);

        if (!hasBuffer)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, field.GetLocation()));
    }

    private static bool HasFixedModifier(FieldDeclarationSyntax field)
    {
        foreach (var modifier in field.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.FixedKeyword))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool TryGetBufferSize(
        VariableDeclaratorSyntax variable,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out int size)
    {
        size = 0;

        // The symbol carries the evaluated size, so a const or a constant expression works too.
        if (semanticModel.GetDeclaredSymbol(variable, cancellationToken) is not IFieldSymbol { IsFixedSizeBuffer: true, FixedSize: > 0 } field)
        {
            return false;
        }

        size = field.FixedSize;
        return true;
    }
}
