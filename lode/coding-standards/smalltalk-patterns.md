# Smalltalk Best Practice Patterns (Applied to C#)
*Updated: 2026-04-09T00:52:00Z*

Key patterns from Kent Beck's Smalltalk Best Practice Patterns, adapted for C#/.NET:

## Naming

- **Intention Revealing Name** — name variables, methods, and classes after what they do, not how they do it.
- **Type Suggesting Name** — when a variable's type isn't obvious from context, include a hint in the name.
- **Role Suggesting Name** — name parameters after their role in the method, not their type.

## Methods

- **Composed Method** — divide your program into methods that perform one identifiable task. Keep all operations at the same level of abstraction.
- **Guard Clause** — handle special cases at the beginning of a method and return early.
- **Explaining Message** — send a message to `self` (call a method on `this`) to explain a complex expression.

## Collections

- **Collection Accessor** — provide methods to iterate over collections rather than exposing the collection directly. Return `IReadOnlyList<T>` or `IReadOnlyCollection<T>`.
- **Enumeration Method** — use LINQ methods (`Where`, `Select`, `Any`) rather than manual loops for collection queries.

## State

- **Direct Variable Access** — access instance variables directly within the class. Use properties for external access.
- **Getting Method / Setting Method** — provide getters freely; restrict setters. Prefer `init`-only or constructor-set properties.
