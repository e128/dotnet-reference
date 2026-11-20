using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128235: A loop that reads a <c lang="csharp">readonly</c> array field of the
///     enclosing type reloads the field on every iteration.
/// </summary>
/// <remarks>
///     The repeated field load also repeats the bounds check on every element access.
///     A local copy declared before the loop removes both. The rule reports the
///     outermost loop that reads the field, so one hoist covers a whole loop cluster.
///     The rule stays silent for a mutable field, a field another type declares, a
///     loop that assigns the field, and a loop with no free derived local name,
///     because the fix cannot repair those shapes safely.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadonlyFieldLoopHoistAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128235";

    private const int MaxNameAttempts = 100;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Readonly field re-read inside a loop",
        "Readonly array field '{0}' is read once per iteration — hoist it into a local before the loop",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true,
        "A local copy declared before the loop lets the JIT drop the repeated field load and the bounds check.");

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
        context.RegisterSyntaxNodeAction(
            AnalyzeLoop,
            SyntaxKind.ForStatement,
            SyntaxKind.ForEachStatement,
            SyntaxKind.ForEachVariableStatement,
            SyntaxKind.WhileStatement,
            SyntaxKind.DoStatement);
    }

    private static void AnalyzeLoop(SyntaxNodeAnalysisContext context)
    {
        var loop = (StatementSyntax)context.Node;

        if (LoopContext.IsWithinAnyLoop(loop))
        {
            return;
        }

        if (TryMatchHoist(loop, context.SemanticModel, context.CancellationToken) is not { } match)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, loop.GetLocation(), match.Field.Name));
    }

    /// <summary>
    ///     Returns the first readonly array field the loop reads once per iteration, with a free
    ///     local name for the hoist. The code fix provider calls the same check, so the fix
    ///     repairs exactly the shapes the rule reports.
    /// </summary>
    internal static (IFieldSymbol Field, string LocalName)? TryMatchHoist(
        StatementSyntax loop,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (loop.Parent is not BlockSyntax)
        {
            return null;
        }

        var enclosingType = GetEnclosingType(semanticModel, loop.SpanStart, cancellationToken);
        if (enclosingType is null)
        {
            return null;
        }

        var matched = FindHoistableField(loop, enclosingType, semanticModel, cancellationToken);
        if (matched is null)
        {
            return null;
        }

        var localName = TryPickLocalName(loop, matched, semanticModel);
        return localName is null ? null : (matched, localName);
    }

    /// <summary>
    ///     Returns the first readonly array field the loop reads once per iteration. Returns
    ///     null when the loop reads none, or reads one through a reference the fix cannot
    ///     replace.
    /// </summary>
    private static IFieldSymbol? FindHoistableField(
        StatementSyntax loop,
        INamedTypeSymbol enclosingType,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        IFieldSymbol? matched = null;
        foreach (var region in GetPerIterationNodes(loop))
        {
            foreach (var node in PerIterationNodes(region))
            {
                if (ClassifyFieldReference(node, enclosingType, semanticModel, cancellationToken, out var field)
                    is RegionMatch.Excluded)
                {
                    return null;
                }

                matched ??= field;
            }
        }

        return matched;
    }

    /// <summary>
    ///     Classifies one node of a per-iteration region against the rule. The fix cannot
    ///     replace a write target or a <c lang="csharp">nameof</c> argument.
    /// </summary>
    private static RegionMatch ClassifyFieldReference(
        SyntaxNode node,
        INamedTypeSymbol enclosingType,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out IFieldSymbol? field)
    {
        field = null;
        if (node is not IdentifierNameSyntax identifier
            || semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol is not IFieldSymbol candidate
            || !IsHoistableField(candidate, enclosingType))
        {
            return RegionMatch.None;
        }

        field = candidate;

        if (IsWriteTarget(identifier))
        {
            return RegionMatch.Excluded;
        }

        if (IsInsideNameOf(identifier))
        {
            return RegionMatch.Excluded;
        }

        return RegionMatch.HoistableRead;
    }

    private enum RegionMatch
    {
        None = 0,
        Excluded = 1,
        HoistableRead = 2
    }

    /// <summary>
    ///     The nodes a loop evaluates once per iteration. A <c lang="csharp">for</c>
    ///     initializer and a <c lang="csharp">foreach</c> source run once, so they stay out.
    /// </summary>
    private static IEnumerable<SyntaxNode> GetPerIterationNodes(StatementSyntax loop)
    {
        switch (loop)
        {
            case ForStatementSyntax forStatement:
                if (forStatement.Condition is not null)
                {
                    yield return forStatement.Condition;
                }

                foreach (var incrementor in forStatement.Incrementors)
                {
                    yield return incrementor;
                }

                yield return forStatement.Statement;
                break;
            case CommonForEachStatementSyntax forEach:
                yield return forEach.Statement;
                break;
            case WhileStatementSyntax whileStatement:
                if (whileStatement.Condition is not null)
                {
                    yield return whileStatement.Condition;
                }

                yield return whileStatement.Statement;
                break;
            case DoStatementSyntax doStatement:
                if (doStatement.Condition is not null)
                {
                    yield return doStatement.Condition;
                }

                yield return doStatement.Statement;
                break;
            default:
                yield break;
        }
    }

    /// <summary>
    ///     Walks a per-iteration region including lambdas, because a captured field still
    ///     reloads on every invocation. A local function body runs on call, not per
    ///     iteration, so the walk stops there and a loop inside it reports on its own.
    /// </summary>
    private static IEnumerable<SyntaxNode> PerIterationNodes(SyntaxNode node)
    {
        yield return node;

        foreach (var child in node.ChildNodes())
        {
            if (child is LocalFunctionStatementSyntax)
            {
                continue;
            }

            foreach (var descendant in PerIterationNodes(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    ///     True for a readonly array field the enclosing type declares. A mutable field stays
    ///     silent, because a concurrent writer could change it and the local copy would hide
    ///     the change. A field another type declares stays outside the rule.
    /// </summary>
    private static bool IsHoistableField(IFieldSymbol field, INamedTypeSymbol enclosingType)
    {
        return field.IsReadOnly
            && field.Type is IArrayTypeSymbol
            && SymbolEqualityComparer.Default.Equals(field.ContainingType, enclosingType);
    }

    /// <summary>
    ///     True for an assignment target, directly or through a
    ///     <c lang="csharp">this._items = value</c> receiver.
    /// </summary>
    private static bool IsWriteTarget(IdentifierNameSyntax identifier)
    {
        return identifier.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == identifier,
            MemberAccessExpressionSyntax { Parent: AssignmentExpressionSyntax assignment } access => assignment.Left == access,
            _ => false
        };
    }

    /// <summary>
    ///     True when the reference sits inside a <c lang="csharp">nameof</c> argument. The
    ///     call carries no symbol of its own, so the name is the only available match.
    /// </summary>
    private static bool IsInsideNameOf(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is InvocationExpressionSyntax invocation)
            {
                return invocation.Expression is IdentifierNameSyntax name
                    && string.Equals(name.Identifier.ValueText, "nameof", StringComparison.Ordinal);
            }

            if (current is MemberDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)
            {
                return false;
            }
        }

        return false;
    }

    private static INamedTypeSymbol? GetEnclosingType(
        SemanticModel semanticModel,
        int position,
        CancellationToken cancellationToken)
    {
        for (var symbol = semanticModel.GetEnclosingSymbol(position, cancellationToken);
             symbol is not null;
             symbol = symbol.ContainingSymbol)
        {
            if (symbol is INamedTypeSymbol type)
            {
                return type;
            }
        }

        return null;
    }

    /// <summary>
    ///     Derives the local name from the field name, then walks the suffix range
    ///     <c lang="csharp">items1</c>, <c lang="csharp">items2</c>, and so on until a
    ///     candidate is free. A method that has none free yields no match, so the rule
    ///     stays silent for that loop.
    /// </summary>
    private static string? TryPickLocalName(
        StatementSyntax loop,
        IFieldSymbol field,
        SemanticModel semanticModel)
    {
        if (DeriveBaseName(field.Name) is not { } baseName)
        {
            return null;
        }

        var taken = CollectTakenNames(loop, baseName);

        for (var attempt = 0; attempt < MaxNameAttempts; attempt++)
        {
            var candidate = attempt is 0
                ? baseName
                : baseName + attempt.ToString(CultureInfo.InvariantCulture);

            if (SyntaxFacts.GetKeywordKind(candidate) is not SyntaxKind.None)
            {
                continue;
            }

            if (taken.Contains(candidate)
                || !semanticModel.LookupSymbols(loop.SpanStart, name: candidate).IsEmpty)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    /// <summary>
    ///     Strips one leading underscore and camel-cases the rest, so `_items` and `Items`
    ///     both derive `items`. A remainder that is empty or starts without a letter, as in
    ///     `_` or `__`, is not a name the fix can build on.
    /// </summary>
    private static string? DeriveBaseName(string fieldName)
    {
        var name = fieldName;
        if (name.Length > 0 && name[0] == '_')
        {
            name = name.Substring(1);
        }

        if (name.Length is 0 || !char.IsLetter(name[0]))
        {
            return null;
        }

        var first = char.ToLowerInvariant(name[0]);
        return first == name[0] ? name : first + name.Substring(1);
    }

    /// <summary>
    ///     Collects every identifier the enclosing member declares or mentions, every name the
    ///     lookup sees at the loop, and the derived names that earlier sibling loops in the
    ///     same block reserve. A fix-all batch computes each fix from one snapshot, so the
    ///     reservation keeps two sibling loops from declaring the same local.
    /// </summary>
    private static HashSet<string> CollectTakenNames(StatementSyntax loop, string baseName)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        AddScopeNames(taken, FindNameScope(loop));

        if (loop.Parent is not BlockSyntax block)
        {
            return taken;
        }

        var reserved = 0;
        foreach (var statement in block.Statements)
        {
            if (statement == loop)
            {
                break;
            }

            if (statement is not (ForStatementSyntax
                or CommonForEachStatementSyntax
                or WhileStatementSyntax
                or DoStatementSyntax))
            {
                continue;
            }

            reserved = ReserveSiblingName(taken, statement, baseName, reserved);
        }

        return taken;
    }

    private static void AddScopeNames(HashSet<string> taken, SyntaxNode? scope)
    {
        if (scope is null)
        {
            return;
        }

        foreach (var token in scope.DescendantTokens())
        {
            if (token.IsKind(SyntaxKind.IdentifierToken))
            {
                taken.Add(token.ValueText);
            }
        }
    }

    /// <summary>
    ///     Reserves the name an earlier sibling loop's own fix would pick, and returns the
    ///     next suffix position.
    /// </summary>
    private static int ReserveSiblingName(HashSet<string> taken, StatementSyntax sibling, string baseName, int reserved)
    {
        foreach (var identifier in sibling.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (DeriveBaseName(identifier.Identifier.ValueText) is not { } derived
                || !string.Equals(derived, baseName, StringComparison.Ordinal))
            {
                continue;
            }

            taken.Add(reserved is 0
                ? baseName
                : baseName + reserved.ToString(CultureInfo.InvariantCulture));
            return reserved + 1;
        }

        return reserved;
    }

    /// <summary>
    ///     The nearest enclosing member, accessor, or local function that holds the loop. The
    ///     walk stops at the type declaration, so a name a sibling method declares stays out.
    /// </summary>
    private static SyntaxNode? FindNameScope(SyntaxNode node)
    {
        SyntaxNode? scope = null;
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is AccessorDeclarationSyntax accessor)
            {
                return accessor;
            }

            if (ancestor is TypeDeclarationSyntax)
            {
                break;
            }

            if (ancestor is MemberDeclarationSyntax or LocalFunctionStatementSyntax)
            {
                scope = ancestor;
            }
        }

        return scope;
    }
}
