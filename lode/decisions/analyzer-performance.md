# Analyzer Decisions: Performance
*Updated: 2026-11-22T04:00:12Z*

Rationale removed from source comments. Each section names the type that carried it.
Read this page before you change or delete the matching check.

## AggressiveOptimizationAnalyzer
- Resolves the attribute through GetSymbolInfo instead of comparing the written name, so MethodImpl, MethodImplAttribute, and a qualified spelling all match one type. A name comparison would silently miss aliases.

## AlternateLookupAnalyzer
- Reports only a lookup method whose first argument is a key. As a result, ContainsValue and other value-position members never prove the key came from the span. The first-argument position is the contract.
- Keeps the key-lookup member names in one set because ContainsValue takes a value position, not a key. Adding it would report a value lookup as a span-key allocation.

## ComplexityCeilingAnalyzer
- Counts a for-loop counter as an element only when it indexes the collection the inner loop walks. A bound such as j < i still grows with the input. As a result, treating every counter as a non-element would miss real nesting.

## DllImportToLibraryImportAnalyzer
- Registers nothing when LibraryImportAttribute is absent. This holds because a target framework before .NET 7 has no remedy. Removing the gate would produce reports a consumer cannot fix.

## EfQueryInLoopAnalyzer
- Treats AsEnumerable and AsAsyncEnumerable as deferred calls that report only as the source of a foreach inside an outer loop. Those calls do not run a query. As a result, reporting them directly would be false.
- Stays silent without DbSet<T> and IQueryable. This holds because a compilation without Entity Framework Core cannot contain the query source the rule describes.
- Requires a resolved method symbol before reporting. As a result, a tree being edited, where binding is incomplete, produces no report.

## ElementLoopToBulkSpanAnalyzer
- Accepts a Fill value only when re-evaluating it is harmless. Span.Fill evaluates the value once. As a result, a call or a property read inside the rewritten loop would run a different number of times.
- Requires the loop bound to match the source length because Span.CopyTo reads the whole source. A shorter loop would copy trailing elements the original loop left untouched.
- Rewrites only a plain local, parameter, or field target because the fix evaluates the destination once. A call, a property read, or the loop variable would change meaning after the rewrite.
- Treats Clear as writing default(T), not zero. A zero literal is not default(T) for a nullable element, an object element, or a type reached through a user-defined conversion.
- Classifies a default literal as clearing, a null literal as clearing only a reference or nullable element, and any other literal as undecided. The caller keeps checking an undecided literal instead of assuming a value.

## FileStreamWithoutUseAsyncAnalyzer
- Treats an omitted useAsync argument as synchronous and stays silent on a non-boolean constant, which cannot bind to a bool parameter.
- Treats FileOptions as asynchronous only when the value carries the Asynchronous bit. Every other constant, including FileOptions.None, keeps synchronous I/O.
- Matches the options parameter by name and type so the FileStreamOptions overload, which this rule does not read, stays out of scope.

## ImmutableArrayCreateToArrayAnalyzer
- Allows only the BCL ToArray owners that always return a fresh array. A member added to the list would make the zero-copy rewrite change aliasing.
- Binds symbols once, after the syntax gates. This holds because both gates reject only calls the symbol check also rejects. The diagnostic set is unchanged and most invocations skip binding.
- Requires an allowlisted ToArray owner because AsImmutableArray wraps the array without a copy. An arbitrary ToArray that returns a cached array would alias the result.

## IndexOfAnyToSearchValuesAnalyzer
- Registers nothing when SearchValues<T> is absent. This holds because the suggested replacement does not compile before .NET 8.

## LargeStructByValueAnalyzer
- Uses 24 bytes as the by-value threshold because above that size a copy costs more than an indirection. The threshold is a model, not a measured constant.
- Models sizes for a 64-bit process, the only target the repository supports. As a result, a pointer field counts as 8 bytes.
- Caps struct nesting at 16 levels and treats a deeper chain as too large to measure. The cap also stops a self-referential struct in broken code from recursing without end.

## LinearLookupInLoopAnalyzer
- Leaves an IQueryable receiver to E128180 because a query source costs a database round trip, not an in-memory scan. Two rules reporting the same call would duplicate the diagnostic.

## ListGetRangeAnalyzer
- Skips a GetRange call whose converted type must remain List<T>. This holds because the fix returns T[] and would break the assignment.
- Reports only a receiver that flow analysis proves non-null. GetRange throws on a null receiver while AsSpan(null) returns an empty span. As a result, the rewrite would change behavior.

## LoggerArgumentGuardAnalyzer
- Accepts a guard only in the then branch of an if that checks the same logger and level. It also accepts a guard after an earlier if (!guard) return. Any other position does not prove the level is enabled.
- Rejects a guard that is negated or is one side of ||. In if (verbose || logger.IsEnabled(Information)) the call still runs when logging is off.
- Follows one level of indirection: a local holding the check, or a helper method or property returning it. Deeper indirection is not modeled.

## ParallelCollectionIndexAnalyzer
- Caches both subtree scans per compilation because the answer depends only on the declaring lambda or loop and the index name. A body with many element accesses then pays for each walk once.
- Recognizes two bound shapes: a lambda parameter bounded by Enumerable.Range(0, X.Count), and a for-loop variable bounded by X.Count. Any other bound shape is not modeled.
- Treats a length comparison against the indexed collection anywhere in the same scope as making the access safe. A narrower search would report guarded accesses.
- Includes the final symbol name in the guard cache key because the scan compares only that name.
- Compares the final name segment. As a result, rows, this.rows, and state.rows all match. Comparing the printed expression to the symbol name would drop a qualifier and miss a present length guard.

