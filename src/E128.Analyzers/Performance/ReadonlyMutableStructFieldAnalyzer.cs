using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128247: A <c lang="csharp">readonly</c> field holds a mutable struct, so every member
///     call copies the whole value.
/// </summary>
/// <remarks>
///     The compiler cannot prove that a mutating member call leaves the value unchanged, so it
///     copies the struct out of the readonly field first. The rule reports a private or public
///     field alike. A <c lang="csharp">readonly struct</c> type, a primitive, an enum, a tuple,
///     and a ref struct stay silent. A struct with no mutable instance member also stays silent,
///     because no call can force a copy.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadonlyMutableStructFieldAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128247";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Readonly field of a mutable struct",
        "Readonly field '{0}' holds a mutable struct and copies on every member call",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true,
        "Make the struct readonly, or keep the value in a mutable field or a local. A call on a readonly struct value copies the whole struct first.");

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
        context.RegisterSymbolAction(Analyze, SymbolKind.Field);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        if (context.Symbol is not IFieldSymbol { IsReadOnly: true } field
            || field.Type is not INamedTypeSymbol { TypeKind: TypeKind.Struct } type
            || type.IsReadOnly
            || type.IsRefLikeType
            || type.IsTupleType
            || type.SpecialType is not SpecialType.None
            || !HasMutableMember(type))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, field.Locations[0], field.Name));
    }

    private static bool HasMutableMember(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            if (member.IsStatic)
            {
                continue;
            }

            if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary, IsReadOnly: false })
            {
                return true;
            }

            if (member is IPropertySymbol { GetMethod.IsReadOnly: false })
            {
                return true;
            }
        }

        return false;
    }
}
