using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

/// <summary>
///     E128263: A containment test compares a resolved path to a root with
///     <c lang="csharp">StartsWith</c> and no directory separator. A sibling whose name
///     extends the root name passes the test and escapes the directory.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PathContainmentPrefixAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128263";

    internal const string StartsWithName = "StartsWith";

    internal const string GetFullPathName = "GetFullPath";

    internal const string FullNamePropertyName = "FullName";

    internal const string DirectorySeparatorName = "DirectorySeparatorChar";

    internal const string AltDirectorySeparatorName = "AltDirectorySeparatorChar";

    private const string FileSystemInfoMetadataName = "System.IO.FileSystemInfo";

    private const string PathMetadataName = "System.IO.Path";

    private const int MaxDepth = 8;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Containment test compares a root with no directory separator",
        "Containment test compares a path to '{0}' with no directory separator: append Path.DirectorySeparatorChar so a sibling name cannot pass",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true,
        "A StartsWith test on a resolved path accepts a sibling whose name extends the root name. Append Path.DirectorySeparatorChar to the root before the comparison.");

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
            var path = start.Compilation.GetTypeByMetadataName(PathMetadataName);

            if (path is null)
            {
                return;
            }

            var fileSystemInfo = start.Compilation.GetTypeByMetadataName(FileSystemInfoMetadataName);

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, path, fileSystemInfo),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol path,
        INamedTypeSymbol? fileSystemInfo)
    {
        if (context.Node is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !TryGetStringRoot(invocation, context, out var root))
        {
            return;
        }

        if (!ShouldReport(memberAccess.Expression, root, context, path, fileSystemInfo))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), root.ToString()));
    }

    private static bool ShouldReport(
        ExpressionSyntax receiver,
        ExpressionSyntax root,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol path,
        INamedTypeSymbol? fileSystemInfo)
    {
        var trace = new Trace(context.SemanticModel, path, fileSystemInfo, context.CancellationToken);

        return IsResolvedPath(receiver, trace, 0)
               && !IsSafeRoot(root, context.SemanticModel, path, context.CancellationToken);
    }

    private static bool TryGetStringRoot(
        InvocationExpressionSyntax invocation,
        SyntaxNodeAnalysisContext context,
        [NotNullWhen(true)] out ExpressionSyntax? root)
    {
        root = null;

        return IsStringStartsWith(invocation, context) && TryGetValueArgument(invocation, out root);
    }

    private static bool IsStringStartsWith(
        InvocationExpressionSyntax invocation,
        SyntaxNodeAnalysisContext context)
    {
        return context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method
               && method.ContainingType?.SpecialType is SpecialType.System_String
               && string.Equals(method.Name, StartsWithName, StringComparison.Ordinal)
               && method.Parameters.Length > 0
               && method.Parameters[0].Type.SpecialType is SpecialType.System_String;
    }

    private static bool TryGetValueArgument(
        InvocationExpressionSyntax invocation,
        [NotNullWhen(true)] out ExpressionSyntax? root)
    {
        root = null;

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            var name = argument.NameColon?.Name.Identifier.ValueText;

            if (name is null || string.Equals(name, "value", StringComparison.Ordinal))
            {
                root = argument.Expression;
                return true;
            }
        }

        return false;
    }

    private static bool IsResolvedPath(ExpressionSyntax expression, Trace trace, int depth)
    {
        if (depth > MaxDepth)
        {
            return false;
        }

        var value = ExpressionUnwrapper.Parentheses(expression);

        if (value is InvocationExpressionSyntax invocation && IsGetFullPath(invocation, trace))
        {
            return true;
        }

        if (value is MemberAccessExpressionSyntax access && IsFullName(access, trace))
        {
            return true;
        }

        if (trace.Model.GetSymbolInfo(value, trace.CancellationToken).Symbol is not { } symbol)
        {
            return false;
        }

        if (symbol is ILocalSymbol local)
        {
            return IsResolvedLocal(value, local, trace, depth);
        }

        return symbol is IFieldSymbol or IPropertySymbol
               && IsResolvedMember(value, symbol, trace, depth);
    }

    private static bool IsResolvedLocal(ExpressionSyntax use, ILocalSymbol local, Trace trace, int depth)
    {
        if (!trace.Visited.Add(local))
        {
            return false;
        }

        var last = LastWrittenValue(use, local, trace.Model, trace.CancellationToken);

        return last is not null && IsResolvedPath(last, trace, depth + 1);
    }

    private static bool IsResolvedMember(ExpressionSyntax use, ISymbol symbol, Trace trace, int depth)
    {
        var last = LastWrittenValue(use, symbol, trace.Model, trace.CancellationToken);

        return last is not null && IsResolvedPath(last, trace, depth + 1);
    }

    private static bool IsGetFullPath(InvocationExpressionSyntax invocation, Trace trace)
    {
        return trace.Model.GetSymbolInfo(invocation, trace.CancellationToken).Symbol is IMethodSymbol method
               && string.Equals(method.Name, GetFullPathName, StringComparison.Ordinal)
               && SymbolEqualityComparer.Default.Equals(method.ContainingType, trace.Path);
    }

    private static bool IsFullName(MemberAccessExpressionSyntax access, Trace trace)
    {
        return string.Equals(access.Name.Identifier.ValueText, FullNamePropertyName, StringComparison.Ordinal)
               && trace.Model.GetSymbolInfo(access, trace.CancellationToken).Symbol is IPropertySymbol property
               && trace.FileSystemInfo is not null
               && TypeSymbols.DerivesFrom(property.ContainingType, trace.FileSystemInfo);
    }

    private static ExpressionSyntax? LastWrittenValue(
        SyntaxNode use,
        ISymbol symbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var assignment = LastAssignment(use, symbol, model, cancellationToken);
        var initializer = DeclaratorInitializer(symbol, use, cancellationToken);

        return initializer is not null && (assignment is null || initializer.SpanStart > assignment.SpanStart)
            ? initializer
            : assignment;
    }

    private static ExpressionSyntax? LastAssignment(
        SyntaxNode use,
        ISymbol symbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        ExpressionSyntax? last = null;
        var position = -1;
        var block = use.FirstAncestorOrSelf<BlockSyntax>();

        if (block is null)
        {
            return null;
        }

        foreach (var assignment in block.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (!IsAssignmentTo(assignment, use, symbol, model, cancellationToken))
            {
                continue;
            }

            if (assignment.SpanStart > position)
            {
                last = assignment.Right;
                position = assignment.SpanStart;
            }
        }

        return last;
    }

    private static bool IsAssignmentTo(
        AssignmentExpressionSyntax assignment,
        SyntaxNode use,
        ISymbol symbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return assignment.Span.End <= use.SpanStart
               && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
               && SymbolEqualityComparer.Default.Equals(
                   model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol,
                   symbol);
    }

    private static ExpressionSyntax? DeclaratorInitializer(
        ISymbol symbol,
        SyntaxNode use,
        CancellationToken cancellationToken)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            var syntax = reference.GetSyntax(cancellationToken);

            if (syntax is VariableDeclaratorSyntax { Initializer.Value: { } value }
                && value.SpanStart <= use.SpanStart)
            {
                return value;
            }

            if (syntax is PropertyDeclarationSyntax { Initializer.Value: { } propertyValue }
                && propertyValue.SpanStart <= use.SpanStart)
            {
                return propertyValue;
            }
        }

        return null;
    }

    private static bool IsSafeRoot(
        ExpressionSyntax expression,
        SemanticModel model,
        INamedTypeSymbol path,
        CancellationToken cancellationToken)
    {
        var value = ExpressionUnwrapper.Parentheses(expression);

        if (IsSafeValue(value, model, path, cancellationToken))
        {
            return true;
        }

        if (model.GetSymbolInfo(value, cancellationToken).Symbol is not { } symbol)
        {
            return false;
        }

        if (symbol is ILocalSymbol or IParameterSymbol)
        {
            return IsSafeLocal(expression, symbol, model, path, cancellationToken);
        }

        return symbol is IFieldSymbol or IPropertySymbol
               && AllWritesSafe(symbol, model, path, cancellationToken);
    }

    private static bool IsSafeLocal(
        ExpressionSyntax use,
        ISymbol symbol,
        SemanticModel model,
        INamedTypeSymbol path,
        CancellationToken cancellationToken)
    {
        var last = LastWrittenValue(use, symbol, model, cancellationToken);

        return last is not null && IsSafeValue(last, model, path, cancellationToken);
    }

    private static bool AllWritesSafe(
        ISymbol symbol,
        SemanticModel model,
        INamedTypeSymbol path,
        CancellationToken cancellationToken)
    {
        var writes = Writes(symbol, model, cancellationToken);

        if (writes.Count is 0)
        {
            return false;
        }

        foreach (var write in writes)
        {
            if (!IsSafeValue(write, model, path, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private static List<ExpressionSyntax> Writes(
        ISymbol symbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var writes = new List<ExpressionSyntax>();

        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            var syntax = reference.GetSyntax(cancellationToken);

            if (syntax is VariableDeclaratorSyntax { Initializer.Value: { } value })
            {
                writes.Add(value);
            }

            if (syntax is PropertyDeclarationSyntax { Initializer.Value: { } propertyValue })
            {
                writes.Add(propertyValue);
            }
        }

        var container = symbol.ContainingType?.DeclaringSyntaxReferences
            .FirstOrDefault()?
            .GetSyntax(cancellationToken);

        if (container is not TypeDeclarationSyntax typeDeclaration)
        {
            return writes;
        }

        foreach (var assignment in typeDeclaration.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                && SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol,
                    symbol))
            {
                writes.Add(assignment.Right);
            }
        }

        return writes;
    }

    private static bool IsSafeValue(
        ExpressionSyntax expression,
        SemanticModel model,
        INamedTypeSymbol path,
        CancellationToken cancellationToken)
    {
        var value = ExpressionUnwrapper.Parentheses(expression);

        if (AppendsSeparator(value, model, path, cancellationToken))
        {
            return true;
        }

        if (value is InterpolatedStringExpressionSyntax interpolated)
        {
            return EndsWithSeparatorText(interpolated);
        }

        var constant = model.GetConstantValue(value, cancellationToken);

        return constant.HasValue
               && constant.Value is string text
               && (text.EndsWith("/", StringComparison.Ordinal) || text.EndsWith("\\", StringComparison.Ordinal));
    }

    private static bool AppendsSeparator(
        ExpressionSyntax expression,
        SemanticModel model,
        INamedTypeSymbol path,
        CancellationToken cancellationToken)
    {
        return expression is BinaryExpressionSyntax binary
               && binary.IsKind(SyntaxKind.AddExpression)
               && IsSeparator(binary.Right, model, path, cancellationToken);
    }

    private static bool IsSeparator(
        ExpressionSyntax expression,
        SemanticModel model,
        INamedTypeSymbol path,
        CancellationToken cancellationToken)
    {
        var value = ExpressionUnwrapper.Parentheses(expression);

        if (value is MemberAccessExpressionSyntax access
            && IsSeparatorName(access.Name.Identifier.ValueText)
            && model.GetSymbolInfo(access, cancellationToken).Symbol is IFieldSymbol field
            && SymbolEqualityComparer.Default.Equals(field.ContainingType, path))
        {
            return true;
        }

        var constant = model.GetConstantValue(value, cancellationToken);

        if (!constant.HasValue)
        {
            return false;
        }

        if (constant.Value is string text)
        {
            return text.EndsWith("/", StringComparison.Ordinal) || text.EndsWith("\\", StringComparison.Ordinal);
        }

        return constant.Value is char character && (character == '/' || character == '\\');
    }

    private static bool IsSeparatorName(string name)
    {
        return string.Equals(name, DirectorySeparatorName, StringComparison.Ordinal)
               || string.Equals(name, AltDirectorySeparatorName, StringComparison.Ordinal);
    }

    private static bool EndsWithSeparatorText(InterpolatedStringExpressionSyntax interpolated)
    {
        var contents = interpolated.Contents;

        if (contents.Count is 0
            || contents.Last() is not InterpolatedStringTextSyntax text)
        {
            return false;
        }

        var value = text.TextToken.ValueText;

        return value.EndsWith("/", StringComparison.Ordinal)
               || value.EndsWith("\\", StringComparison.Ordinal);
    }

    private sealed class Trace
    {
        public Trace(
            SemanticModel model,
            INamedTypeSymbol path,
            INamedTypeSymbol? fileSystemInfo,
            CancellationToken cancellationToken)
        {
            Model = model;
            Path = path;
            FileSystemInfo = fileSystemInfo;
            CancellationToken = cancellationToken;
            Visited = new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
        }

        public SemanticModel Model { get; }

        public INamedTypeSymbol Path { get; }

        public INamedTypeSymbol? FileSystemInfo { get; }

        public CancellationToken CancellationToken { get; }

        public HashSet<ILocalSymbol> Visited { get; }
    }
}