## HandlerNoDecompressionAnalyzer
- Reports a private field initializer that sets no decompression when no declaration of the containing type sets the property or reassigns the field.
- Stays silent for a field set in a constructor and for a non-private field, because another site can configure the handler.

## ParallelForEachAsyncCapAnalyzer
- Treats default and default(ParallelOptions) as an options object with no degree cap. As a result, the call reports.
- Follows a local, field, or property initializer and a single-return factory call to the options creation. A parameter or an unresolvable value stays silent.

## ParallelForPerItemBodyAnalyzer
- Gates the rule on the presence of Partitioner.Create and Parallel.ForEach because the fix needs both. A compilation without the remedy gets no report.
- Supports the int and long range overloads and the `Action<T, ParallelLoopState>` body. The `For<TLocal>` overload stays silent.
- Maps a named argument to its parameter, so a call with names still reports and fixes.
- Reports a method group whose parameters match the body delegate and that returns void. The fix invokes it inside the generated loop.
- Reports an async lambda or async void method group with the `Parallel.ForEachAsync` remedy. It offers no fix for that shape.
- Requires the body to avoid the name range because the fix declares a lambda parameter with that name. A body parameter with the same name would collide and the rewrite would not compile.
- Rejects a body that mutates the item parameter or passes it by reference. This holds because the fix reuses that parameter as the loop variable. Such a mutation would write the loop counter.
- Treats any other use of the name range, including a member access such as state.range, as a collision. The scan is wider than strictly needed so the emitted lambda cannot conflict.
- Skips a body that already runs a loop per item. This holds because a range partition adds no benefit over the existing inner loop.
- Accepts an expression body only when it fits a statement position. This holds because the rewrite turns the lambda body into one statement of the for loop. A read such as i => items[i] does not fit.
- Stops its body walk at a nested lambda or local function, since a loop or await there stays nested after the rewrite. The parameter-mutation scan deliberately does not stop.

## PresizeCollectionAnalyzer
- Lists only types with a public capacity constructor, so SortedSet and SortedDictionary stay out and the fix remains applicable.

## ReadOnlyMemoryStreamAnalyzer
- Treats an assignment or an increment of a settable property such as Position as a write. This holds because the replacement types reject a write.

## ReadonlyFieldLoopHoistAnalyzer
- Stops the suffix search after a fixed number of collisions. A method that declares that many names from one base does not benefit from a hoisted local.
- Reports only the outermost loop. This holds because its fix rewrites every read inside it, including reads in nested loops. A nested report would name a rewrite the outer loop already owns.
- Requires the loop to sit in a block that can hold the inserted local. An embedded statement with no block, or a loop under a switch section, has no place for the declaration.
- Rejects a field assigned inside the loop. This holds because only a constructor can assign a readonly field. The hoisted copy would hide the new value for the rest of the loop.
- Rejects a nameof argument. This holds because hoisting the field name into a local would change the constant string the expression produces.
- Validates suffixed candidate names by parsing them. This holds because a field such as _if derives the keyword if, which is not a legal identifier.
- Reserves names by field base name. As a result, an earlier sibling loop always reserves its identifier. A same-named symbol from another kind costs only one suffix.

## RuntimeRegexToGeneratedAnalyzer
- Registers nothing without GeneratedRegexAttribute because a target framework before .NET 7 has no remedy.
- Requires C# 13 because the fix emits a partial property.

## SaveChangesInLoopAnalyzer
- Fails closed: a compilation without Entity Framework Core cannot contain the call, and a look-alike type named DbContext is not EF.
- Runs the syntactic name gate before loop walking or overload resolution. This holds because most invocations are not SaveChanges and must not pay that cost.
- Reports the whole conditional access for a null-conditional call. As a result, the location covers the receiver that may be null.
- Accepts the declaring type of an instance call as the EF proof, and walks the base chain so a derived DbContext still matches.
- Still reports a derived receiver when overload resolution binds the call to a base declaration the analyzer cannot walk. A type parameter bound is an example.
- Also accepts an extension method whose containing type carries the EF namespace or assembly, and a receiver that names an unbound generic EF type.

## SearchValuesHoistingAnalyzer
- Requires every fixed input to be a constant or a static readonly value because the fix moves the call into a static initializer. A local, parameter, or instance member would not compile there.
- Requires a member call to be static. This holds because hoisting an instance call into a static field initializer does not compile.

## SequenceEqualLoopAnalyzer
- Rewrites only when the code before the loop already rejects a length mismatch, because SequenceEqual compares lengths first and the loop does not.
- Excludes floating-point elements because != treats NaN as unequal while SequenceEqual does not.

## Sha256CreateObsoleteAnalyzer
- Registers nothing when HashData is absent. This holds because the suggestion has no compiling remedy before .NET 5.

## StaticReadonlyFrozenCollectionAnalyzer
- Registers only when FrozenSet<T> exists. This holds because the rewrite has no target type before .NET 8.
- Rewrites the whole declaration because one type node declares every variable in the list, and it requires an initializer at the declaration site. Rewriting one variable alone would drop the others.

## StringBuilderPoolAnalyzer
- Names an API from the Pug package. Without that type the suggestion has no remedy. As a result, the analyzer does not register.

## ToListInForeachAnalyzer
- Relies on Enumerable owning every ToList binding in the base library, including for IQueryable<T> and ImmutableArray<T> receivers. It also relies on Enumerable having ToArray for the rewrite.
- Reports only when System.Linq.Enumerable exists. This holds because a type with its own ToList is not a LINQ method.

## Utf8GetBytesLiteralAnalyzer
- Treats a u8 literal as ReadOnlySpan<byte> only. This holds because a Span<byte> target takes the byte[] shape through the implicit array conversion.
- Keeps the byte[] shape when the call has a named sibling argument. This holds because the written position is then unreliable.

Related: [Decision Records](summary.md) explains how to use this page.
