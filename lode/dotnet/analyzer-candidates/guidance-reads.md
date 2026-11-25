# Guidance Document Reads
*Updated: 2026-11-25T22:13:23Z*

Two published guidance documents seeded a large part of the rule set. Source A
is the performance skill guide in `EgorBo/BlazingFastDotnet`. Source B is the
runtime unsafe-code guidance page in `dotnet/docs`.

Each read below checked the guide against the shipped rule set and the
installed analyzers. The catalog keeps the shipped rule IDs. See
[catalog.md](catalog.md).

## Rows That Stay A Document

| Row                                    | Why it stays a document                   |
| -------------------------------------- | ----------------------------------------- |
| Ordinal string comparison              | CA1307, CA1309, and CA1310 cover it       |
| `ToLower()` equality idiom             | Meziantou covers it                       |
| Runtime knob tuning                    | Environment variables, not source         |
| False sharing and cache-line padding   | Not visible to the compiler               |
| Thread-local and striped counters      | Detection needs runtime behavior          |
| `WebApplication.CreateSlimBuilder`     | Project-level choice, not a code defect   |
| Source-generated JSON registration     | Whole-project configuration, no local fix |
| Object header and MethodTable access   | No supported replacement                  |
| Fuzzing and raw IL validation          | Process guidance, not a code shape        |
| `AggressiveInlining` overuse           | The size heuristic is too noisy           |
| NativeAOT publish properties           | Publish configuration, not source         |
| Hot-path wrappers and hash choice      | No traffic or security requirement        |

## Second Read

A second read on 2026-10-31 found ten more rows with a code shape that an
analyzer can see. Nine of them ship. The tenth folded into a shipped rule.

| ID            | Rule                                                   | Source  | Code fix | Noise  |
| ------------- | ------------------------------------------------------ | ------- | -------- | ------ |
| none          | Static byte array literal becomes an RVA span property | C5      | Yes      | Low    |
| E128131       | Parse and format UTF-8 bytes without a string          | C7, C28 | Yes      | Low    |
| E128132       | Use an ordinal comparer in a string dictionary or set  | C27     | Yes      | Low    |
| E128133       | Look up a span key with `GetAlternateLookup`           | C10     | Yes      | Medium |
| E128134       | Use `BitOperations` instead of a manual bit loop       | C17     | Yes      | Medium |
| E128135       | Use `TensorPrimitives` over a numeric span             | C12     | Partial  | Medium |
| E128136       | Use `Span<T>` instead of a pointer and a length        | 16.1    | No       | Low    |
| E128137       | Do not access nonpublic BCL members                    | 3.4     | No       | Low    |
| E128138       | Do not emit raw IL with `ILGenerator`                  | 18.1    | No       | Low    |
| none          | Guard pointer indexing with `Debug.Assert`             | 9.4     | No       | High   |

The byte literal row folded into E128061. That rule owns every
`private static readonly T[]` field, so a second rule would report the same
field twice. The E128061 fix reads a constant byte sequence as a
`ReadOnlySpan<byte>` property and every other element type as
`ImmutableArray<T>`.

The unsafe guidance row 20.2 says to abandon a pooled buffer instead of
returning it in `finally`. The repository requires a zero and a return in
`finally`. Keep the repository rule. It satisfies the row, because the buffer
is provably unused at that point.

The `Debug.Assert` guard idea stays unshipped. Its noise is high, so it needs a
real defect in review before a rule is worth the false positives. The E128139
ID belongs to the unaligned-access rule.

## Third Read

A third read on 2026-10-31 found three code shapes that the first two reads
missed. Two of them ship a code fix. E128139 covers the `bool` case only.

| ID      | Rule                                                         | Source                            | Code fix         | Noise  |
| ------- | ------------------------------------------------------------ | --------------------------------- | ---------------- | ------ |
| E128139 | Do not move a non-blittable type through an unaligned access | Unsafe guide, struct bit patterns | `bool` read only | Low    |
| E128140 | Use a span bulk method instead of an element loop            | Performance guide, bulk span APIs | Yes              | Low    |
| E128141 | Use a u8 literal instead of `Encoding.UTF8.GetBytes`         | Performance guide, UTF-8 parsing  | Yes              | Low    |

E128139 extends E128118 to the `Unsafe` read and write calls. E128118 covers
`MemoryMarshal` only. E128140 covers the `Fill`, `Clear`, and `CopyTo` shapes.
E128141 needs C# 11 or later, and the rule checks the language version.

## Fourth Read

A read on 2026-11-17 checked the whole guide against the shipped rule set and
the installed analyzers. The guide covers profiling, runtime knobs, NativeAOT,
ASP.NET Core, strings, and vectorization.

Three shapes from that read shipped as E128229 to E128231: the `Func<Task>`
middleware delegate, `UseStaticFiles`, and `string.Split` used only to
enumerate. Three more joined the Tier 2 table: a per-item `Parallel.For` body,
a `FileStream` captured by a parallel body, and a `readonly` field re-read
inside a loop.

Four shapes have an installed owner. See
[skipped-decisions.md](skipped-decisions.md).

Two extensions belong to shipped rules, not to new IDs. E128135 covers a sum
loop and a min or max loop with a neutral initial value. E128084 covers
`List.GetRange` and an indexer loop over a list.
The `Add` and `Multiply` loop shapes ship as E128255. Neither maps to a one-span
reduction, so the rule reads the indexed `for` shape and writes to a second span.

## Related

- [catalog.md](catalog.md): the settled rule catalog and the ID map
- [index.md](index.md): the open register
- [skipped-decisions.md](skipped-decisions.md): every declined shape and its reason
