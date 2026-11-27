# Pugworks Review Sweep
*Updated: 2026-11-27T15:25:11Z*

This sweep reads the pugworks repository changes from 2026-11-24 to 2026-11-27.
It covers the Harvest document-extraction plan and its review-fix plan. The read
looks for a review fix that a Roslyn rule could prevent at compile time.

## Shipped Rules

The 2026-11-27 batch shipped all three shapes.

| Pattern                                                        | Score | Rule    |
| -------------------------------------------------------------- | ----- | ------- |
| A structured log message carries a local full path             | 4/5   | E128256 |
| A property on a bound options type is non-public               | 4/5   | E128257 |
| A DI service lookup throws a generic exception with no key     | 3/5   | E128258 |

## Detail: Path In A Log Message

The review replaced `file.FullName` with `file.Name` in two PDF log calls and
two `ExtractionService` debug calls. The `NotSupportedException` text kept the
full path, because the caller asked for that file.

E128166 reads the argument name for a credential word. `filePath` is not
sensitive by that test. A rule must read the value expression instead. The
`FileInfo` and `DirectoryInfo` types expose `FullName` and `Name`. The rule
reports `FullName` in a logger call or a `[LoggerMessage]` parameter, and it
leaves an exception message alone.

Score: frequency 1, gap 1, expressibility 1, value 1.

## Detail: Non-Public Options Property

`ExtractionOptions.OcrConcurrency` was `internal` and `init`. The review made
the property `public` and added `[Range(1, 64)]`. The binder reads public
instance properties. `ValidateDataAnnotations()` also validates public
properties. A non-public property takes its default value and ignores the
configuration key. E128033 checks an `init` accessor. E128179 checks that a
registration calls a validation method. Neither checks accessibility.

Score: frequency 1, gap 1, expressibility 1, value 1.

## Shipped-Rule Fix: E128033 Reads The Extension Method

`OptionsBindInitAnalyzer` matches `OptionsBuilder<T>.Bind` by the containing
type of the method. The real `Bind` is an extension method in
`OptionsBuilderConfigurationExtensions`, and its containing type is not generic.
The analyzer therefore returns no options type for the canonical
`AddOptions<T>().Bind(...)` call. The same check misses `BindConfiguration`.

The tests defined a fake `OptionsBuilder<T>` with an instance `Bind` method, so
they passed. Pugworks binds `ExtractionOptions` through the real extension call
and keeps `E128033` at `error`. The type still held `init` properties, and the
build passed.

The 2026-11-27 batch resolves the receiver type instead. It accepts `Bind` and
`BindConfiguration`. Two tests use a fake extension class in
`Microsoft.Extensions.DependencyInjection`.

## Declined

- An unbounded collection filled from parsed input. The PDF image list, the font
  histogram, and the encoded image bytes each gained a cap. Detection needs a
  trust dataflow, which Roslyn does not model. E128037 and E128070 own the
  nearby unbounded forms.
- A redundant freeze across a constructor boundary. The `IReadOnlySet<T>`
  parameter erases the frozen type, so the analyzer cannot see the second
  freeze. E128026 owns the local `new HashSet<T>().ToFrozenSet()` form.
- A numeric options property with no range. Many numeric options are unbounded
  on purpose. E128179 owns the registration-level check.
- A synchronous parse on the caller thread inside an async method. A
  `Task`-returning API hides the synchronous body.
- `Assembly.Location` with no single-file guard. IL3000 covers the publish
  mode, and the location is empty only in that mode.
- An MSBuild `AssemblyMetadata` value from `UtcNow` breaks the incremental
  build. This is a project-file shape. It matches the slopwatch project-file
  decision.
- An embedded-resource name built from a string prefix. The 2026-11-25 refactor
  removed the raw duplication. The duplicate-code row already covers the class.

## Evidence

- Pugworks commits `615c38c1f` to `6d4ae76fc`, 2026-11-26 and 2026-11-27.
- The `PdfExtractionClient` and `ExtractionService` log changes.
- The `ExtractionOptions.OcrConcurrency` change.
- The `ServiceCollectionExtensions.cs` registration at 2026-11-27.

## Related

- [index.md](index.md): the open register
- [catalog.md](catalog.md): the shipped rules
- [unsafe-sweep.md](unsafe-sweep.md): the previous review read
