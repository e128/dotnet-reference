# Unsafe Code Policy
*Updated: 2026-10-31T13:32:41Z*

Production code in this repository contains no unsafe code. No project sets
`AllowUnsafeBlocks`, so a new `unsafe` block or a pointer type fails the build.
Every match for an unsafe construct sits inside an analyzer test fixture, as a
string. The analyzer reads the string and never compiles it.

The guidance source is the .NET unsafe best-practices document
(`learn.microsoft.com/dotnet/standard/unsafe-code/best-practices`). The E128
unsafe rules encode most of that document as diagnostics. The rules run on this
repository and on any project that references the package.

## Coverage Map

| Guidance in the document                       | Rule                                 |
| ---------------------------------------------- | ------------------------------------ |
| Do not let a pointer outlive its `fixed` block  | E128125                              |
| Do not reinterpret between `bool` and `int`     | E128115                              |
| Use `Unsafe.BitCast` over `Unsafe.As`           | E128112                              |
| Use the unaligned `Unsafe` read and write calls | E128113                              |
| Do not remove a bounds check by hand            | E128114                              |
| Do not treat a non-blittable type as bytes      | E128118                              |
| Use an inline array over a fixed-size buffer    | E128119                              |
| Do not skip local initialization                | E128120                              |
| Do not create a null managed reference          | E128127                              |
| Keep a delegate alive while native code holds it| E128128                              |
| Do not bulk-copy a type that holds references   | E128129                              |
| Bound `stackalloc` and keep it out of a loop    | E128109                              |
| Zero a pooled buffer before `Return`            | E128110, E128111                     |
| Prefer `LibraryImport` over `DllImport`         | E128117                              |
| Do not write to a string through a pointer      | E128126                              |

The rule set and the severity live in `E128.Analyzers/README.md`. See
[analyzers.md](analyzers.md) for the deny-by-default strategy.

## Gaps

The document lists four areas that no E128 rule covers as of 2026-10-31.

- `MemoryPool<T>` and `IMemoryOwner<T>`. E128110 and E128111 watch
  `ArrayPool<T>` only. The pooled buffer rule in `AGENTS.md` still applies by
  hand.
- A pointer plus a length, and an assumed zero terminator. No rule flags this.
  Use `Span<T>` and `ReadOnlySpan<T>` instead.
- Cross-thread access to a local variable. No rule flags this.
- Fuzz testing. The repository has no fuzz harness.

## Invariants For New Code

- Write safe C#. Reach for `Span<T>`, `ReadOnlySpan<T>`, and
  `System.Runtime.CompilerServices.Unsafe` helpers only when a measurement
  shows a gain.
- Never add `AllowUnsafeBlocks` to a project file without user approval. The
  property opens the whole assembly, not one method.
- Never add an analyzer suppression to silence an unsafe rule. Fix the code.
- Read `lode/dotnet/performance.md` before you trade safety for speed. The
  change protocol there applies to every such trade.
