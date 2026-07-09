# Refactoring Catalogs

## Design Priority Order

When two code smells compete for attention, fix the higher-priority one first:

1. **Immutability** -- is mutable state justified? `set` vs `init`, `IReadOnlyList` vs `List`, mutable fields in async types
2. **Memory efficiency** -- unnecessary allocations, missing capacity hints, LINQ mid-chain `.ToList()`, LOH candidates
3. **CPU efficiency** -- algorithmic complexity first (`O(n^2)` -> `O(n)`); micro-optimizations only after measurement
4. **Parallelism** -- last resort; if a parallelism bug is found, check whether the root cause is mutable shared state (fix immutability first)

## Code Reduction Catalog

Shrink without changing unit test assertions. Flag these separately from structural refactorings -- they're low-risk batch candidates:

- Dead private methods/fields with no callers
- Single-use local variables that can be inlined (RCS1124)
- Catch blocks that only rethrow (remove the try/catch)
- `.Where(p).First()` -> `.First(p)`, `.OrderBy(...).First()` -> `.MinBy(...)`
- Single-line private methods called in exactly one place -- inline the body
- `if (x == null) return null; else return x.Foo` -> `x?.Foo`
- Interfaces with exactly one implementation and no test doubles
- Remove redundant local alias of parameter
- Inline single-use private method (called in exactly one place)
- Remove catch-rethrow (replace try/catch with direct call)
- Collapse LINQ chain (`.Where(p).First(p)`, `.OrderBy().First()` -> `.MinBy()`)
- Collapse null-check to null-conditional (`?.`, `??`, `??=`)

## Core Refactoring Catalog

- Extract Method / Extract Variable / Extract Interface / Extract Superclass
- Inline Method / Inline Variable / Collapse Hierarchy
- Change Function Declaration / Introduce Parameter Object / Encapsulate Variable
- Replace Conditional with Polymorphism / Replace Type Code with Subclasses
- Replace Inheritance with Delegation / Form Template Method
- Replace Constructor with Factory

## SOLID-Driven Refactoring Patterns

- SRP violation -> Extract Class, Move Method to separate responsibility
- OCP violation -> Replace Conditional with Polymorphism, introduce Strategy/interface + DI
- LSP violation -> Replace Inheritance with Delegation, Extract Interface
- ISP violation -> Split Interface into focused role interfaces
- DIP violation -> Extract Interface, Introduce Constructor Injection

## Design Patterns

Apply when appropriate: Strategy, Factory, Observer, Decorator, Adapter, Template Method, Chain of Responsibility, Composite
