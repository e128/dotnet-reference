using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128190: Flags a <c lang="csharp">System.IO.FileStream</c> construction that cannot run
///     asynchronous I/O.
/// </summary>
/// <remarks>
///     A <c lang="csharp">FileStream</c> runs asynchronous I/O only when the constructor receives
///     <c lang="csharp">useAsync: true</c> or a <c lang="csharp">FileOptions</c> value that includes
///     <c lang="csharp">FileOptions.Asynchronous</c>. The rule finds each argument through the
///     constructor parameter, so positional and named forms both match. It stays silent when an
///     argument is not constant, when the constructor has no asynchronous switch, and when the
///     created type is not the framework type.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FileStreamWithoutUseAsyncAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128190";

    private const string FileStreamMetadataName = "System.IO.FileStream";

    private const string FileOptionsMetadataName = "System.IO.FileOptions";

    private const string UseAsyncParameterName = "useAsync";

    private const string OptionsParameterName = "options";

    private const string AsynchronousFieldName = "Asynchronous";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "FileStream created without asynchronous I/O",
        "'{0}' creates a FileStream that runs synchronous I/O because the call sets no 'useAsync' or 'FileOptions.Asynchronous'",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true,
        "A FileStream runs asynchronous I/O only when the constructor receives useAsync: true " +
        "or a FileOptions value that includes FileOptions.Asynchronous. Without either, every " +
        "asynchronous read or write blocks a thread, so a caller that awaits the stream loses " +
        "the scalability the asynchronous API promises.");

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
        var fileStreamType = context.Compilation.GetTypeByMetadataName(FileStreamMetadataName);
        var fileOptionsType = context.Compilation.GetTypeByMetadataName(FileOptionsMetadataName);

        if (fileStreamType is null || fileOptionsType is null)
        {
            return;
        }

        var asynchronousField = fileOptionsType
            .GetMembers(AsynchronousFieldName)
            .OfType<IFieldSymbol>()
            .FirstOrDefault();

        if (asynchronousField?.ConstantValue is not int asynchronousMask || asynchronousMask == 0)
        {
            return;
        }

        context.RegisterOperationAction(
            operationContext => AnalyzeObjectCreation(
                operationContext,
                fileStreamType,
                fileOptionsType,
                asynchronousMask),
            OperationKind.ObjectCreation);
    }

    private static void AnalyzeObjectCreation(
        OperationAnalysisContext context,
        INamedTypeSymbol fileStreamType,
        INamedTypeSymbol fileOptionsType,
        int asynchronousMask)
    {
        var creation = (IObjectCreationOperation)context.Operation;

        if (creation.Type is null
            || !SymbolEqualityComparer.Default.Equals(creation.Type.OriginalDefinition, fileStreamType)
            || creation.Constructor is null)
        {
            return;
        }

        var useAsyncParameter = FindParameter(creation.Constructor, UseAsyncParameterName);
        var optionsParameter = FindOptionsParameter(creation.Constructor, fileOptionsType);

        if (useAsyncParameter is null && optionsParameter is null)
        {
            return;
        }

        if (useAsyncParameter is not null
            && !IsSynchronousUseAsyncArgument(creation, useAsyncParameter))
        {
            return;
        }

        if (optionsParameter is not null
            && !IsSynchronousOptions(creation, optionsParameter, asynchronousMask))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            creation.Syntax.GetLocation(),
            context.ContainingSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    // An omitted useAsync argument leaves the stream in synchronous mode. A non-boolean constant
    // cannot occur on a bool parameter, so it stays silent rather than guessing.
    private static bool IsSynchronousUseAsyncArgument(
        IObjectCreationOperation creation,
        IParameterSymbol parameter)
    {
        var argument = FindArgument(creation, parameter);
        if (argument is null)
        {
            return true;
        }

        var constant = Unwrap(argument.Value).ConstantValue;
        return constant.HasValue && constant.Value is bool useAsync && !useAsync;
    }

    // The stream runs asynchronous I/O when the FileOptions value carries the Asynchronous bit.
    // Any other constant, including FileOptions.None, keeps synchronous I/O.
    private static bool IsSynchronousOptions(
        IObjectCreationOperation creation,
        IParameterSymbol parameter,
        int asynchronousMask)
    {
        var argument = FindArgument(creation, parameter);
        if (argument is null)
        {
            return true;
        }

        var constant = Unwrap(argument.Value).ConstantValue;
        return constant.HasValue
            && constant.Value is int options
            && (options & asynchronousMask) == 0;
    }

    private static IArgumentOperation? FindArgument(
        IObjectCreationOperation creation,
        IParameterSymbol parameter)
    {
        return creation.Arguments.FirstOrDefault(
            argument => SymbolEqualityComparer.Default.Equals(argument.Parameter, parameter));
    }

    private static IParameterSymbol? FindParameter(IMethodSymbol constructor, string name)
    {
        return constructor.Parameters.FirstOrDefault(
            parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal));
    }

    // The FileStreamOptions overload also names its parameter options. Matching the type keeps the
    // rule off a FileStreamOptions instance, which this rule does not read.
    private static IParameterSymbol? FindOptionsParameter(
        IMethodSymbol constructor,
        INamedTypeSymbol fileOptionsType)
    {
        return constructor.Parameters.FirstOrDefault(
            parameter => string.Equals(parameter.Name, OptionsParameterName, StringComparison.Ordinal)
                && SymbolEqualityComparer.Default.Equals(parameter.Type, fileOptionsType));
    }

    private static IOperation Unwrap(IOperation operation)
    {
        while (true)
        {
            if (operation is IConversionOperation conversion)
            {
                operation = conversion.Operand;
                continue;
            }

            if (operation is IParenthesizedOperation parenthesized)
            {
                operation = parenthesized.Operand;
                continue;
            }

            return operation;
        }
    }
}
