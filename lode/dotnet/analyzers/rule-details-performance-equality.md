# Performance Rule: Struct Key Equality
*Updated: 2026-11-25T17:04:34Z*

This file covers E128253, the struct-key equality rule. It reports a
`Dictionary<TKey, TValue>`, `HashSet<T>`, or
`ConcurrentDictionary<TKey, TValue>` created with the default equality comparer
where the key is a source struct without `IEquatable<T>`.

## Why It Matters

`EqualityComparer<T>.Default` uses `ObjectEqualityComparer<T>` when `T` does not
implement `IEquatable<T>`. Each hash and equality test then boxes the key. A
lookup allocates on the hot path even though the collection itself does not.

A struct that overrides `Equals(object)` and `GetHashCode()` still boxes, because
the comparer calls the object overloads.

## Detection Shape

The rule registers on `OperationKind.ObjectCreation` inside a compilation start
action. It resolves five types once:

- `System.Collections.Generic.Dictionary`2`
- `System.Collections.Generic.HashSet`1`
- `System.Collections.Concurrent.ConcurrentDictionary`2`
- `System.IEquatable`1`
- `System.Collections.Generic.IEqualityComparer`1`

The created type must match one of the three collection original definitions.
The key is `TypeArguments[0]`. The rule reports only when all four hold:

1. The key kind is `TypeKind.Struct`.
2. The key declares source syntax, so the maintainer can change it.
3. The key implements no `IEquatable<key>` interface.
4. No constructor argument implements `IEqualityComparer<key>`.

A missing comparer type in the compilation turns the rule off, so an old target
framework gets no report.

## What Stays Silent

- A key from metadata, such as `Guid`.
- An enum key, because the runtime compares it through the underlying value.
- A record struct, because the compiler generates `IEquatable<T>`.
- A class or type parameter key.
- A creation with an explicit `IEqualityComparer<T>` argument. The rule cannot
  see inside the comparer, so it trusts the caller.
- A collection expression such as `HashSet<Key> set = [a, b]`. It reaches no
  object creation.

## Trade-offs

The rule does not report `ToDictionary`, `ToHashSet`, `ToFrozenDictionary`, or
`ToFrozenSet`. Those calls take an optional comparer and build the collection
without an object creation. A later session can extend the rule when a consumer
site needs it.

The rule carries no code fix. The remedy adds `IEquatable<T>` to the key type,
which changes the type rather than the call site.

## Tests

- `StructKeyWithoutEquatableAnalyzerTests` covers each collection, the target
  typed form, the capacity and copy constructor, and each silent case.
- `StructKeyWithoutEquatableBranchCoverageTests` covers the interface-typed
  comparer, a generic key, `IEquatable` of another type, one report per
  creation, and an unresolved key.

## Related

- [rule-details-performance.md](rule-details-performance.md)
- [rule-details-performance-tier2.md](rule-details-performance-tier2.md)
- [Analyzer Candidates: Settled Catalog](../analyzer-candidates/catalog.md)
- [Open Register](../analyzer-candidates/index.md)
