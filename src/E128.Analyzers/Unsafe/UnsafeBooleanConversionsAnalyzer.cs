using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Unsafe;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnsafeBooleanConversionsAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128115";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not reinterpret between bool and int",
        "Unsafe.As between bool and int reinterprets a one-byte value as four bytes. Use a comparison or a ternary.",
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
        context.RegisterCompilationStartAction(start =>
        {
            var unsafeType = MetadataTypes.Find(start.Compilation, "System.Runtime.CompilerServices.Unsafe");
            if (unsafeType.IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, unsafeType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> unsafeType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (InvocationNames.GetSimpleName(invocation) is not { } memberAccess
            || !string.Equals(memberAccess.Identifier.ValueText, "As", StringComparison.Ordinal))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !MetadataTypes.Contains(unsafeType, method.ContainingType)
            || method.TypeArguments.Length != 2
            || method.Parameters.Length != 1
            || method.Parameters[0].RefKind != RefKind.Ref)
        {
            return;
        }

        if (GetConversionKind(method.TypeArguments[0], method.TypeArguments[1]) is null
            || RefUsage.IsUsedAsReference(invocation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    internal static BooleanConversion? GetConversionKind(ITypeSymbol source, ITypeSymbol target)
    {
        if (source.SpecialType == SpecialType.System_Int32 && target.SpecialType == SpecialType.System_Boolean)
        {
            return BooleanConversion.ToIntBool;
        }

        var isBoolToInt = source.SpecialType == SpecialType.System_Boolean
            && target.SpecialType == SpecialType.System_Int32;

        return isBoolToInt ? BooleanConversion.ToBoolInt : null;
    }

    internal enum BooleanConversion
    {
        ToIntBool = 0,
        ToBoolInt = 1,
    }
}
