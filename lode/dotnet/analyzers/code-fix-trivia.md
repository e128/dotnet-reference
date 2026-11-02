# Code Fix Trivia
*Updated: 2026-11-01T20:55:29Z*

How a code fix keeps the text around the node it rewrites. See
[Code Fix Patterns](code-fix-patterns.md) for the rewrite shapes.

## Copy Only the Indentation

A method leading trivia holds the doc comment as well as the indentation.
Copying the whole list onto a new attribute list duplicates the comment.

Copy the last whitespace trivia instead. E128073 follows this rule.

```csharp
var indentation = leadingTrivia.Last().IsKind(SyntaxKind.WhitespaceTrivia)
    ? SyntaxFactory.TriviaList(leadingTrivia.Last())
    : default;
```

## A Comment Inside a Directive

A structured directive holds its own trailing comment. A split of one pragma
into two drops that comment unless the last line keeps it.

Copy the tail of the last ID token onto the last split directive. E128065
does this. Leading trivia on the end-of-directive token does not render.

## Trivia That Is Absent

A leading-trivia list may hold no whitespace. `indentation.Last()` throws on an
empty list. Branch on the count and emit the line break alone. E128051 follows
this rule.

## An Unquoted Replacement Literal

`SyntaxFactory.Literal(value)` takes token text, not a value. It emits an
unquoted literal. Pass `SymbolDisplay.FormatLiteral(value, quote: true)` as the
text, and the value as the second argument. E128013 follows this rule.

## Text in an Interpolated String

An `InterpolatedStringTextToken` carries two texts. `Text` is source text, so
escape it. `ValueText` is the runtime value, so keep it raw. Set both to the
escaped text and a tree comparison against parsed source fails. Set both raw
and a quote or a line break breaks the emitted literal.

## Insert Above the First Member

A new field goes under the open brace. Give the field the indentation of the
first member only. Prepend two line feeds to the existing leading trivia of
that member. The doc comment then stays attached to the member it documents.
E128102 and E128029 follow this rule.

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

## Related

- [Code Fix Patterns](code-fix-patterns.md)
- [New Analyzer Checklist](new-analyzer-checklist.md)
