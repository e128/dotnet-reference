# Symbol and Semantic Lookups

*Updated: 2026-11-03T03:14:50Z*

Recipes for the question the rule asks once the syntax shape passed. The
binding rules live in [new-analyzer-checklist.md](new-analyzer-checklist.md)
§ Match Symbols, Not Names. The cost rules live in
[performance.md](performance.md).

## Reach a Member by Documentation Comment ID

`GetTypeByMetadataName` reaches a type. A specific overload needs a
documentation comment ID:

```csharp
var symbol = DocumentationCommentId.GetFirstSymbolForDeclarationId(
    "M:System.Text.StringBuilder.Append(System.Char)",
    compilation);
```

The ID format is the one the compiler writes into a documentation file. Take it
from an IDE "Copy Reference" action or from the published API docs.

The call returns null when no type carries the ID. Guard it like any metadata
lookup.

## Two Accessibility Questions

The two questions are different, and the APIs differ.

| Question                                    | Call                                                      |
| ------------------------------------------- | --------------------------------------------------------- |
| May this compilation call the symbol?       | `compilation.IsSymbolAccessibleWithin(symbol, compilation.Assembly)` |
| Is the symbol visible outside its assembly? | Walk `DeclaredAccessibility` from the symbol up through `ContainingType` |

The first call answers a remedy question, so use it when a code fix will write a
call. The second answers a public API question, and it needs the walk. A symbol
counts as visible when every level is `Public`, `Protected`, or
`ProtectedOrInternal`.

## Interface Implementation

An explicit implementation needs no walk:

```csharp
if (member.ExplicitInterfaceImplementations.Length > 0)
{
    // Explicit implementation.
}
```

An implicit implementation needs the interface list and the implementation
lookup:

```csharp
foreach (var interfaceType in member.ContainingType.AllInterfaces)
{
    foreach (var interfaceMember in interfaceType.GetMembers(member.Name))
    {
        if (SymbolEqualityComparer.Default.Equals(
                member.ContainingType.FindImplementationForInterfaceMember(interfaceMember),
                member))
        {
            return true;
        }
    }
}
```

## Operator and Extension Method Predicates

```csharp
methodSymbol.MethodKind is MethodKind.UserDefinedOperator or MethodKind.Conversion
methodSymbol.IsExtensionMethod
```

`MethodKind.Conversion` covers both the implicit and the explicit conversion
operator. Test `MethodKind.UserDefinedOperator` for an arithmetic or comparison
operator.

## Namespace and Display Name

`ContainingNamespace` is null for the global namespace. A null-conditional call
returns null, and the caller decides the default.

```csharp
var format = SymbolDisplayFormat.FullyQualifiedFormat
    .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);
var namespaceName = type.ContainingNamespace?.ToDisplayString(format);
```

Omit the `global::` prefix when the text names a namespace rather than a type.

## Data Flow and Control Flow

Both APIs return a result with a `Succeeded` flag, and both return a result with
no content when the region does not compile.

```csharp
var dataFlow = semanticModel.AnalyzeDataFlow(node);
if (!dataFlow.Succeeded)
{
    return;
}
```

`DataFlowAnalysis` exposes `ReadInside`, `WrittenInside`, `VariablesDeclared`,
`CapturedInside`, and `UsedLocalFunctions`. `ControlFlowAnalysis` exposes
`EntryPoints`, `ExitPoints`, `StartPointIsReachable`, `EndPointIsReachable`, and
`ReturnStatements`.

`CapturedInside` is the cheapest answer to "does this lambda close over a
local". Use it instead of an ancestry walk.

## Language Version

The syntax tree carries the parse options, and the options carry the version:

```csharp
if (node.SyntaxTree.Options is CSharpParseOptions options
    && options.LanguageVersion < LanguageVersion.CSharp10)
{
    return;
}
```

Compare against a `LanguageVersion` member. Never parse the version from a
string, and never infer the framework version from an assembly version. An
assembly version heuristic breaks on .NET Framework, on Mono, and on a custom
reference set.

## Scope Work to One Symbol

A rule that applies to one symbol registers a scoped start action, so the
callback runs only for that symbol. Three levels exist.

| Registration                     | Scope                                  |
| -------------------------------- | -------------------------------------- |
| `RegisterSymbolStartAction`      | One named type or method, plus its end |
| `RegisterOperationBlockStartAction` | One method, accessor, or field body |
| `RegisterCompilationStartAction` | The whole compilation                  |

The middle level answers "only inside a member that carries this attribute":

```csharp
start.RegisterOperationBlockStartAction(block =>
{
    if (!block.OwningSymbol.GetAttributes().Any(
            attribute => SymbolEqualityComparer.Default
                .Equals(attribute.AttributeClass, auditedAttribute)))
    {
        return;
    }

    block.RegisterOperationAction(
        operation => AnalyzeInvocation(operation),
        OperationKind.Invocation);
});
```

The nested registration is the point. The inner action runs only for the
attributed body, so the rule pays no cost elsewhere.

`RegisterSymbolStartAction` also carries `RegisterSymbolEndAction`, which runs
after the symbol's own callbacks. Both are available from Roslyn 3.0 onward.

## Related

- [New Analyzer Checklist](new-analyzer-checklist.md): gates and symbol rules
- [Analyzer Performance](performance.md): cost of each lookup
- [Code Fix Output](code-fix-output.md): trivia and emitted text
- [Analyzer Testing](testing.md): driving the path in a test
