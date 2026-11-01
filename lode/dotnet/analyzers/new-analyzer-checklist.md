# New Analyzer Checklist
*Updated: 2026-11-01T01:49:31Z*

Pre-flight checks and common pitfalls when writing new E128.Analyzers rules.

## Decide Before You Write It

Answer these five questions before the first line of a new rule. Each answer
changes the registration, not the message text.

1. Which API does the remedy need? Gate the rule at compilation start when
   that API is absent. See § Gate on API Availability.
2. Does the remedy need a type or a member? Check the member, not only the
   containing type.
3. How does the rule identify the construct: by a name or by a symbol? Match
   the symbol. See § Match Symbols, Not Names.
4. Can the code fix repair everything the rule reports? Narrow the report
   until it can. A structural change needs an exclusion for every context that
   forbids it, such as a generic constraint, an override, or a partial type.
5. Does the fix read a table that this analyzer owns? Declare the table here
   as `internal static readonly`. Never copy it into the fix provider.

## Netstandard2.0 Constraints

The analyzer project targets `netstandard2.0`. This imposes constraints that do not surface until compile time:

- **String equality**: use `string.Equals(a, b, StringComparison.Ordinal)` instead of `a != b` or `a == b`. Raw `!=` and `==` on strings triggers MA0006 (`UseStringComparison`) in the analyzer project because netstandard2.0 lacks the `StringComparer`-aware operators. Test projects (net10.0) do not trigger this, so the error only appears when building the analyzer itself.
- **No `Span<T>` / `Memory<T>`**: unavailable without polyfills
- **No default interface methods**: unsupported by the runtime
- **No `IAsyncEnumerable<T>`**: unavailable without polyfills
- **PolySharp**: used to polyfill `NotNullWhenAttribute` (see `analyzers.md § Netstandard2.0 Nullable Polyfill`)

## Common Nullable Surprises in Analyzer Code

Roslyn syntax node properties that are semantically always-present can be nullable in the netstandard2.0 API surface. The compiler enforces this, producing CS8631 or CS8600-series warnings. Always null-check these:

- `SimpleLambdaExpressionSyntax.ExpressionBody`: nullable even though lambdas used in LINQ always have expression bodies
- `MemberAccessExpressionSyntax.Name`: nullable in the type system
- `InvocationExpressionSyntax.Expression`: can technically be null
- `VariableDeclaratorSyntax.Initializer`: nullable when no initializer present
- `ArgumentSyntax.Expression`: nullable in the API

**Rule of thumb**: treat every Roslyn `SyntaxNode` property as potentially nullable unless the type is a non-nullable value type. Use pattern matching (`is { ExpressionBody: { } body }`) or explicit null guards.

## Gate on API Availability

A rule that names an API must check that the compilation can use the API.
Register the analyzer only when the API type exists.

```csharp
context.RegisterCompilationStartAction(start =>
{
    // The rule names an API from the Pug package. Without that type the
    // suggestion has no remedy, so the analyzer must not run at all.
    if (start.Compilation.GetTypeByMetadataName("Pug.Core.Text.StringBuilderPool") is null)
    {
        return;
    }
    // ...
});
```

Without the gate, every consumer gets a diagnostic that no code fix can
resolve. E128081, E128072, and E128102 follow this shape. Each one checks its
API before it registers, so an older target framework gets no report.

A rule can also need one member, not a whole type. E128072 resolves
`System.Security.Cryptography.SHA256` and then looks for a `HashData` member.
.NET 5 adds that member, so the rule stays silent on an older target.

## Register Both Object Creation Forms

`new Foo { ... }` reports as `ObjectCreationExpression`. The target-typed form
`Foo x = new() { ... }` reports as `ImplicitObjectCreationExpression`. Register
both, and take `BaseObjectCreationExpressionSyntax` in the handler.

```csharp
context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ImplicitObjectCreationExpression);
```

E128071 and E128091 follow this shape. A rule that registers one form misses
half of the object initializers in modern code.

Gate the remedy, not the API the rule names. E128028 maps each sync I/O call to
its async replacement. `File.ReadAllTextAsync` arrived after `File.ReadAllText`,
so a netstandard2.0 target has no remedy for the pair. The rule keeps a pair
only when the async member exists in the compilation.

```csharp
var owner = compilation.GetTypeByMetadataName(entry.Value);
if (owner is null
    || !SyncToAsyncMethodNames.TryGetValue(entry.Key, out var asyncName)
    || !owner.GetMembers(asyncName).OfType<IMethodSymbol>().Any())
{
    continue;
}
```

A missing key must skip the pair. An indexer throws inside the registration
action, and Roslyn reports that as AD0001 for every consumer.

