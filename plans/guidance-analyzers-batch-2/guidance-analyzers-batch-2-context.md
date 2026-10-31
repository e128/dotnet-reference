# Guidance Analyzers Batch 2 Context

*Updated: 2026-10-31T19:20:54Z*

## Source Rows

The source row labels match the two guidance documents. The C rows come from
the performance skill guide. The numbered rows come from the unsafe
best-practices page.

| ID      | Source row | Rule shape                                                |
| ------- | ---------- | --------------------------------------------------------- |
| E128131 | C7, C28    | `Encoding.UTF8.GetString` feeds a `Parse` or `TryParse` call |
| E128132 | C27        | String dictionary or set with a culture comparer          |
| E128133 | C10        | `new string(span)` used as a key into a dictionary or set |
| E128134 | C17        | Loop that counts set bits by hand                         |
| E128135 | C12        | Loop that sums a float or double span                     |
| E128136 | 16.1       | Method takes a pointer and a length                       |
| E128137 | 3.4        | Reflection reaches a nonpublic member of a BCL type       |
| E128138 | 18.1       | Code emits raw IL through `ILGenerator`                   |

## Decisions

- **E128061 owns the RVA fix.** The two rules would flag the same field. The
  E128061 fix provider picks the RVA span form for a byte or sbyte literal
  initializer and the `ImmutableArray<T>` form for every other element type.
  One diagnostic yields one action, so a Fix All run is deterministic.
- **E128131 uses the current culture.** `int.Parse(string)` reads the current
  culture. The fix passes `CultureInfo.CurrentCulture` to the UTF-8 overload
  when the span overload needs a provider.
- **E128135 tests use a stub type.** `System.Numerics.Tensors` is not in the
  Microsoft.NETCore.App reference pack. Each test declares a stub
  `TensorPrimitives` type, so the test run needs no package download.
- **E128133 reports only a provable comparer.** The fix calls
  `GetAlternateLookup`, which throws for a culture comparer. The report
  requires a local declared by an object creation with no comparer or an
  ordinal comparer.

## Prior Art

The first batch shipped E128112 to E128129 from the same two documents. See
[analyzer-candidates.md](../../lode/dotnet/analyzer-candidates.md) for the
rows that stay a document.
