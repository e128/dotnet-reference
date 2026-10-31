# Analyzer Candidates
*Updated: 2026-10-31T21:17:52Z*
*Source: analyzer-review-miner 2026-04-15, evidence window 2026-04-12..2026-04-15*

No open candidate remains from the 2026-04-15 mining pass. Each of the three
candidates below now ships as a real analyzer. See the catalog table for the
rule ID, and see `src/E128.Analyzers/README.md` for the current rule
description.

- `mutable-static-readonly-array` shipped as E128061.
- `test-reference-assemblies-tfm-mismatch` shipped as E128062.
- `readonly-struct-property-no-init` shipped as E128074.

## Catalog

| ID      | Name                                   | Status      | Score | Last seen  |
| ------- | -------------------------------------- | ----------- | ----- | ---------- |
| E128061 | mutable-static-readonly-array          | implemented | 5/5   | 2026-04-15 |
| E128062 | test-reference-assemblies-tfm-mismatch | implemented | 4/5   | 2026-04-15 |
| E128074 | readonly-struct-property-no-init       | implemented | 3/5   | 2026-04-15 |
| E128070 | pool-rent-capacity-guard               | implemented | n/a   | 2026-05-09 |
| E128071 | fips-unapproved-hash                   | implemented | n/a   | 2026-05-10 |
| E128072 | sha256-create-obsolete                 | implemented | n/a   | 2026-05-10 |

## Rules From Guidance Documents

Two published guidance documents seeded the rules below. Most rules carry a code
fix provider. The rules E128125 to E128129 and E128136 to E128138 carry a
diagnostic only.

- Source A: the .NET performance skill guide in `EgorBo/BlazingFastDotnet`.
- Source B: the runtime unsafe-code best-practices page in `dotnet/docs`.

This table records the rule IDs so a later session can check for ID reuse. The
rule description and the fix shape live in `src/E128.Analyzers/README.md`.

| ID range        | Domain      | Code fix        | Category directory |
| --------------- | ----------- | --------------- | ------------------ |
| E128112-E128115 | Unsafe code | every rule      | `Unsafe/`          |
| E128116-E128117 | Performance | every rule      | `Performance/`     |
| E128118-E128120 | Unsafe code | every rule      | `Unsafe/`          |
| E128121-E128124 | Performance | every rule      | `Performance/`     |
| E128125-E128129 | Unsafe code | no rule         | `Unsafe/`          |
| E128131-E128135 | Performance | E128135 partial | `Performance/`     |
| E128136-E128138 | Unsafe code | no rule         | `Unsafe/`          |
| E128139         | Unsafe code | `bool` read     | `Unsafe/`          |
| E128140-E128141 | Performance | every rule      | `Performance/`     |

The lowest free ID is E128130.

These rules have no code fix. The remediation depends on intent that the syntax
tree does not carry.

- E128125: the pin scope is a caller decision.
- E128126: the replacement depends on how the new string is built.
- E128127: safe code has no null byref equivalent.
- E128128: the anchor field must be placed by the user.
- E128129: the replacement depends on whether the caller needs a deep or a shallow copy.

The guidance rows that stay a document and not a rule:

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

## Second Read Of The Guidance Documents

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
ID belongs to the unaligned-access rule below.

## Third Read Of The Guidance Documents

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
