# Unsafe Best-Practices Sweep
*Updated: 2026-11-25T17:55:52Z*

This sweep reads the .NET unsafe-code best-practices document
(`learn.microsoft.com/dotnet/standard/unsafe-code/best-practices`) against the
repository on 2026-11-25. It covers two questions. Does the shipped code break
the document? Does each E128 unsafe rule detect what the document asks for?

## Code Audit Result

The reference code contains no unsafe construct. `rg` finds no `unsafe` block,
pointer type, `stackalloc`, `ArrayPool`, `MemoryPool`, `MemoryMarshal`,
`GCHandle`, `Marshal` call, or `DllImport` in `src/E128.Reference.Core`,
`src/E128.Reference.Web`, `src/E128.Reference.Cli`, or `benchmarks/`. No project
sets `AllowUnsafeBlocks`. The unsafe rules therefore never fire on this
repository. The audit moved to the analyzers, which are the shipped product.

## Correctness Defects

### A stack overflow on `nint` and `nuint`

`Blittability.IsBlittable` walks each instance field and calls itself on the
field type. `System.IntPtr` holds one field, `private readonly nint _value`.
The `nint` type is `System.IntPtr`, so the call recurses until the process
ends. `System.UIntPtr` has the same shape with `nuint`.

This path runs for `MemoryMarshal.Read<nint>`, `MemoryMarshal.Cast<byte, nint>`,
`Unsafe.ReadUnaligned<nint>`, and `Unsafe.WriteUnaligned<nint>`. A stack
overflow is not catchable, so AD0001 does not cover it. The compiler or IDE
process ends.

The root cause sits in [Blittability.cs](../../../src/E128.Analyzers/Unsafe/Blittability.cs),
in the struct branch. No test passes `nint` or `IntPtr` to E128118 or E128139.

### `Unsafe.As` to a reference type is silent

Guidance item 5 says not to cast a struct to a class or a class to a struct.
`Unsafe.As<int, object>(ref value)` turns a four byte integer into a GC
reference. `UnsafeAsToBitCastAnalyzer` returns early when either type lacks a
fixed size, so this case never reports. The test
`Silent_WhenTargetIsAReferenceType` encodes that silence.

The rule message still says `Unsafe.BitCast` "rejects a reference type", which
reads as a reason to report. The gap is a policy choice that the document
rejects.

### A `stackalloc` guard that only mentions the length

`StackAllocSafetyAnalyzer.IsBounded` accepts any enclosing `if` or conditional
whose condition names the length identifier. These forms pass:

```csharp
if (length > 0) { Span<byte> b = stackalloc byte[length]; }
if (length != 0) { Span<byte> b = stackalloc byte[length]; }
Span<byte> b = stackalloc byte[Math.Min(length, 1_000_000)];
```

None of the three caps the length at 1024 bytes. The first two are normal
argument validation. The guard check reads syntax, not the bound.

### The element size under-counts

`ElementSize` returns 1 byte for every type the table does not name. The table
holds the numeric primitives and `char`. These forms pass the 1024 byte cap:

```csharp
Span<Guid> a = stackalloc Guid[300];      // 4800 bytes, counted as 300
Span<BigStruct> b = stackalloc BigStruct[100]; // counted as 100
Span<nint> c = stackalloc nint[300];      // 2400 bytes, counted as 300
Span<MyEnum> d = stackalloc MyEnum[400];  // counted as 400
```

### `Unsafe.AsPointer` escape

The rule name is `UnsafeAsPointerEscapeAnalyzer`. The check covers a `fixed`
scope, a stack local, and an async member. It does not check whether the
pointer outlives the method. These forms are silent:

```csharp
int local = 1;
return Unsafe.AsPointer(ref local);

_field = Unsafe.AsPointer(ref _buffer[0]);
```

Guidance item 1.2 requires the result not to outlive its target.

### The delegate lifetime check treats any call as a root

