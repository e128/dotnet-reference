# Unsafe Code Policy
*Updated: 2026-11-25T22:37:43Z*

Production code in this repository contains no unsafe code. No project sets
`AllowUnsafeBlocks`, so a new `unsafe` block or a pointer type fails the build.
Analyzer tests supply unsafe code only as source strings to the Roslyn test
harness. The analyzer reads those strings and the test assembly never compiles them.

The guidance source is the .NET unsafe best-practices document
(`learn.microsoft.com/dotnet/standard/unsafe-code/best-practices`). The E128
unsafe rules encode most of that document as diagnostics. The rules run on this
repository and on any project that references the package.

## Coverage Map

| Guidance in the document                                  | Rule             |
| --------------------------------------------------------- | ---------------- |
| Pin an object with `fixed` before taking a pointer        | E128125          |
| Do not reinterpret between `bool` and `int`               | E128115          |
| Use `Unsafe.BitCast` over `Unsafe.As`                     | E128112          |
| Use the unaligned `Unsafe` read and write calls           | E128113          |
| Do not remove a bounds check by hand                      | E128114          |
| Do not treat a non-blittable type as bytes                | E128118, E128139 |
| Use an inline array over a fixed-size buffer              | E128119          |
| Do not skip local initialization                          | E128120          |
| Do not create a null managed reference                    | E128127          |
| Keep a delegate alive while native code holds it          | E128128          |
| Do not bulk-copy a type that holds references             | E128129          |
| Never write a static readonly field through reflection    | E128254          |
| Bound `stackalloc` and keep it out of a loop              | E128109          |
| Zero a pooled buffer before `Return`, then never reuse it | E128110, E128111 |
| Prefer `LibraryImport` over `DllImport`                   | E128117          |
| Do not write to a string through a pointer                | E128126          |
| Pass a span, not a pointer plus a length                  | E128136          |

The rule set and the severity live in `src/E128.Analyzers/README.md`. See
[analyzers.md](analyzers.md) for the deny-by-default strategy.

## Reflection Write To A Static Readonly Field

E128254 reports a `FieldInfo.SetValue` or `SetValueDirect` call that writes a
static readonly field of a type outside the framework. The field resolves from a
`GetField` call on `typeof(T)` or `value.GetType()` with a constant name, or from
a local with one such initializer. A `Type` local resolves through its
declaration, and a null-conditional call is read the same way.

The rule reads the `BindingFlags` argument. A flag set must fold to a constant.
A literal flag set and a `const BindingFlags` field therefore work. A flag set
held in a runtime local stays silent, because the rule cannot prove which field
the call returns. A framework type stays silent, because E128137 owns the
nonpublic BCL lookup.

A `FieldInfo` local counts only when its one assignment runs before the write.

No code fix. Removing `readonly` changes the constant-fold contract, and the
runtime inlines the field into every reader.

## Gaps

The document lists areas that no E128 rule covers.

- `MemoryPool<T>` and `IMemoryOwner<T>`. E128110 and E128111 watch
  `ArrayPool<T>` only, so no rule requires a zero before an owner returns its
  buffer. E128162 covers a direct use after an explicit `Dispose()`. A retained
  slice used after the owner is disposed stays open. The pooled buffer rule in
  `.claude/rules/security.md` still applies by hand.
- A struct with padding that moves through a byte-wise API. No rule compares
  the field offsets with the byte span.
- A block move of two plain `void*` values. E128129 cannot see the type behind
  a `void*`.
- An assumed zero terminator. No rule flags this.
- Cross-thread access to a local variable. No rule flags this.
- Fuzz testing. The repository has no fuzz harness.

## Shipped Rule Corrections

The 2026-11-25 unsafe read found eight defects in shipped rules. One defect
crashed the analyzer process on `nint`. All eight shipped with tests on the
same day. The list and the changes live in
[unsafe-sweep.md](analyzer-candidates/unsafe-sweep.md).

The coverage map matches the shipped behavior for items 1, 2, 5, and 6.

## Invariants For New Code

- Write safe C#. Reach for `Span<T>`, `ReadOnlySpan<T>`, and
  `System.Runtime.CompilerServices.Unsafe` helpers only when a measurement
  shows a gain.
- Never add `AllowUnsafeBlocks` to a project file without user approval. The
  property opens the whole assembly, not one method.
- Never add an analyzer suppression to silence an unsafe rule. Fix the code.
- Read `lode/dotnet/performance.md` before you trade safety for speed. The
  change protocol there applies to every such trade.
