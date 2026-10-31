# Code Fix Patterns
*Updated: 2026-10-31T18:31:44Z*

Implementation patterns and gotchas for Roslyn code fix providers in E128.Analyzers.

## Decide Before You Write the Fix

Answer these five questions before the first line of a fix provider. Each
answer prevents a defect that the test suite will not catch on its own.

1. Does the rewritten text parse? Emit a value through its formatter and a
   type through `SymbolDisplayFormat`. See § Emit Valid Source Text.
2. Does the walk cross a lambda or a local function? Stop at the boundary.
   See § Stop at a Nested Function.
3. Does the fix read a table the analyzer owns? Read the same gated table.
   See § One Table for Analyzer and Fix.
4. Does the fix insert a member? Return null from `GetFixAllProvider()`. See
   § FixAll That Inserts a Member.
5. Does the fix remove a member? Trim the trivia, never clear it. See
   § Removing a Member Node.

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

Some providers still insert a using inline through
`CompilationUnitSyntax.AddUsings()`. Move one to the helper the next time you
touch it.

The helper handles a top-level using on the compilation unit only. A file with
a namespace-scoped using list needs a different path.

## BatchFixer vs SequentialRenameFixAllProvider

- **`WellKnownFixAllProviders.BatchFixer`**: standard choice for most code fixes. Computes all fixes from the original snapshot and merges. Works well when fixes are independent local edits (expression replacements, type swaps).
- **`SequentialRenameFixAllProvider`**: required when the fix uses `Renamer.RenameSymbolAsync`. BatchFixer fails when multiple renames touch the same document because the rename API modifies the solution globally. See `analyzers.md § SequentialRenameFixAllProvider`. E128059 and E128063 use it.

## Emit Valid Source Text

A syntax token that the analyzer captured carries the unescaped value. Never
put that value between hand-written quotes.

```csharp
// Wrong: the value behind "a\"b" is a"b, so the emitted field does not parse.
sb.Append('"').Append(literal).Append('"');

// Right
sb.Append(SymbolDisplay.FormatLiteral(literal, quote: true));
```

`SyntaxFactory.ParseMemberDeclaration` returns null when the text is malformed.
A fallback branch that emits a stub member hides the defect and writes broken
code into the document. Use a formatter for each value and the parse holds.

Abort the fix when the parse returns null. A stub drops the attributes and the
fields, so the result is not a usable member. `FixedBufferFieldCodeFixProvider`
parses the InlineArray struct before it registers and offers no fix on a null.
`MultiStringEqualsOrChainCodeFixProvider` returns null from its build step and
leaves the document unchanged. It validates the whole member before it
registers, so every offered fix changes code.

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

## FixAll That Inserts a Member

`BatchFixer` computes every fix from the same snapshot and merges the results.
Two fixes in one type that insert the same member name each pass their own
conflict check on the original tree. The merge then emits the member twice.

Return `null` from `GetFixAllProvider()` for a fix that inserts a field or a
property. Keep that fix single-diagnostic. E128029 follows this rule.

## One Table for Analyzer and Fix

A lookup table that the analyzer reports from must be the same table the fix
reads. A private copy in the fix provider drifts, and the drift is silent.

E128028 held the sync I/O names twice. The analyzer table listed `File.Copy`,
`File.Move`, and `File.ReadLines`. The fix table did not. The fix still added
`async` and unwrapped `Task.FromResult`, so it wrote an `async` method with no
`await` and left the blocking call in place. The compiler then reported CS1998.

Declare the table `internal static readonly` on the analyzer and let the fix
provider read it. Report only what the fix can repair. E128028 now omits a
method unless an awaitable overload exists.

Gate the table once. E128028 builds the pair list at compilation start and the
fix reads that gated list, so both sides skip a pair with no async member. An
ungated fix rewrote `File.ReadAllText` on a netstandard2.0 target.

Add a parity test when a rule needs two tables. Assert that both key sets match.

Pass a computed value through `diagnostic.Properties` instead of copying the
table. E128023 puts the detected path literal in the analyzer and hands the
relative suffix to the fix. The path prefixes live in one file.

## Guard the Rewrite Shape

A fix must produce code that compiles. Test the shape that breaks it, not only
the common shape.

- **Return type**: skip a method with `override` or an explicit interface
  implementation. The base member or the interface owns the return type. E128007
  rewrote both and produced CS0508 or CS0738.
- **Wrap in try/catch**: a catch body that returns `default` fits a return
  statement. Any other statement in a value-returning method loses the scope of
  its local. A void method takes an empty catch block instead. E128056 and
  E128057 follow this rule.
- **Append an argument**: find an overload with one more parameter that ends
  with the required type. The Playwright calls accept only an options object, so
  E128038 offers no fix there.
- **Await placement**: an await needs an async member. Refuse a lambda, a local
  function, and an accessor. E128008 follows this rule.
- **Value position**: a rewrite whose result value is new fits statement
  position only. E128087 offers the `Interlocked` fix for a whole statement.
- **Hoist a declaration**: a rewrite that needs a local declares it before the
  statement that reads it. E128086 declares the parameter for every statement
  form.
- **Member initializer**: a nested collection initializer fits an object
  initializer only. E128091 leaves the statement form alone.
- **Constant context**: a pattern or an attribute argument needs a constant.
  E128002 skips both.
- **Forwarded argument**: an argument takes its type from the old target. A
  collection expression forwarded to a new call site has no target, so
  `[1, 2, 3].ToFrozenSet()` is CS9176. E128026 declines that argument.
- **Factory argument**: a two-type-argument registration takes no factory. The
  fix drops the argument list and declines a factory that passes an argument.
  The container cannot rebuild that type. E128031 and E128032 follow this rule.
- **Type-only change**: an initializer keeps its old value. `int TimeoutMs = 5000`
  becomes `TimeSpan TimeoutMs = 5000`, which is CS0029. E128050 declines a
  property with an initializer and a parameter with a default.
- **Null annotation**: a type replacement keeps the `?` of the old type. Copy
  the annotation across, or a working null return gains a warning. E128052
  follows this rule.
- **Static field order**: a new static field goes after the static fields that
  its initializer reads. Static initializers run in textual order. E128124
  inserts after the last static field.
- **Bare pragma**: a bare `#pragma warning disable` covers every warning. Hold it
  under a `"*"` sentinel, and emit a bare restore for that key. E128055 follows
  this rule.

## Decline at Registration

A fix that returns the document unchanged offers the user a dead action. The
harness fails that case with "a fix was offered that made no changes".

Do the whole shape check in `RegisterCodeFixesAsync`. Build the replacement
node there and hand it to the action delegate. The apply path then never
discovers a reason to stop.

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

- [Code Fix Trivia](code-fix-trivia.md): absent trivia, literals, member removal
- [New Analyzer Checklist](new-analyzer-checklist.md)
- [Analyzers](../analyzers.md): main analyzer documentation
