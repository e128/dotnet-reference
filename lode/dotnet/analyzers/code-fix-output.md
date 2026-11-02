# Code Fix Output
*Updated: 2026-11-01T20:55:29Z*

Rules for the code a fix writes. A fix that emits a broken member costs more than
a missing fix: the apply path writes it into the document.

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

## Insert a Member Only Once

A fix that adds a member must look for a member with the same name and arity
first. A second copy emits CS0111, and the fixed code does not compile.

E128044 reports a type that implements `IAsyncDisposable` without `IDisposable`,
so the type can already declare `Dispose`. E128054 reports a class that can
already declare `InitializeAsync` or `DisposeAsync`. Both fixes add the interface
only and skip a member that exists.

The analyzer gate is not a substitute: it checks the interface list, not the
member list.

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
- [Code Fix Trivia](code-fix-trivia.md): absent trivia, literals, member removal
- [Code Fix Techniques](code-fix-techniques.md): DocumentEditor, speculative
  bind, generator
