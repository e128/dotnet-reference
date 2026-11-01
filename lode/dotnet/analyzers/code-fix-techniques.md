# Code Fix Techniques
*Updated: 2026-11-01T13:40:12Z*

Techniques that prove a fix writes correct code. The shape rules live in
[Code Fix Output](code-fix-output.md). The decision list lives in
[Code Fix Patterns](code-fix-patterns.md).

## Use DocumentEditor for a Multi-Node Edit

`DocumentEditor` records every change against one tree and applies them together.
It also exposes the semantic model and the syntax generator for the document it
edits.

Use it when a fix touches a member and then a reference to that member. A second
call that passes a node from the original tree throws "Syntax node is not within
syntax tree". See [Code Fix Patterns](code-fix-patterns.md) § Tree Identity.

```csharp
var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
editor.ReplaceNode(declaration, newDeclaration);
editor.ReplaceNode(reference, newReference);
return editor.GetChangedDocument();
```

A fix that rewrites one node can keep `root.ReplaceNode`.

## Verify the Rewrite Binds

A shape check predicts the result. A speculative bind proves it. Build the
candidate expression, bind it at the original position, and decline the fix when
the bind fails or lands on the wrong symbol.

```csharp
var candidate = invocation.WithArgumentList(argumentList.AddArguments(newArgument));
var bound = semanticModel.GetSpeculativeSymbolInfo(
    invocation.SpanStart,
    candidate,
    SpeculativeBindingOption.BindAsExpression).Symbol as IMethodSymbol;

if (bound is null
    || bound.Parameters.Length <= parameterIndex
    || !SymbolEqualityComparer.Default.Equals(bound.Parameters[parameterIndex].Type, expectedType))
{
    return null;
}
```

Apply the guard when a fix adds an argument, swaps an overload, or names a
namespace that the new call needs. Import the namespace in a copy of the
compilation first, so the bind sees the new scope:

```csharp
var tree = invocation.SyntaxTree;
var root = tree.GetRoot(cancellationToken);
var annotated = root.ReplaceNode(invocation, candidate.WithAdditionalAnnotations(marker));

// The same helper the fix uses on the real document.
var updated = UsingDirectives.AddIfMissing(annotated, "System.Linq");
var copy = semanticModel.Compilation.ReplaceSyntaxTree(tree, tree.WithRootAndOptions(updated, tree.Options));
```

## Compare the Error Count

A fix that changes a signature can break a caller in another document. Compile
the fixed solution, count the errors, and decline the fix when the count grows.

Limit the check to the documents that reference the changed symbol, found through
`SymbolFinder.FindReferencesAsync`. Use this guard for a return-type change or an
async conversion. Do not use it for a local rewrite: the cost is a full
compilation.

## Build a Member with the Generator

`SyntaxGenerator` writes a type, member, attribute, or argument through the
Roslyn formatting model. `TypeExpression(symbol, addImport: true)` attaches the
import annotation, so a host that runs the code-action post-processing adds and
sorts the using.

The generator picks the shortest name that binds in the current compilation. A
file without the import yields a fully qualified name. The code fix test harness
does not run the post-processing. A qualified name compiles without a using, so
the fix needs no explicit import call.

Use the generator for a new member or a new type name. Keep hand-built syntax for
a local rewrite inside an expression, where the generator adds noise.

## Add or Update an Attribute

A fix that adds an attribute must look for the same attribute first and replace
its argument list. The compiler accepts a duplicate for an `AllowMultiple`
attribute, so the duplicate survives and the analyzer reads the first value.

Match the attribute through its symbol, not its name:

```csharp
var existing = parameter.AttributeLists
    .SelectMany(list => list.Attributes)
    .FirstOrDefault(attribute => SymbolEqualityComparer.Default.Equals(
        semanticModel.GetSymbolInfo(attribute, cancellationToken).Symbol?.ContainingType,
        attributeSymbol));
```

## Prefer the Operation Tree

`RegisterOperationAction` answers exact questions that a syntax walk
approximates: the bound target of a call, the conversion applied to an argument,
and the flow of a value between statements.

Keep the syntax gate in front of the operation when the rule fires rarely. See
[Analyzer Performance](performance.md) § Bind Late.

## Sequential Rename Needs Its Own Fix-All

`SequentialRenameFixAllProvider` in `E128.Analyzers.Style` serves every fix that
renames a symbol through `Renamer.RenameSymbolAsync`. It replaces
`WellKnownFixAllProviders.BatchFixer`, which computes all renames from the
original solution snapshot and then merges. That merge fails when two renames
touch one document, and `dotnet format` reports that it cannot apply Fix All in
Solution.

The provider takes a `Func<Diagnostic, string?, string?>` delegate that computes
the new name, so several fixers share it:

- `NamingStyleCodeFixProvider` (IDE1006) delegates to
  `ComputeCompliantName(diagnostic, symbolName)`.
- `MidNameUnderscoreCodeFixProvider` (E128063) delegates to
  `ComputeFixedName(symbolName)`.

It applies the fixes in this order:

1. Collect every diagnostic in the fix-all scope.
2. Sort the renames back to front within each document, so a span does not drift.
3. Apply `Renamer.RenameSymbolAsync` once at a time to the evolving solution.
4. Skip a rename whose symbol no longer matches the recorded name.

## Related

- [Code Fix Output](code-fix-output.md): valid text, one member, formatter
- [Code Fix Patterns](code-fix-patterns.md): decision list, tables, fix-all
- [Analyzer Performance](performance.md): cost rules
