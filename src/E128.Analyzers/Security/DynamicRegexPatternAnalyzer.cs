using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

/// <summary>
///     E128265: A regex is built from a run-time pattern with no timeout and no
///     <c lang="csharp">NonBacktracking</c> option. The caller can inject a catastrophic
///     pattern, and no build-time rule can inspect it.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DynamicRegexPatternAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128265";

    private const string RegexMetadataName = "System.Text.RegularExpressions.Regex";

    private const string RegexOptionsMetadataName = "System.Text.RegularExpressions.RegexOptions";

    private const string TimeSpanMetadataName = "System.TimeSpan";

    private const string NonBacktrackingName = "NonBacktracking";

    private const string PatternParameterName = "pattern";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Regex pattern is not a constant and the call sets no timeout",
        "Regex pattern '{0}' is not a constant and the call sets no timeout: escape or validate the pattern, or pass a timeout",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true,
        "A run-time regex pattern can inject a nested quantifier or an overlapping alternation. Escape or validate the pattern, or pass a timeout or the NonBacktracking option.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

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
            var regex = start.Compilation.GetTypeByMetadataName(RegexMetadataName);

            if (regex is null)
            {
                return;
            }

            var options = start.Compilation.GetTypeByMetadataName(RegexOptionsMetadataName);
            var timeSpan = start.Compilation.GetTypeByMetadataName(TimeSpanMetadataName);
            var nonBacktracking = NonBacktrackingValue(options);

            start.RegisterOperationAction(
                operation => Analyze(operation, regex, options, timeSpan, nonBacktracking),
                OperationKind.ObjectCreation,
                OperationKind.Invocation);
        });
    }

    private static void Analyze(
        OperationAnalysisContext context,
        INamedTypeSymbol regex,
        INamedTypeSymbol? options,
        INamedTypeSymbol? timeSpan,
        int nonBacktracking)
    {
        if (context.Operation is IObjectCreationOperation creation)
        {
            AnalyzeCreation(context, creation, regex, options, timeSpan, nonBacktracking);
        }
        else if (context.Operation is IInvocationOperation invocation)
        {
            AnalyzeInvocation(context, invocation, regex, options, timeSpan, nonBacktracking);
        }
    }

    private static void AnalyzeCreation(
        OperationAnalysisContext context,
        IObjectCreationOperation creation,
        INamedTypeSymbol regex,
        INamedTypeSymbol? options,
        INamedTypeSymbol? timeSpan,
        int nonBacktracking)
    {
        if (!SymbolEqualityComparer.Default.Equals(creation.Type, regex))
        {
            return;
        }

        Report(context, creation.Arguments, options, timeSpan, nonBacktracking);
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        IInvocationOperation invocation,
        INamedTypeSymbol regex,
        INamedTypeSymbol? options,
        INamedTypeSymbol? timeSpan,
        int nonBacktracking)
    {
        if (!SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, regex)
            || !IsStaticPatternMethod(invocation.TargetMethod.Name))
        {
            return;
        }

        Report(context, invocation.Arguments, options, timeSpan, nonBacktracking);
    }

    private static void Report(
        OperationAnalysisContext context,
        ImmutableArray<IArgumentOperation> arguments,
        INamedTypeSymbol? options,
        INamedTypeSymbol? timeSpan,
        int nonBacktracking)
    {
        var pattern = FindArgument(arguments, PatternParameterName);

        if (pattern is null
            || IsConstant(pattern)
            || HasTimeSpan(arguments, timeSpan)
            || HasNonBacktracking(arguments, options, nonBacktracking))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, context.Operation.Syntax.GetLocation(), pattern.Syntax.ToString()));
    }

    private static bool IsStaticPatternMethod(string name)
    {
        return string.Equals(name, "Match", StringComparison.Ordinal)
               || string.Equals(name, "IsMatch", StringComparison.Ordinal)
               || string.Equals(name, "Replace", StringComparison.Ordinal)
               || string.Equals(name, "Split", StringComparison.Ordinal);
    }

    private static IArgumentOperation? FindArgument(
        ImmutableArray<IArgumentOperation> arguments,
        string name)
    {
        foreach (var argument in arguments)
        {
            if (argument.Parameter is { } parameter
                && string.Equals(parameter.Name, name, StringComparison.Ordinal))
            {
                return argument;
            }
        }

        return null;
    }

    private static bool IsConstant(IArgumentOperation argument)
    {
        return argument.Value.ConstantValue.HasValue && argument.Value.ConstantValue.Value is string;
    }

    private static bool HasTimeSpan(
        ImmutableArray<IArgumentOperation> arguments,
        INamedTypeSymbol? timeSpan)
    {
        if (timeSpan is null)
        {
            return false;
        }

        foreach (var argument in arguments)
        {
            if (argument.Parameter is { } parameter
                && SymbolEqualityComparer.Default.Equals(parameter.Type, timeSpan))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasNonBacktracking(
        ImmutableArray<IArgumentOperation> arguments,
        INamedTypeSymbol? options,
        int nonBacktracking)
    {
        if (options is null || nonBacktracking is 0)
        {
            return false;
        }

        foreach (var argument in arguments)
        {
            if (OptionsValue(argument, options) is { } value)
            {
                return (value & nonBacktracking) is not 0;
            }
        }

        return false;
    }

    private static int? OptionsValue(IArgumentOperation argument, INamedTypeSymbol options)
    {
        if (argument.Parameter is not { } parameter
            || !SymbolEqualityComparer.Default.Equals(parameter.Type, options)
            || argument.Value.ConstantValue is not { HasValue: true, Value: int value })
        {
            return null;
        }

        return value;
    }

    private static int NonBacktrackingValue(INamedTypeSymbol? options)
    {
        if (options is null)
        {
            return 0;
        }

        foreach (var member in options.GetMembers(NonBacktrackingName).OfType<IFieldSymbol>())
        {
            if (member.HasConstantValue && member.ConstantValue is int value)
            {
                return value;
            }
        }

        return 0;
    }
}
