# Code Fix Patterns
*Updated: 2026-11-03T03:14:50Z*

Implementation patterns and gotchas for Roslyn code fix providers in E128.Analyzers.
The emitted text rules live in [code-fix-output.md](code-fix-output.md). The
advanced edit APIs live in [code-fix-techniques.md](code-fix-techniques.md).

## Decide Before You Write the Fix

Answer these six questions before the first line of a fix provider. Each
answer prevents a defect that the test suite will not catch on its own.

1. Does the rewritten text parse? Emit a value through its formatter and a
   type through `SymbolDisplayFormat`. See [code-fix-output.md](code-fix-output.md).
2. Does the walk cross a lambda or a local function? Stop at the boundary.
   See § Stop at a Nested Function.
3. Does the fix read a table the analyzer owns? Read the same gated table.
   See § One Table for Analyzer and Fix.
4. Does the fix insert a member? Return null from `GetFixAllProvider()`, then
   check that no member shares the name and arity. See
   [code-fix-techniques.md](code-fix-techniques.md) § Fix-All Providers and
   [code-fix-output.md](code-fix-output.md) § Insert a Member Only Once.
5. Does the fix remove a member? Trim the trivia, never clear it. See
   [code-fix-output.md](code-fix-output.md) § Removing a Member Node.
6. Does the remedy name an API that the caller can already reach? When the
   only correct repair is a different call, the library must expose that call.
   See § Code-Aware Library.

## Code-Aware Library

A library that ships an analyzer with it controls the remedy. The analyzer
points at the canonical entry point, and the fix never invents a call.

The `System.Collections.Immutable` guide shows the reason. `new ImmutableArray<int>()`
produces a struct with a null backing array, so `.Length` throws at run time.
The fix replaces the construction expression with a name the library owns:

```csharp
var generator = SyntaxGenerator.GetGenerator(document);
var memberAccess = generator.MemberAccessExpression(objectCreation.Type, "Empty");
var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
return document.WithSyntaxRoot(root.ReplaceNode(objectCreation, memberAccess));
```

Apply this rule this way. Gate the analyzer on the API, as
[new-analyzer-checklist.md](new-analyzer-checklist.md) § Gate on API
Availability requires. When no such API exists, report without a fix.

## Add a Using Directive

Use `UsingDirectives.AddIfMissing(root, namespaceName)` from
`src/E128.Analyzers/UsingDirectives.cs`. Do not write a local helper.

The helper skips the namespace when an exact-name using is already present.
Otherwise it inserts the directive after the last directive that sorts before it,
so an already-sorted block stays sorted. A file header is leading trivia of the
first directive, so the helper moves that trivia to the new directive when the
new one sorts first.

```csharp
var newRoot = UsingDirectives.AddIfMissing(replaced, "System.Collections.Generic");
return document.WithSyntaxRoot(newRoot);
```

The elastic line feed adds a blank line before the first declaration when the
new directive lands at the end of the block. A `FixedCode` string must match
the real output. Run the code fix test before you finalize the expected text.

The helper handles a top-level using on the compilation unit only. A file with
a namespace-scoped using list needs a different path.

## Span Widening

A diagnostic often lands on a narrower node than the edit replaces. Report on
the token that names the defect, then widen the edit target before you replace.

```csharp
if (root.FindNode(context.Span, getInnermostNodeForTie: true)
        .FirstAncestorOrSelf<ObjectCreationExpressionSyntax>() is not { } creation)
{
    return;
}
```

`root.FindNode(span, getInnermostNodeForTie: true)` is the repo idiom. A token
walk with `FindToken(span.Start)` reaches the same node and costs more.

## A Fix Without a Rewritable Receiver

`InvocationExpressionSyntax.Expression` is the only node the fix can rewrite. A
call that avoids a member access on the receiver, such as a fully qualified
static call, still yields one. A `using static` import cannot call an extension
method unqualified in C# at all, so a guard for that shape needs no test. The
harness cannot compile such source and a diagnostic that fires there would
offer nothing to rewrite.