`FunctionPointerDelegateLifetimeAnalyzer.IsRooted` returns true when the
delegate is passed to any other call in the member. `Console.WriteLine(cb)`
therefore counts as a root, and the real hazard goes unreported.

### The non-public BCL check misses the common receiver

The receiver must be `typeof(FrameworkType)`. These forms are silent:

```csharp
type.GetMethod("M", BindingFlags.NonPublic | BindingFlags.Instance)
obj.GetType().GetField("_f", BindingFlags.NonPublic | BindingFlags.Instance)
```

The rule also never examines `FieldInfo.SetValue` on a `static readonly` field.
Guidance item 3.6 names that case.

## Anti-Patterns

- The unsafe rules default to silence when the shape is hard. E128112 skips an
  unknown size, E128109 skips an unnamed element type, and E128137 skips a
  receiver that is not a `typeof` expression. A missed report is invisible, so
  the gap survives every review.
- The rules match simple names where a symbol comparison is possible.
  `IsMathMinCall` compares the type name `Math`. E128114 compares the type name
  `MemoryMarshal`. A user type with either name changes the result.
- E128136 reports every pointer parameter of a method that also takes an
  integer. A `[DllImport]` method cannot take a span. A `Copy(int* src, int*
  dst, int count)` reports twice. Guidance item 16 concerns the public buffer
  API, not every interop signature.
- The code audit cannot exercise the unsafe rules. The tests pass unsafe source
  as strings, and no test project sets `AllowUnsafeBlocks`. A rule can ship
  with a defect such as the `nint` recursion and stay green.
- The coverage table in [unsafe-code.md](../unsafe-code.md) lists a guidance
  item as covered when the rule only covers part of it. Items 1, 2, 5, and 6
  need that distinction.

## Shipped Fixes

All eight fixes shipped on 2026-11-25 with a test for each. The tests are
`Silent_WhenTheTypeIsNint` in the E128118 and E128139 test classes, the new
cases in the E128109, E128112, E128125, E128128, E128129, and E128137 test
classes, and `NoFix_WhenTheTargetIsAReferenceType` for the E128112 fix.

| Rule       | Change                                                                    |
| ---------- | ------------------------------------------------------------------------- |
| E128118    | `Blittability` stops the `IntPtr` and `UIntPtr` recursion                 |
| E128112    | Reports a reference type source or target                                 |
| E128109    | Reads the guard bound, not the identifier presence                        |
| E128109    | Sizes an enum, a pointer, and `nint` from its real width                  |
| E128125    | Reports a pointer that returns or stores into a field                     |
| E128128    | Roots the delegate only through a store, a keep-alive, or a pin           |
| E128137    | Accepts a `GetType()` receiver next to `typeof`                           |
| E128129    | Covers `Unsafe.Write`, `WriteUnaligned`, and the block moves              |

E128129 cannot infer the type behind a plain `void*` argument, so a block move
of two `void*` values stays out of reach.

## Candidates

Two shapes remain open.

| Pattern                                                   | Score | Status |
| --------------------------------------------------------- | ----- | ------ |
| `MemoryPool<T>` or `IMemoryOwner<T>` is used after dispose | 4/5   | new    |
| A padded struct is copied byte-wise through a marshal API  | 3/5   | new    |

The `Unsafe.As` reference shape shipped inside E128112. The `fixed` pointer
escape shape shipped inside E128125. The write barrier shape shipped inside
E128129. Their rows left the register.

The `MemoryPool<T>` row closes the documented gap. The padded struct row is a
Tier 2 shape that waits for a real defect.

## Declined

- Cross-thread access to a local. The compiler cannot see the thread boundary,
  and the shape is rare. The gap stays documented.
- A missing `GC.KeepAlive`. The object lifetime is not visible at the call
  site. The shape needs dataflow across a native call, which Roslyn does not
  model.

## Related

- [index.md](index.md): the open register
- [catalog.md](catalog.md): the shipped rules
- [skipped-decisions.md](skipped-decisions.md): every declined shape
- [../unsafe-code.md](../unsafe-code.md): the policy and the coverage map
