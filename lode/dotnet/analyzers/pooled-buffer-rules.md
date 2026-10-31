# Pooled Buffer and stackalloc Rules
*Updated: 2026-10-31T16:06:37Z*

Reliability analyzers guard buffer memory. All report at Warning. E128109 ships a
code fix. E128110 and E128111 ship no code fix.
See also [code-fix-patterns.md](code-fix-patterns.md) and the E128070 section in
[analyzers.md](../analyzers.md).

## E128109: stackalloc Safety

`StackAllocSafetyAnalyzer` flags a `stackalloc` expression in three cases:

- It sits inside a loop. The loop walk stops at a member, lambda, or local function.
- Its constant size is above 1024 bytes. Element size comes from a primitive table. An unknown type counts as 1 byte.
- Its length is not constant and has no guard. `Math.Min` counts as a guard. A conditional expression or an `if` statement counts only when its condition tests a name used in the length.
- It is assigned to a pointer. A `Span<T>` or `ReadOnlySpan<T>` target gives bounds checks.

Rationale: stack space is not reclaimed until the method returns.

`StackAllocSafetyCodeFixProvider` changes the pointer target to `Span<T>`. The fix
applies only when the pointer variable has one declarator, sits outside a loop,
and is used with an indexer alone. Taking `&buffer[i]` blocks the fix. Any other use
keeps the diagnostic and offers no fix.

```csharp
Span<byte> buffer = length <= 256 ? stackalloc byte[length] : new byte[length];
```

## E128110: Zero a Pooled Buffer Before Return

`PooledBufferZeroingAnalyzer` enforces the AGENTS.md section "Pooled Buffers (Required)".
It flags `ArrayPool<T>.Return(buffer)` unless the same `finally` block first calls
`CryptographicOperations.ZeroMemory` on the whole array.
The zero call must be a direct statement of the `finally` block. A call nested in an `if`, loop, or lambda does not count.

- The zero call may pass `buffer`, `buffer.AsSpan()`, or `MemoryMarshal.AsBytes(buffer)`.
- A slice such as `buffer.AsSpan(0, used)` does not count.
- `Return(buffer, clearArray: true)` does not count.
- `MemoryPool<T>` is not checked. Follow the AGENTS.md rule by hand there.

Invariant: this repo policy overrides the generic .NET unsafe-code advice to avoid `Return` in a `finally` block.

## E128111: Use After Return

The same analyzer reports E128111 when a statement after `Return` reads or writes the buffer.
The scan covers the statements that follow the `Return` statement in each enclosing block.
A reassignment of the buffer stops the scan. After `Return` the pool may hand the array to another caller.
Return from a `finally` block only when no other thread or pending async operation uses the buffer.

```csharp
try { /* use buffer */ }
finally
{
    CryptographicOperations.ZeroMemory(buffer);
    ArrayPool<byte>.Shared.Return(buffer);
}
```
