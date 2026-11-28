using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128260: A <c lang="csharp">Memory&lt;T&gt;</c> local is read after the owner that
///     produced it leaves a <c lang="csharp">using</c> statement. The owner returns its
///     buffer on dispose, so the retained slice reads memory that another caller can reuse.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RetainedMemoryAfterDisposeAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128260";

    private const string MemoryOwnerMetadataName = "System.Buffers.IMemoryOwner`1";

    private const string MemoryManagerMetadataName = "System.Buffers.MemoryManager`1";

    private const string MemoryPropertyName = "Memory";

    private const string SliceMethodName = "Slice";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Retained memory is read after its owner is disposed",
        "'{0}' is read after its owner is disposed: copy the data or finish the read inside the using scope",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "A memory owner returns its buffer on dispose. A Memory<T> slice that outlives the owner can read memory another caller has reused. Copy the data or finish the read inside the using scope.");

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
            var memoryOwner = start.Compilation.GetTypeByMetadataName(MemoryOwnerMetadataName);
            var memoryManager = start.Compilation.GetTypeByMetadataName(MemoryManagerMetadataName);

            if (memoryOwner is null && memoryManager is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, memoryOwner, memoryManager),
                SyntaxKind.UsingStatement);
        });
    }

    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? memoryOwner,
        INamedTypeSymbol? memoryManager)
    {
        if (context.Node is not UsingStatementSyntax usingStatement
            || usingStatement.Parent is not BlockSyntax block
            || usingStatement.Statement is not { } body
            || ResolveOwner(usingStatement, context, memoryOwner, memoryManager) is not { } owner)
        {
            return;
        }

        var retained = FindRetainedLocals(body, block, owner, context);

        if (retained.Count is 0)
        {
            return;
        }

        ReportFirstUse(block, usingStatement, retained, context);
    }

    private static ISymbol? ResolveOwner(
        UsingStatementSyntax usingStatement,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? memoryOwner,
        INamedTypeSymbol? memoryManager)
    {
        return usingStatement.Declaration is not null
            ? ResolveDeclarationOwner(usingStatement.Declaration, context, memoryOwner, memoryManager)
            : ResolveExpressionOwner(usingStatement.Expression, context, memoryOwner, memoryManager);
    }

    private static ISymbol? ResolveDeclarationOwner(
        VariableDeclarationSyntax declaration,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? memoryOwner,
        INamedTypeSymbol? memoryManager)
    {
        if (declaration.Variables.Count is not 1)
        {
            return null;
        }

        var variable = declaration.Variables[0];

        return variable.Initializer is { } initializer
               && IsOwner(
                   context.SemanticModel.GetTypeInfo(initializer.Value, context.CancellationToken).Type,
                   memoryOwner,
                   memoryManager)
            ? context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken)
            : null;
    }

    private static ISymbol? ResolveExpressionOwner(
        ExpressionSyntax? expression,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? memoryOwner,
        INamedTypeSymbol? memoryManager)
    {
        return expression is not null
               && IsOwner(
                   context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type,
                   memoryOwner,
                   memoryManager)
            ? context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol
            : null;
    }

    private static List<ILocalSymbol> FindRetainedLocals(
        StatementSyntax body,
        BlockSyntax block,
        ISymbol owner,
        SyntaxNodeAnalysisContext context)
    {
        var candidates = LocalsDeclaredBefore(block, body, context);
        var retained = new List<ILocalSymbol>();

        foreach (var node in body.DescendantNodesAndSelf())
        {
            if (node is not AssignmentExpressionSyntax assignment
                || !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                || !IsOwnerMemory(assignment.Right, owner, context))
            {
                continue;
            }

            if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is ILocalSymbol local
                && candidates.Contains(local))
            {
                retained.Add(local);
            }
        }

        return retained;
    }

    private static HashSet<ILocalSymbol> LocalsDeclaredBefore(
        BlockSyntax block,
        StatementSyntax body,
        SyntaxNodeAnalysisContext context)
    {
        var locals = new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);

        foreach (var statement in block.Statements)
        {
            if (statement.SpanStart >= body.SpanStart)
            {
                break;
            }

            if (statement is not LocalDeclarationStatementSyntax declaration)
            {
                continue;
            }

            foreach (var variable in declaration.Declaration.Variables)
            {
                if (context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is ILocalSymbol local)
                {
                    locals.Add(local);
                }
            }
        }

        return locals;
    }

    private static bool IsOwnerMemory(
        ExpressionSyntax expression,
        ISymbol owner,
        SyntaxNodeAnalysisContext context)
    {
        var value = ExpressionUnwrapper.Parentheses(expression);

        return value is MemberAccessExpressionSyntax access
            ? IsMemoryAccess(access, owner, context)
            : IsSliceOfMemory(value, owner, context);
    }

    private static bool IsMemoryAccess(
        MemberAccessExpressionSyntax access,
        ISymbol owner,
        SyntaxNodeAnalysisContext context)
    {
        return string.Equals(access.Name.Identifier.ValueText, MemoryPropertyName, StringComparison.Ordinal)
               && IsSameSymbol(access.Expression, owner, context);
    }

    private static bool IsSliceOfMemory(
        ExpressionSyntax value,
        ISymbol owner,
        SyntaxNodeAnalysisContext context)
    {
        if (value is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax call }
            || !string.Equals(call.Name.Identifier.ValueText, SliceMethodName, StringComparison.Ordinal)
            || call.Expression is not MemberAccessExpressionSyntax sliceOwner)
        {
            return false;
        }

        return IsMemoryAccess(sliceOwner, owner, context);
    }

    private static bool IsSameSymbol(
        ExpressionSyntax expression,
        ISymbol owner,
        SyntaxNodeAnalysisContext context)
    {
        return SymbolEqualityComparer.Default.Equals(
            context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol,
            owner);
    }

    private static void ReportFirstUse(
        BlockSyntax block,
        UsingStatementSyntax usingStatement,
        List<ILocalSymbol> retained,
        SyntaxNodeAnalysisContext context)
    {
        var start = usingStatement.Span.End;

        foreach (var node in block.DescendantNodes())
        {
            if (node.SpanStart < start || node is not IdentifierNameSyntax identifier)
            {
                continue;
            }

            var symbol = context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol;
            var local = retained.Find(candidate => SymbolEqualityComparer.Default.Equals(candidate, symbol));

            if (local is null)
            {
                continue;
            }

            if (IsWrite(identifier))
            {
                _ = retained.Remove(local);
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(), local.Name));
            return;
        }
    }

    private static bool IsWrite(IdentifierNameSyntax identifier)
    {
        return identifier.Parent switch
        {
            AssignmentExpressionSyntax assignment => ReferenceEquals(assignment.Left, identifier),
            VariableDeclaratorSyntax => true,
            PrefixUnaryExpressionSyntax => true,
            PostfixUnaryExpressionSyntax => true,
            _ => false,
        };
    }

    private static bool IsOwner(
        ITypeSymbol? type,
        INamedTypeSymbol? memoryOwner,
        INamedTypeSymbol? memoryManager)
    {
        if (type is null)
        {
            return false;
        }

        if (memoryManager is not null && DerivesFromDefinition(type, memoryManager))
        {
            return true;
        }

        if (memoryOwner is null)
        {
            return false;
        }

        foreach (var @interface in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, memoryOwner))
            {
                return true;
            }
        }

        return SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, memoryOwner);
    }

    private static bool DerivesFromDefinition(ITypeSymbol type, INamedTypeSymbol definition)
    {
        if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, definition))
        {
            return true;
        }

        return TypeSymbols.WalkBaseTypes(
            type,
            current => SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, definition),
            includeSelf: false);
    }
}
