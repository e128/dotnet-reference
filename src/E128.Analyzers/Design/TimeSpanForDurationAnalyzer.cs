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
        "E128-Design",
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
        // A partial member arrives as two symbol events. Report only the defining part.
        if (PartialMembers.IsImplementationPart(context.Symbol))
        {
            return;
        }

        var (type, name, location) = GetDetails(context.Symbol);

        if (type is null || name is null || location is null)
        {
            return;
        }

        var effectiveType = TypeSymbols.UnwrapNullable(type);

        if (!IsNumericDurationType(effectiveType))
        {
            return;
        }

        if (!NameSuffixes.Matches(name, DurationSuffixes))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, name));
    }

    private static (ITypeSymbol? Type, string? Name, Location? Location) GetDetails(ISymbol symbol)
    {
        return symbol switch
        {
            IPropertySymbol p => (p.Type, p.Name, p.Locations.IsEmpty ? null : p.Locations[0]),
            IParameterSymbol p => (p.Type, p.Name, p.Locations.IsEmpty ? null : p.Locations[0]),
            _ => default
        };
    }

    private static bool IsNumericDurationType(ITypeSymbol type)
    {
        return type.SpecialType is SpecialType.System_Int32
            or SpecialType.System_Int64
            or SpecialType.System_Single
            or SpecialType.System_Double;
    }
}