Emit a type through `ToMinimalDisplayString(semanticModel, position)`. It keeps
the type arguments, qualifies a name only when the name is out of scope, and
escapes a keyword identifier. `ITypeSymbol.Name` drops the arguments and the
qualification. E128032 wrote `AddSingleton<IRepository, Repository>()` for a
generic type, which is CS0305.

```csharp
var typeName = SyntaxFactory.ParseTypeName(
    targetInterface.ToMinimalDisplayString(semanticModel, invocation.SpanStart));
```

Use the same call for the code action title, so the preview shows the text that
lands. E128031 and E128032 both follow this shape.

## Stop at a Nested Function

A walk over `MethodDeclarationSyntax.DescendantNodes()` reaches into lambdas and
local functions. Neither can take an `await` unless it becomes `async` itself.

E128028 treated both as part of the method body. An `await` inside a lambda
suppressed a real report. The fix wrote `await` inside a lambda that stayed sync,
which is CS4033.

Stop the walk at `AnonymousFunctionExpressionSyntax` and
`LocalFunctionStatementSyntax`. Declare the helper once on the analyzer as
`internal static` and call it from the fix provider, so both apply one boundary.
`TaskFromResultSyncIoAnalyzer.IsInNestedFunction` is that helper.

Apply the boundary to the node that is reported, not only to the scans. A rule
that scans the method body but reports every candidate node still names a node
that the fix refuses to touch.

## One Table for Analyzer and Fix

A lookup table that the analyzer reports from must be the same table the fix
reads. A private copy in the fix provider drifts, and the drift is silent.

E128028 held the sync I/O names twice. The analyzer table listed `File.Copy`,
`File.Move`, and `File.ReadLines`. The fix table did not. The fix still added
`async` and unwrapped `Task.FromResult`, so it wrote an `async` method with no
`await` and left the blocking call in place. The compiler then reported CS1998.

Declare the table `internal static readonly` on the analyzer and let the fix
provider read it. Report only what the fix can repair. E128028 omits a
method unless an awaitable overload exists.

Gate the table once. E128028 builds the pair list at compilation start and the
fix reads that gated list, so both sides skip a pair with no async member. An
ungated fix rewrote `File.ReadAllText` on a netstandard2.0 target.

Add a parity test when a rule needs two tables. Assert that both key sets match.

Pass a computed value through `diagnostic.Properties` instead of copying the
table. E128023 puts the detected path literal in the analyzer and hands the
relative suffix to the fix. The path prefixes live in one file.

## Decline at Registration

A fix that returns the document unchanged offers a dead action. The harness
fails that case with "a fix was offered that made no changes".

Do the whole shape check at registration, in the override the provider
defines (`RegisterCodeFixesAsync`, or `RegisterFixesAsync(context, root)`
from `E128CodeFixProviderBase`). Build the replacement node there and hand
it to the action delegate. The apply path then never discovers a reason to
stop.

```csharp
var interpolated = BuildFromInvocation(invocation, semanticModel, cancellationToken);
if (interpolated is null)
{
    return; // no code action, so no dead fix
}
```

The rule also covers a rewrite that needs statements outside the reported node.
`StringConcatInLoopCodeFixProvider` builds the whole method body first. It
declares a builder before the loop and reads it back after the loop. The build
step returns null when the local is read inside the loop or is not empty at the
start. A null build registers no action.

## Tree Identity

The semantic model binds to one tree. A node from another tree throws "Syntax
node is not within syntax tree" on `GetSymbolInfo`.

Annotate every target in one `ReplaceNodes` call over the original tree. Never
annotate the declaration first, then pass the original reference nodes to a
later call. E128017 did that, and the reference pass matched nothing.

## Keep the Receiver

Rewrite the member name, not the whole member access. `Encoding.Default` may
carry a qualified receiver such as `System.Text.Encoding`. E128006 swaps
`Name` only.

## Related

- [Code Fix Output](code-fix-output.md): emitted text, trivia, rewrite shapes
- [Code Fix Techniques](code-fix-techniques.md): DocumentEditor, speculative bind, generator
- [New Analyzer Checklist](new-analyzer-checklist.md)
- [Analyzers](../analyzers.md): main analyzer documentation
