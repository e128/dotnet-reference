using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

/// <summary>
///     E128256: A logger call passes a local filesystem path. A log entry outlives the
///     request and reaches every reader of the log store, so the path leaks the local
///     directory layout. Log the file name or an identifier instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LogPathLeakAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128256";

    internal const string FullNamePropertyName = "FullName";

    internal const string NamePropertyName = "Name";

    private const string LoggerExtensionsMetadataName = "Microsoft.Extensions.Logging.LoggerExtensions";

    private const string LoggerMetadataName = "Microsoft.Extensions.Logging.ILogger";

    private const string LoggerMessageAttributeName = "Microsoft.Extensions.Logging.LoggerMessageAttribute";

    private const string FileSystemInfoMetadataName = "System.IO.FileSystemInfo";

    private const string PathMetadataName = "System.IO.Path";

    private const string GetFullPathName = "GetFullPath";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Avoid logging a local filesystem path",
        "Logger call passes '{0}', a local filesystem path: log the file name or an identifier instead",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true,
        "A log entry outlives the request and reaches every reader of the log store. A full path leaks the local directory layout. Log the file name or an identifier instead.");

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
            if (XunitTestHelper.IsTestCompilation(start.Compilation, start.CancellationToken))
            {
                return;
            }

            var loggerExtensions = start.Compilation.GetTypeByMetadataName(LoggerExtensionsMetadataName);
            var logger = start.Compilation.GetTypeByMetadataName(LoggerMetadataName);

            if (loggerExtensions is null && logger is null)
            {
                return;
            }

            var fileSystemInfo = start.Compilation.GetTypeByMetadataName(FileSystemInfoMetadataName);
            var path = start.Compilation.GetTypeByMetadataName(PathMetadataName);
            var loggerMessageAttribute = start.Compilation.GetTypeByMetadataName(LoggerMessageAttributeName);

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, loggerExtensions, logger, fileSystemInfo, path, loggerMessageAttribute),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? loggerExtensions,
        INamedTypeSymbol? logger,
        INamedTypeSymbol? fileSystemInfo,
        INamedTypeSymbol? path,
        INamedTypeSymbol? loggerMessageAttribute)
    {
        if (context.Node is not InvocationExpressionSyntax syntax
            || context.SemanticModel.GetSymbolInfo(syntax, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsLoggerCall(method, loggerExtensions, logger, loggerMessageAttribute))
        {
            return;
        }

        ScanLeaks(context, syntax, fileSystemInfo, path);
        ScanStagedLocals(context, syntax, fileSystemInfo, path);
    }

    private static void ScanLeaks(
        SyntaxNodeAnalysisContext context,
        SyntaxNode syntax,
        INamedTypeSymbol? fileSystemInfo,
        INamedTypeSymbol? path)
    {
        foreach (var node in syntax.DescendantNodesAndSelf())
        {
            if (IsInsideNameOf(node))
            {
                continue;
            }

            if (node is MemberAccessExpressionSyntax access
                && IsFullPathProperty(access, context.SemanticModel, fileSystemInfo, context.CancellationToken))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, access.GetLocation(), access.ToString()));
            }
            else if (node is InvocationExpressionSyntax invocation
                     && IsGetFullPath(invocation, context.SemanticModel, path, context.CancellationToken))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), invocation.ToString()));
            }
            else if (node is InvocationExpressionSyntax toString
                     && IsFileSystemInfoToString(toString, context.SemanticModel, fileSystemInfo, context.CancellationToken))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, toString.GetLocation(), toString.ToString()));
            }
        }
    }

    private static void ScanStagedLocals(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax syntax,
        INamedTypeSymbol? fileSystemInfo,
        INamedTypeSymbol? path)
    {
        foreach (var argument in syntax.ArgumentList.Arguments)
        {
            if (ExpressionUnwrapper.Parentheses(argument.Expression) is not IdentifierNameSyntax identifier
                || context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol is not ILocalSymbol local)
            {
                continue;
            }

            var declarator = local.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax(context.CancellationToken))
                .OfType<VariableDeclaratorSyntax>()
                .FirstOrDefault();

            if (declarator?.Initializer?.Value is { } initializer
                && !syntax.Span.Contains(initializer.Span))
            {
                ScanLeaks(context, initializer, fileSystemInfo, path);
            }
        }
    }

    private static bool IsInsideNameOf(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is InvocationExpressionSyntax invocation
                && invocation.Expression is IdentifierNameSyntax identifier
                && string.Equals(identifier.Identifier.ValueText, "nameof", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLoggerCall(
        IMethodSymbol method,
        INamedTypeSymbol? loggerExtensions,
        INamedTypeSymbol? logger,
        INamedTypeSymbol? loggerMessageAttribute)
    {
        if (HasLoggerMessageAttribute(method, loggerMessageAttribute))
        {
            return true;
        }

        var containingType = method.ContainingType;

        return (loggerExtensions is not null
                && SymbolEqualityComparer.Default.Equals(containingType, loggerExtensions))
               || (logger is not null
                   && SymbolEqualityComparer.Default.Equals(containingType, logger));
    }

    private static bool HasLoggerMessageAttribute(
        IMethodSymbol method,
        INamedTypeSymbol? loggerMessageAttribute)
    {
        if (loggerMessageAttribute is null)
        {
            return false;
        }

        return HasAttribute(method, loggerMessageAttribute)
               || (method.PartialDefinitionPart is { } definition
                   && HasAttribute(definition, loggerMessageAttribute))
               || (method.PartialImplementationPart is { } implementation
                   && HasAttribute(implementation, loggerMessageAttribute));
    }

    private static bool HasAttribute(IMethodSymbol method, INamedTypeSymbol attributeType)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFileSystemInfoToString(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        INamedTypeSymbol? fileSystemInfo,
        CancellationToken cancellationToken)
    {
        return invocation.ArgumentList.Arguments.Count is 0
               && invocation.Expression is MemberAccessExpressionSyntax access
               && string.Equals(access.Name.Identifier.ValueText, "ToString", StringComparison.Ordinal)
               && fileSystemInfo is not null
               && TypeSymbols.DerivesFrom(
                   model.GetTypeInfo(access.Expression, cancellationToken).Type,
                   fileSystemInfo);
    }

    private static bool IsFullPathProperty(
        MemberAccessExpressionSyntax access,
        SemanticModel model,
        INamedTypeSymbol? fileSystemInfo,
        CancellationToken cancellationToken)
    {
        return string.Equals(access.Name.Identifier.ValueText, FullNamePropertyName, StringComparison.Ordinal)
               && model.GetSymbolInfo(access, cancellationToken).Symbol is IPropertySymbol property
               && fileSystemInfo is not null
               && TypeSymbols.DerivesFrom(property.ContainingType, fileSystemInfo);
    }

    private static bool IsGetFullPath(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        INamedTypeSymbol? path,
        CancellationToken cancellationToken)
    {
        return model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method
               && string.Equals(method.Name, GetFullPathName, StringComparison.Ordinal)
               && path is not null
               && SymbolEqualityComparer.Default.Equals(method.ContainingType, path);
    }
}
