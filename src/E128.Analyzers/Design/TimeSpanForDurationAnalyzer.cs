using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128050: Flags numeric properties and parameters whose names suggest a time duration
///     (e.g., TimeoutSeconds, DelayMs). Use <see cref="TimeSpan" /> instead to eliminate
///     unit ambiguity at call sites.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TimeSpanForDurationAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128050";

    private static readonly ImmutableArray<string> DurationSuffixes =
    [
        "Seconds", "Sec", "Millis", "Milliseconds", "Ms",
        "Minutes", "Min", "Hours", "Days",
        "Timeout", "Delay", "Duration", "Interval", "Period"
    ];

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use TimeSpan for time-duration values to avoid unit ambiguity",
        "'{0}' is numeric but its name suggests a time duration. Use TimeSpan instead to eliminate unit ambiguity at call sites.",
        "Design",
        DiagnosticSeverity.Error,
        true,
        "Numeric properties and parameters whose names imply a time duration (e.g., TimeoutSeconds, " +
        "DelayMs) are ambiguous at call sites — callers can't tell whether the value is seconds, " +
        "milliseconds, or ticks. Use TimeSpan to make the unit explicit in the type system.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Property, SymbolKind.Parameter);
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        var (type, name, location) = context.Symbol switch
        {
            IPropertySymbol p => (p.Type, p.Name, p.Locations.IsEmpty ? null : p.Locations[0]),
            IParameterSymbol p => (p.Type, p.Name, p.Locations.IsEmpty ? null : p.Locations[0]),
            _ => default
        };

        if (type is null || location is null)
        {
            return;
        }

        var effectiveType = UnwrapNullable(type);

        if (!IsNumericDurationType(effectiveType))
        {
            return;
        }

        if (!HasDurationSuffix(name))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, name));
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named
               && named.TypeArguments.Length == 1
            ? named.TypeArguments[0]
            : type;
    }

    private static bool IsNumericDurationType(ITypeSymbol type)
    {
        return type.SpecialType is SpecialType.System_Int32
            or SpecialType.System_Int64
            or SpecialType.System_Single
            or SpecialType.System_Double;
    }

    internal static bool HasDurationSuffix(string name)
    {
        foreach (var suffix in DurationSuffixes)
        {
            if (name.Length < suffix.Length)
            {
                continue;
            }

            var startIndex = name.Length - suffix.Length;

            // Require the suffix to start at an uppercase-letter position in the name.
            // This excludes bare lowercase words (e.g. "min", "days", "timeout" as local params)
            // while correctly matching PascalCase compound names (TimeoutSeconds, delayMs, ActiveDays)
            // and uppercase-first standalone names (Timeout, Days, Min as property names).
            if (!char.IsUpper(name[startIndex]))
            {
                continue;
            }

            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
