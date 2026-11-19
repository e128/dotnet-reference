using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128245: A constant integral-to-enum cast produces a value that matches no declared member.
/// </summary>
/// <remarks>
///     The rule reports only a constant operand, because a runtime value cannot be checked. A
///     <c lang="csharp">[Flags]</c> enum accepts any bit combination, so it stays silent and
///     E128246 owns that shape. A value equal to a declared member, including an alias, stays
///     silent. A test compilation stays silent, because a validator test builds an undefined
///     value on purpose. The rule carries no code fix, because the intended member depends on
///     intent.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EnumCastOutsideMembersAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128245";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Enum cast outside the member set",
        "'{0}' has no member with value '{1}'",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Cast to a declared enum member. A value outside the member set silently enters the domain as an undefined state.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static start =>
        {
            if (XunitTestHelper.IsTestCompilation(start.Compilation, start.CancellationToken))
            {
                return;
            }

            var flagsAttribute = start.Compilation.GetTypeByMetadataName("System.FlagsAttribute");
            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, flagsAttribute), SyntaxKind.CastExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol? flagsAttribute)
    {
        var cast = (CastExpressionSyntax)context.Node;
        var constant = context.SemanticModel.GetConstantValue(cast.Expression, context.CancellationToken);
        if (context.SemanticModel.GetTypeInfo(cast, context.CancellationToken).Type
            is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType
            || IsFlags(enumType, flagsAttribute)
            || !constant.HasValue
            || !TryNormalize(constant.Value, enumType, out var value))
        {
            return;
        }

        foreach (var field in enumType.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.HasConstantValue
                && TryNormalize(field.ConstantValue, enumType, out var memberValue)
                && memberValue == value)
            {
                return;
            }
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, cast.GetLocation(), enumType.Name, constant.Value));
    }

    private static bool IsFlags(INamedTypeSymbol enumType, INamedTypeSymbol? flagsAttribute)
    {
        return flagsAttribute is not null
            && enumType.GetAttributes().Any(attribute =>
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, flagsAttribute));
    }

    /// <summary>
    ///     Converts a boxed integral constant to one bit pattern in the enum's underlying width.
    ///     A signed value keeps its two's-complement pattern so an alias such as <c lang="csharp">-1</c> matches.
    /// </summary>
    private static bool TryNormalize(object? constant, INamedTypeSymbol enumType, out ulong value)
    {
        value = 0;
        if (constant is not (IConvertible and not (string or char or bool)))
        {
            return false;
        }

        try
        {
            value = IsUnsigned(enumType.EnumUnderlyingType)
                ? Convert.ToUInt64(constant, CultureInfo.InvariantCulture)
                : unchecked((ulong)Convert.ToInt64(constant, CultureInfo.InvariantCulture));
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
    }

    private static bool IsUnsigned(INamedTypeSymbol? underlyingType)
    {
        return underlyingType?.SpecialType
            is SpecialType.System_Byte
            or SpecialType.System_UInt16
            or SpecialType.System_UInt32
            or SpecialType.System_UInt64;
    }
}
