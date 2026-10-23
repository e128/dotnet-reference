# Analyzer Performance

*Updated: 2026-10-23T21:43:00Z*

Every analyzer in `src/E128.Analyzers` runs on each compilation and each edit. A
slow analyzer multiplies its cost by the number of nodes it registers for.
These rules keep the cost near linear in the size of the file.

## Bind Late

`SemanticModel.GetSymbolInfo` is the expensive call. Roslyn caches the result per
node, but the first bind still costs a lookup plus allocation. Gate on syntax
shape first, then bind.

```csharp
var methodName = invocation.Expression switch
{
    MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
    IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
    _ => null
};

if (methodName is not ("Create" or "CreateRange"))
{
    return;
}

// Shape checks passed. Bind once, then verify the containing type.
if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
        is not IMethodSymbol method
    || !SymbolEqualityComparer.Default.Equals(method.ContainingType, immutableArrayType))
{
    return;
}
```

A syntax gate must reject only calls that the symbol check also rejects. Add the
semantic check back after the gates so the diagnostic set does not change.

## Resolve Types Once Per Compilation

Call `GetTypeByMetadataName` in `RegisterCompilationStartAction`, then compare
with `SymbolEqualityComparer.Default`. Do not compare `ToDisplayString` output in
a per-node path.

```csharp
context.RegisterCompilationStartAction(start =>
{
    var taskType = start.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
    if (taskType is null)
    {
        return;
    }

    start.RegisterSyntaxNodeAction(node => Analyze(node, taskType), SyntaxKind.InvocationExpression);
});
```

## Walk Each Scope Once

A detector that scans the enclosing method, block, or lambda for each candidate
node turns linear work into quadratic work. Scan once, store the result, and
reuse it through a `ConcurrentDictionary` created at compilation start.

```csharp
var scans = new ConcurrentDictionary<MethodDeclarationSyntax, (bool HasAwait, string? SyncIoCall)>();
var (hasAwait, syncIoCall) = scans.GetOrAdd(method, m =>
    MethodContainsAwait(m)
        ? (true, null)
        : (false, FindSyncIoCall(m, model, cancellationToken)));
```

`ConcurrentDictionary` rejects a null value. Use a tuple with a `Found` flag, or
a non-null wrapper, when the cached result can be absent.

## Use Spans, Not Ancestry Walks

`Span.Contains(other.Span)` answers "is this node inside that subtree" in
constant time. `DescendantNodes().Contains(node)` costs the size of the subtree
and multiplies across candidates.

Check `IsChildOf`/ancestor loops the same way, with a span test at each step.

## Use Symbols Instead of Rescans

`ILocalSymbol.DeclaringSyntaxReferences` reaches the declaration without a walk
over the enclosing block. Guard the reference's `SyntaxTree` against the
current tree before calling `GetSyntax`.

## Build One Index Per Scope

When a detection needs source order, build a position list once per scope,
sorted by document order, and query it with `BinarySearch`.

```csharp
var positions = new Dictionary<string, List<int>>(StringComparer.Ordinal);
// Fill from one DescendantNodes walk, which is in document order.
var index = positions[name].BinarySearch(writePosition);
if (index < 0)
{
    index = ~index;
}
```

## Cache Derived Views on Fact Objects

A collector that already walks a test method can hold derived data. Store the
argument subtrees and the returned literal set on the fact object, then let each
detector read the stored value. A lazy property keeps a method that never needs
the view free of the cost.

## Exemptions

- A syntax walk that exists only to answer one question at one node needs no
  cache. Add the cache when a loop calls it.
- An analyzer disabled by default still pays the parse cost. Keep its gates
  early even when the rule is off.

## Related

- [Analyzer Strategy](../analyzers.md)
- [New Analyzer Checklist](new-analyzer-checklist.md)
- [.NET Performance](../performance.md)
