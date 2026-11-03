# Code Fix Output
*Updated: 2026-11-03T03:14:50Z*

Rules for the text a fix writes and the text around it. A fix that emits a
broken member costs more than a missing fix: the apply path writes it into the
document. The rewrite shapes live in
[code-fix-patterns.md](code-fix-patterns.md).

## Emit Valid Source Text

A syntax token that the analyzer captured carries the unescaped value. Never
put that value between hand-written quotes.

```csharp
// Wrong: the value behind "a\"b" is a"b, so the emitted field does not parse.
sb.Append('"').Append(literal).Append('"');

// Right
sb.Append(SymbolDisplay.FormatLiteral(literal, quote: true));
```

`SyntaxFactory.Literal(value)` takes token text, not a value. It emits an
unquoted literal. Pass `SymbolDisplay.FormatLiteral(value, quote: true)` as the
text, and the value as the second argument. E128013 follows this rule.

`SyntaxFactory.ParseMemberDeclaration` returns null when the text is malformed.
A fallback branch that emits a stub member hides the defect and writes broken
code into the document. Use a formatter for each value and the parse holds.

Abort the fix when the parse returns null. A stub drops the attributes and the
fields, so the result is not a usable member. `FixedBufferFieldCodeFixProvider`
parses the InlineArray struct before it registers and offers no fix on a null.
`MultiStringEqualsOrChainCodeFixProvider` returns null from its build step and
leaves the document unchanged. It validates the whole member before it
registers, so every offered fix changes code.

## Text in an Interpolated String

An `InterpolatedStringTextToken` carries two texts. `Text` is source text, so
escape it. `ValueText` is the runtime value, so keep it raw. Set both to the
escaped text and a tree comparison against parsed source fails. Set both raw
and a quote or a line break breaks the emitted literal.

## Place a New Node with the Formatter

Attach `Formatter.Annotation` to every node a fix inserts or reshapes. The
formatter indents the node and keeps the space between tokens.

A member built with a hand-written line feed lands at column 0. The annotation
places it on the indent of the surrounding members. E128044 and E128054 follow
this rule.

```csharp
var member = SyntaxFactory.MethodDeclaration(returnType, "Dispose")
    .WithBody(SyntaxFactory.Block())
    .WithAdditionalAnnotations(Formatter.Annotation);
```

A new base list needs one extra step. The identifier of a type without a base
list carries the line break before the brace, so the colon lands on the next
line. Replace that trailing trivia with a space before the formatter runs.

```csharp
// Without a base list, the line break before the brace is trailing trivia of the
// identifier. Move it after the new base list.
var newClassDecl = classDecl
    .WithIdentifier(classDecl.Identifier.WithTrailingTrivia(SyntaxFactory.Space))
    .WithBaseList(newBaseList.WithTrailingTrivia(classDecl.Identifier.TrailingTrivia))
    .WithMembers(newMembers);
```

Annotate the nodes you insert, not the type declaration. An annotation on the
type reformats every member that the caller already wrote. E128054 inserts an
`IAsyncLifetime` base list and two methods this way.

Keep manual trivia when a token replaces an absent token, and when the test pins
a blank line that the formatter would collapse.

## Copy Only the Indentation

A method leading trivia holds the doc comment as well as the indentation.
Copying the whole list onto a new attribute list duplicates the comment.

Copy the last whitespace trivia instead. E128073 follows this rule.

```csharp
var indentation = leadingTrivia.Last().IsKind(SyntaxKind.WhitespaceTrivia)
    ? SyntaxFactory.TriviaList(leadingTrivia.Last())
    : default;
```

## Trim Trailing Whitespace When Appending

A fix that appends to a line can leave trailing whitespace behind. Reverse the
trailing trivia, skip the whitespace, then reverse back:

```csharp
private static T WithoutTrailingSpaces<T>(T node)
    where T : SyntaxNode
{
    var trailing = node.GetTrailingTrivia().Reverse()
        .SkipWhile(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia))
        .Reverse();

    return node.WithTrailingTrivia(trailing);
}
```

Reach this helper only for an appended line. A node the fix inserts takes the
formatter rule instead. See § Place a New Node with the Formatter.

## Trivia That Is Absent

A leading-trivia list may hold no whitespace. `indentation.Last()` throws on an
empty list. Branch on the count and emit the line break alone. E128051 follows
this rule.

## A Comment Inside a Directive

A structured directive holds its own trailing comment. A split of one pragma
into two drops that comment unless the last line keeps it.

Copy the tail of the last ID token onto the last split directive. E128065
does this. Leading trivia on the end-of-directive token does not render.

## Insert Above the First Member

A new field goes under the open brace. Give the field the indentation of the
first member only. Prepend two line feeds to the existing leading trivia of
that member. The doc comment then stays attached to the member it documents.
E128102 and E128029 follow this rule.

## Insert a Member Only Once

A fix that adds a member must look for a member with the same name and arity
first. A second copy emits CS0111, and the fixed code does not compile.

E128044 reports a type that implements `IAsyncDisposable` without `IDisposable`,
so the type can already declare `Dispose`. E128054 reports a class that can
already declare `InitializeAsync` or `DisposeAsync`. Both fixes add the interface
only and skip a member that exists.

The analyzer gate is not a substitute: it checks the interface list, not the
member list.

## Removing a Member Node

`RemoveNode(node, SyntaxRemoveOptions.KeepNoTrivia)` drops the trivia of the
removed node. The line break that ended the previous token stays. That result
is correct for the common case.

A blank line between the removed member and the closing brace survives the
removal. Trim that line only when the leading trivia of the closing brace holds
whitespace and line breaks alone. Keep the whitespace, and drop the line
breaks. Never clear the whole trivia list.

Clearing the whole list moves the closing brace to column 0 when the type is
nested. It also joins a trailing comment and the brace onto one line, which
comments the brace out.

`LowValueTestCodeFixProvider` follows this rule.

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
- **Constraint type**: a sealed class cannot be a generic constraint. Sealing
  the named type is CS0701 at the `where T : X` clause. E128005 skips a class
  that any type parameter, method, or local function names as a constraint. The
  guard covers the same compilation only.
- **Existing member**: a member that the fix inserts must not share a name and
  an arity with a member that is already declared. See § Insert a Member Only
  Once.
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
- **Inserted statement**: the new statement takes the leading trivia of the
  target statement and an elastic trailing line feed. Without the line feed both
  statements land on one line. E128110 follows this rule.
- **Policy value**: a fix that needs a threshold reads an `e128_*` option and
  falls back to a constant. E128070 follows this rule.

## Related

- [Code Fix Patterns](code-fix-patterns.md): decision list, using directives,
  fix-all strategy, registration
- [Code Fix Techniques](code-fix-techniques.md): DocumentEditor, speculative
  bind, generator