Test the gate. Set `ReferenceAssemblies` to
`ReferenceAssemblies.NetStandard.NetStandard20` and expect no diagnostic.

## Match Symbols, Not Names

Report a construct only after the semantic model confirms the symbol. A check
on the spelling alone reports user code that shares the name.

- A method name: E128018 fired on any type with its own `ToList()`.
- A method name on a receiver: E128051 fired on a custom `GetAsync` client.
- A type name in a catch clause: E128051 treated a local `Exception` as broad.
- A parameter or variable name: E128056 matched a guarded value from another
  scope.

Resolve the symbol once at compilation start, then compare with
`SymbolEqualityComparer.Default`. E128090 resolves `System.Reflection.BindingFlags`
and `System.Type`. E128018 resolves `System.Linq.Enumerable`, which owns every
base-library `ToList` binding. E128051 and E128056 resolve their framework types and
compare the bound symbol.

Never compare `ToDisplayString()` with a literal. Never key a lookup on a
display string. A display format change silences the rule and no test fails.
E128068 and E128083 resolve their BCL types and compare `OriginalDefinition`
with `SymbolEqualityComparer.Default`.

One action on `SimpleMemberAccessExpression` covers both the enum member and the
reflection call shape. A bare type name, as in `typeof(BindingFlags)`, reaches no
member access, so E128090 also registers an `IdentifierName` action. That action
skips the receiver of a member access, which the first action already reports.
Prefer one narrow action over two broad actions.

Key a guarded value on a symbol that names storage. `GetSymbolInfo` on an
invocation returns the callee method, so two calls to the same helper compare
equal. E128056 returns no key for a call argument, and it unwraps a member of a
`FileSystemInfo` to the value that a read of the receiver itself produces. Any
other member keys on the (receiver, member) pair, so `config.Input` and
`config.Output` stay distinct.

A helper type used as a set key needs a pair-keyed comparer. A bare symbol key
reports `configA.Path` as guarded when only `configB.Path` is guarded. Add an
`IEqualityComparer<T>` that compares through `SymbolEqualityComparer.Default`.
Give an `Equals(object?)` override a `[NotNullWhen(true)]` parameter. Compute a
helper type's hash in the constructor and have `GetHashCode()` return the
stored value. A repo gate bans a `GetHashCode` body that reads a static
comparer.

### Exact Type or Derived Type

Decide which comparison the rule needs. A rule that names a forbidden type
requires an exact match. A rule that accepts a family requires a base-type walk.

- E128051 reports a broad catch only for `System.Exception` itself. A derived
  type is a narrower catch, so it does not report.
- E128051 accepts any type that derives from `OperationCanceledException` as a
  cancellation handler. `TaskCanceledException` counts.
- E128056 accepts `FileSystemInfo.Exists`, so a `DirectoryInfo` receiver counts.
- E128090 accepts any type that derives from `System.Type`, so a `TypeBuilder`
  receiver counts.
- `TypeSymbols.DerivesFrom` is the shared base-type walk. Call it instead of
  writing a local copy.

### Document Order

A directive, a state machine, and a switch arm all apply in source order. A set
comparison loses that order.

- E128055 walks the pragmas in one pass. A restore removes only a disable that
  comes first, so a file that restores early and disables later stays open.
- E128125 treats a local in an `async` member as moving. The state machine
  stores the local on the heap. Only a synchronous frame keeps the address
  stable.
- E128088 treats an unguarded `var` or discard pattern as a catch-all. A `when`
  clause filters the match, so a guarded pattern is not exhaustive.

## Keep the Package README Current

`src/E128.Analyzers/README.md` is the NuGet package page (`PackageReadmeFile`). It is the primary documentation surface for the package and is copied to nuget.org. After any analyzer change (new rule, renamed rule, changed category/severity, added/removed code fix), update the README's rule tables, "What each rule catches" sections, and code examples to match. A stale rule table makes the package look unmaintained.

## Test Infrastructure Notes

- `CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>` and `CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>` from `Microsoft.CodeAnalysis.CSharp.Testing` are the standard test harness. Most test files build a `test.RunAsync()` call directly, not the static `CSharpAnalyzerVerifier` helper.
- Test `ReferenceAssemblies` default to the SDK version configured in `tests/.globalconfig` via `e128_minimum_framework_version`
- xUnit v3 MTP runner requires `scripts/test.sh`. Raw `dotnet test --filter` does not work

## Related

- [Analyzers](../analyzers.md): main analyzer documentation
- [Code Fix Patterns](code-fix-patterns.md): code fix implementation patterns
- [Release Tracking](../release-tracking.md): Shipped/Unshipped workflow
