# Smalltalk Best Practice Patterns (Applied to C#)
*Updated: 2026-11-01T20:56:22Z*

Key patterns from Kent Beck's Smalltalk Best Practice Patterns, adapted for C#/.NET:

## Naming

- **Intention Revealing Name**: name variables, methods, and classes after what they do, not how they do it.
- **Type Suggesting Name**: when the type is not obvious from context, include a hint in the name.
- **Role Suggesting Name**: name parameters after their role in the method, not their type.

## Methods

- **Composed Method**: divide a program into methods that each perform one identifiable task. Keep all operations in a method at the same level of abstraction.
- **Guard Clause**: handle special cases at the start of a method and return early.
- **Explaining Message**: send a message to `self` (call a method on `this`) to explain a complex expression.

## Collections

- **Collection Accessor**: provide methods to iterate over a collection. Do not expose the collection directly. Return `IReadOnlyList<T>` or `IReadOnlyCollection<T>`.
- **Enumeration Method**: use LINQ methods (`Where`, `Select`, `Any`) for collection queries. Do not write manual loops.

## State

- **Direct Variable Access**: access instance variables directly inside the class. Use properties for external access.
- **Getting Method and Setting Method**: provide getters freely and restrict setters. Prefer `init`-only or constructor-set properties.
