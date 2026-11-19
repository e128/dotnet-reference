using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128246: A <c lang="csharp">[Flags]</c> enum member holds a value that is not a power of
///     two and does not name its combination.
/// </summary>
/// <remarks>
///     A flags member should own one bit. A combination is valid when its name carries the
///     member names it combines, such as <c lang="csharp">ReadWrite</c>, or when it is the
///     <c lang="csharp">All</c> member. Any other multi-bit value is likely a mistyped next bit,
///     for example <c lang="csharp">Execute = 3</c> after <c lang="csharp">Read = 1</c> and
///     <c lang="csharp">Write = 2</c>.
///     A negative value and a zero value stay silent.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FlagsMemberValueAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128246";

    private const int MinimumNamedMembers = 2;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Flags member value is not a power of two",
        "'{0}.{1}' is not a power of two and is not a named combination",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Give the member its own bit, or name the combination after the members it joins. A value with an unnamed extra bit overlaps another member.");

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
            var flagsAttribute = start.Compilation.GetTypeByMetadataName("System.FlagsAttribute");
            start.RegisterSymbolAction(nodeContext => Analyze(nodeContext, flagsAttribute), SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol? flagsAttribute)
    {
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType
            || !EnumConstants.IsFlags(enumType, flagsAttribute))
        {
            return;
        }

        var members = enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(static field => field.HasConstantValue)
            .ToList();

        var singleBits = CollectSingleBits(members, enumType);
        if (singleBits.Count is 0)
        {
            return;
        }

        foreach (var member in members)
        {
            if (!EnumConstants.TryNormalize(member.ConstantValue, enumType, out var value)
                || value is 0
                || EnumConstants.IsPowerOfTwo(value)
                || EnumConstants.IsNegative(member.ConstantValue, enumType))
            {
                continue;
            }

            if (IsNamedCombination(member.Name, value, singleBits))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, member.Locations[0], enumType.Name, member.Name));
        }
    }

    private static List<(IFieldSymbol Member, ulong Value)> CollectSingleBits(
        List<IFieldSymbol> members,
        INamedTypeSymbol enumType)
    {
        var singleBits = new List<(IFieldSymbol Member, ulong Value)>();
        foreach (var member in members)
        {
            if (EnumConstants.TryNormalize(member.ConstantValue, enumType, out var value)
                && value is not 0
                && EnumConstants.IsPowerOfTwo(value))
            {
                singleBits.Add((member, value));
            }
        }

        return singleBits;
    }

    /// <summary>
    ///     True when the value equals the union of the single-bit members named in the member
    ///     name, or when it is the all-bits member with an <c lang="csharp">All</c> style name.
    /// </summary>
    private static bool IsNamedCombination(
        string memberName,
        ulong value,
        List<(IFieldSymbol Member, ulong Value)> singleBits)
    {
        var namedBits = 0UL;
        var namedMembers = 0;
        var allBits = 0UL;

        foreach (var (member, memberValue) in singleBits)
        {
            allBits |= memberValue;
            if (memberName.IndexOf(member.Name, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            namedBits |= memberValue;
            namedMembers++;
        }

        if (namedMembers >= MinimumNamedMembers && value == namedBits)
        {
            return true;
        }

        return value == allBits && IsAllName(memberName);
    }

    private static bool IsAllName(string memberName)
    {
        return memberName.IndexOf("All", StringComparison.OrdinalIgnoreCase) >= 0
            || memberName.IndexOf("Everything", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
