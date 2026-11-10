using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128194: Reports a field whose declared type is <c lang="csharp">Lazy&lt;T&gt;</c> and whose
///     initializer creates it with <c lang="csharp">LazyThreadSafetyMode.None</c>. Two threads can
///     run the factory at the same time, so each one builds a separate value.
/// </summary>
/// <remarks>
///     The rule reads the mode argument from the bound constructor symbol, so a positional argument
///     and a named argument both match. The rule stays silent when the mode is omitted, when the
///     mode is not a constant, or when the initializer is not an object creation.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LazyFieldThreadSafetyAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128194";

    internal const string LazyMetadataName = "System.Lazy`1";

    internal const string ModeMetadataName = "System.Threading.LazyThreadSafetyMode";

    private const string NoneMemberName = "None";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Lazy<T> field without thread safety",
        "'{0}' is a shared Lazy<T> field built with 'LazyThreadSafetyMode.None', so two threads can build it twice",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "A shared Lazy<T> field that uses LazyThreadSafetyMode.None lets two threads run the " +
        "factory at the same time. Both threads build a value, and the field keeps only one " +
        "result. Use LazyThreadSafetyMode.ExecutionAndPublication, or the default constructor, " +
        "for a value that two threads can read.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var lazyTypes = MetadataTypes.Find(context.Compilation, LazyMetadataName);
        var modeTypes = MetadataTypes.Find(context.Compilation, ModeMetadataName);

        if (lazyTypes.IsEmpty || modeTypes.IsEmpty)
        {
            return;
        }

        if (FindNoneValue(modeTypes) is not { } noneValue)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeField(ctx, lazyTypes, modeTypes, noneValue),
            SyntaxKind.FieldDeclaration);
    }

    private static int? FindNoneValue(ImmutableArray<INamedTypeSymbol> modeTypes)
    {
        foreach (var modeType in modeTypes)
        {
            foreach (var member in modeType.GetMembers(NoneMemberName))
            {
                if (member is IFieldSymbol { IsConst: true, ConstantValue: int value })
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static void AnalyzeField(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> lazyTypes,
        ImmutableArray<INamedTypeSymbol> modeTypes,
        int noneValue)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        var model = context.SemanticModel;
        var token = context.CancellationToken;

        foreach (var variable in field.Declaration.Variables)
        {
            if (variable.Initializer?.Value is not BaseObjectCreationExpressionSyntax creation)
            {
                continue;
            }

            if (model.GetDeclaredSymbol(variable, token) is not IFieldSymbol fieldSymbol)
            {
                continue;
            }

            if (!IsLazyType(fieldSymbol.Type, lazyTypes))
            {
                continue;
            }

            if (model.GetSymbolInfo(creation, token).Symbol is not IMethodSymbol constructor
                || !IsLazyType(constructor.ContainingType, lazyTypes))
            {
                continue;
            }

            if (!IsUnsynchronized(constructor, creation.ArgumentList, modeTypes, noneValue, model, token))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, variable.GetLocation(), fieldSymbol.Name));
        }
    }

    private static bool IsLazyType(ITypeSymbol type, ImmutableArray<INamedTypeSymbol> lazyTypes)
    {
        return type is INamedTypeSymbol named
            && MetadataTypes.Contains(lazyTypes, named.OriginalDefinition);
    }

    // LazyThreadSafetyMode.None, and the isThreadSafe: false overload that means the same thing.
    private static bool IsUnsynchronized(
        IMethodSymbol constructor,
        ArgumentListSyntax? argumentList,
        ImmutableArray<INamedTypeSymbol> modeTypes,
        int noneValue,
        SemanticModel model,
        CancellationToken token)
    {
        return FindArgument(constructor, argumentList, parameter => MetadataTypes.Contains(modeTypes, parameter.Type))
                   is { } modeArgument
            ? model.GetConstantValue(modeArgument.Expression, token) is { HasValue: true, Value: int mode }
              && mode == noneValue
            : FindArgument(constructor, argumentList, static parameter => string.Equals(parameter.Name, "isThreadSafe", StringComparison.Ordinal))
                  is { } flagArgument
              && model.GetConstantValue(flagArgument.Expression, token) is { HasValue: true, Value: false };
    }

    private static ArgumentSyntax? FindArgument(
        IMethodSymbol constructor,
        ArgumentListSyntax? argumentList,
        Func<IParameterSymbol, bool> matches)
    {
        if (argumentList is null)
        {
            return null;
        }

        foreach (var parameter in constructor.Parameters)
        {
            if (!matches(parameter))
            {
                continue;
            }

            var argument = FindArgumentForParameter(argumentList.Arguments, parameter);
            if (argument is not null)
            {
                return argument;
            }
        }

        return null;
    }

    private static ArgumentSyntax? FindArgumentForParameter(
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        IParameterSymbol parameter)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];

            if (argument.NameColon is { } nameColon)
            {
                if (string.Equals(
                        nameColon.Name.Identifier.ValueText,
                        parameter.Name,
                        StringComparison.Ordinal))
                {
                    return argument;
                }

                continue;
            }

            if (index == parameter.Ordinal)
            {
                return argument;
            }
        }

        return null;
    }
}
