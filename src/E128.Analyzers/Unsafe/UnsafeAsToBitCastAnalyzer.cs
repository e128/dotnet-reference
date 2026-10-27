using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Unsafe;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnsafeAsToBitCastAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128112";

    private static readonly Dictionary<SpecialType, int> ElementSizes = new()
    {
        [SpecialType.System_Boolean] = 1,
        [SpecialType.System_Byte] = 1,
        [SpecialType.System_SByte] = 1,
        [SpecialType.System_Int16] = 2,
        [SpecialType.System_UInt16] = 2,
        [SpecialType.System_Char] = 2,
        [SpecialType.System_Int32] = 4,
        [SpecialType.System_UInt32] = 4,
        [SpecialType.System_Single] = 4,
        [SpecialType.System_Int64] = 8,
        [SpecialType.System_UInt64] = 8,
        [SpecialType.System_Double] = 8,
        [SpecialType.System_Decimal] = 16,
    };

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use Unsafe.BitCast instead of Unsafe.As",
        "Unsafe.As reinterprets one value type as another. Use Unsafe.BitCast, which checks the size and rejects a reference type.",
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
            // Unsafe.BitCast needs .NET 8. Earlier targets have no replacement to offer.
            if (unsafeType.IsEmpty || unsafeType.All(type => type.GetMembers("BitCast").IsEmpty))
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

        if (!TryGetFixedSize(method.TypeArguments[0], out var sourceSize)
            || !TryGetFixedSize(method.TypeArguments[1], out var targetSize)
            || sourceSize != targetSize
            || RefUsage.IsUsedAsReference(invocation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    /// <summary>
    ///     Returns the storage size for a type whose size is known without layout computation.
    ///     A struct with fields returns false, because its size depends on layout rules.
    /// </summary>
    private static bool TryGetFixedSize(ITypeSymbol type, out int size)
    {
        if (type.TypeKind == TypeKind.Enum)
        {
            size = 0;
            var underlying = ((INamedTypeSymbol)type).EnumUnderlyingType;
            return underlying is not null && TryGetFixedSize(underlying, out size);
        }

        return ElementSizes.TryGetValue(type.SpecialType, out size);
    }
}
