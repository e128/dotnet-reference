# Performance Rule Details: Tier 2
*Updated: 2026-11-07T17:31:31Z*

Detection shape for performance rules E128211 to E128217. The earlier
performance rules live in [rule-details-performance.md](rule-details-performance.md).

## Rules

### E128211: `Task.Delay` in a loop without a token

Flags `Task.Delay` inside a loop body when the resolved overload has no
`CancellationToken` parameter. The wait does not observe cancellation.

A call whose overload carries a `CancellationToken` stays silent. So does a loop
whose condition reads a `CancellationToken`, such as
`while (!token.IsCancellationRequested)`. `Thread.Sleep` is not this rule.

No code fix. The token source is a caller decision.

### E128212: `ToCharArray` used only to index or iterate

Flags `string.ToCharArray()` when the result is the direct `foreach` source or
the receiver of an element access. The copy serves no purpose.

An array assigned to a local or passed to a method stays silent.

No code fix. The span or direct-index shape is a caller choice.

### E128213: `Activator.CreateInstance` inside a loop

Flags `Activator.CreateInstance` inside a loop body. Reflection runs on every
iteration.

A `for` loop with a literal bound stays silent, because the total work is
bounded.

No code fix. The cached factory shape is a caller decision.

### E128214: HTTP handler without automatic decompression

Flags `new HttpClientHandler()` and `new SocketsHttpHandler()` when neither the
object initializer nor a later assignment on the same local sets
`AutomaticDecompression`. The default is `DecompressionMethods.None`.

A derived handler type stays silent because its constructor may set the property.
A field or property initializer stays silent, because a constructor can set the
property later.

No code fix. The decompression method set is a caller decision. The rule text
warns that decompression applies to every origin the handler reads.

### E128215: `StringBuilder` without capacity for a known append count

Flags `new StringBuilder()` with no arguments when the next statement is a loop
that calls `Append` or `AppendLine` on the builder and the loop count is known.

The count comes from the `foreach` source or a `Count` or `Length` bound in a
`for` condition. The source expression must be a chain of names, not a call. A
loop with a `break` that exits this loop stays silent. So does an append inside a
lambda or local function, and a capacity set in an object initializer. A
constructor with a capacity or an initial string stays silent.

The count query and the next-statement walk live in `LoopCount`, shared with
E128205.

No code fix. The capacity value is an estimate.

### E128216: Unbounded channel

Flags `Channel.CreateUnbounded<T>()` and
`Channel.CreateUnboundedPrioritized<T>()`. An unbounded channel grows without
limit when producers outrun consumers.

No code fix. The capacity and full-mode policy are caller decisions.

### E128217: Large struct passed by value

Flags a parameter of a source-declared struct larger than 24 bytes, passed by
value. The size sums instance fields, counts a reference or pointer field as 8
bytes, and handles fixed buffers, inline arrays, nullable structs, and enums.

The message reports a lower bound, because the estimate ignores padding. A
self-referential or deeply nested struct stops at a fixed depth instead of
recursing without end.

Exclusions:
- An `in`, `ref`, `out`, `params`, or `this` parameter.
- An override, an interface declaration, or an interface implementation, where
  the signature is fixed.
- A struct declared outside the current compilation.
- A SIMD or `System.Numerics` type.
- An explicit-layout struct, whose fields overlap.

No code fix. The `in` modifier or a class change is a caller decision.

## Related

- [rule-details-performance.md](rule-details-performance.md): performance rules
  E128203 to E128210
- [performance.md](performance.md): analyzer hot-path rules
