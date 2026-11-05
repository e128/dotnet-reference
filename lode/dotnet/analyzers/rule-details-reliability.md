# Reliability Rule Details
*Updated: 2026-11-05T15:20:29Z*

Behavior notes for the reliability rules. The complete rule table, the
severity, and the code-fix status live in `src/E128.Analyzers/README.md`. The
async and concurrent rules live in
[rule-details-reliability-concurrency.md](rule-details-reliability-concurrency.md).
The remaining rules live in [rule-details.md](rule-details.md).

### E128103: Named `HttpClient` resolve with no registration

One rule reports two shapes. The first is a DI resolve with no `Add*` or
`TryAdd*` registration in the compilation. The second is
`IHttpClientFactory.CreateClient(name)` with no `AddHttpClient(name, ...)` in the
compilation.

The named-client shape is silent for an empty name, for the unnamed overload,
and for a non-constant name. It reports once per name, at the first use.

Two descriptors share the `E128103` id. A test harness then needs
`MarkupOptions = MarkupOptions.UseFirstDescriptor`, or every markup test throws
`Multiple diagnostic descriptors with ID E128103 were found`.

### E128176: Event subscription that keeps the subscriber alive

Severity: **Warning**. Flags an event subscription inside a disposable type
where the handler reads the subscriber instance and no `Dispose`,
`DisposeAsync`, or `DisposeAsyncCore` method removes the same event. The publisher holds the delegate and the delegate holds the
subscriber, so the subscriber stays reachable after its owner drops it.

**Exclusions**:

- A type that implements neither `IDisposable` nor `IAsyncDisposable`. The type
  has no cleanup site to compare against, so the remedy is a new lifetime.
- A static handler, which holds no subscriber instance.
- A subscription to an event on this instance, which dies with the subscriber.
- A subscription to an event on an instance field that a dispose method disposes.
- A handler that reads only a parameter or a local.
- A member read on another receiver, such as `other.Field`, which does not
  reach this instance.

The rule reads the type once through `RegisterSymbolStartAction`. A partial
type that removes the subscription in another file is therefore covered. Any
`-=` on the same event inside a method named `Dispose` or `DisposeAsync`
clears the report, because handler identity is not part of the check.

No code fix. The removal needs a stored delegate field, and the field shape
depends on the class.

### E128177: Record member with a mutable collection type

Severity: **Warning**. Flags a record member whose type is a mutable collection.
The generated `Equals` compares that member by reference, so two records with
equal contents compare unequal, and a mutation after construction breaks
hash-based lookup.

The rule reports a positional parameter, an auto-property, and a field. An
abstract property and a computed property do not join the generated equality, so
they are skipped. A static member and a const field are skipped for the same
reason. Both a record class and a record struct are checked.

Type matching reads `MetadataTypes.Find` at compilation start and compares
`OriginalDefinition` through `MetadataTypes.Contains`. A `string` and an
immutable collection are never in the mutable set.

No code fix. The repair changes a public member type, and the right replacement
depends on how the caller mutates the collection.

### E128178: Duplicate `JsonPropertyName` value

Severity: **Error**. Flags two members of one type that map to one JSON name.
System.Text.Json throws `InvalidOperationException` at the first serialize or
deserialize call.

An ignored member, an indexer, a static member, and an explicit interface
implementation are skipped. A member that is not public is skipped unless it
carries `[JsonInclude]`. A `[JsonIgnore]` with a condition other than `Always`
keeps the member in the contract. A derived member that overrides or hides the
base member it would collide with is skipped, because it replaces the base member
in the contract. The comparison uses `StringComparer.Ordinal`, so a name pair that
differs only by case does not report. The rule registers a named-type symbol
action, so a partial type is read once.

No code fix. The rename choice belongs to the caller.

### E128181: `HttpClient.Timeout` set to an infinite value

Severity: **Warning**. Flags an assignment of `Timeout.InfiniteTimeSpan` or
`TimeSpan.FromMilliseconds(-1)` to `HttpClient.Timeout`. A stalled server then
holds the caller and its thread pool slot. `TimeSpan.MaxValue` is not reported,
because the setter throws `ArgumentOutOfRangeException` for any value above
`int.MaxValue` milliseconds.

The rule reads the left side as the `Timeout` property declared on exactly
`System.Net.Http.HttpClient`, so a user type with its own `Timeout` is silent.
The right side must be one of the named forms. A `FromMilliseconds` argument is
read with `GetConstantValue`, and only a constant `-1` reports. A computed value
is skipped, because the rule cannot prove it is infinite.

One assignment action covers an object initializer, a plain property assignment,
and a lambda body.

No code fix. A safe timeout value is a policy choice.

### E128188: Exception filter reads the message text

Severity: **Warning**. Flags a `when` filter that reads `Message`,
`StackTrace`, or `ToString` on the caught exception. The filter runs before any
`catch` clause, so a formatted member costs work on the fault path and can throw
inside the filter itself.

The rule registers `SyntaxKind.CatchFilterClause`. It matches a member access on
the identifier of the caught variable. A read on another object, a method call
such as `ex.GetType()`, and a filter on a `catch` clause with no variable stay
silent.

**Exclusions**:

- A read of `HResult` or `InnerException`.
- A member read on an expression other than the caught variable.
- A method call on the caught exception.

No code fix. Filter on a typed exception or a property, and read the message in
the `catch` body.

### E128189: `GCHandle` with no `Free` call

Severity: **Warning**. Flags a local that stores a `GCHandle.Alloc` result in
the same body and never calls `Free` on it. The handle pins the target object,
so a missing `Free` keeps the object reachable and pinned. The rule also reads a
later assignment to the same local.

**Exclusions**:

- A local that a method call receives, because the callee may free it.
- A local that the body returns.
- A local stored in a field, a property, another local, or a collection.
- An allocation assigned directly to a field, a property, a parameter, or an
  element.
- A discarded allocation.
- A compilation without `System.Runtime.InteropServices.GCHandle`, or one where
  a user type shadows the name.

The body is the nearest method, constructor, local function, accessor, or
lambda body. A `Free` call anywhere in that body clears the report, including a
call in a `finally` block.

No code fix. The release site needs a `try`/`finally`, and the shape depends on
the method.

### E128195: Configuration key absent from the appsettings files

Severity: **Warning**. Flags a constant configuration key that no JSON
additional file defines. A missing key returns null, or throws at startup when
the reader calls the required form.

The rule reads the key from three sites:

- An `IConfiguration` indexer with one argument.
- `GetSection` or `GetRequiredSection` on `IConfiguration`.
- `GetConnectionString(name)` on `ConfigurationExtensions`, which maps to
  `ConnectionStrings:name`.

At compilation start the rule reads each `*.json` additional file with
`AppSettingsKeyReader`, a hand-written scanner that accepts comments and a
trailing comma. It flattens each document to colon-separated paths and adds
every intermediate node, so a section read matches a prefix and an array element
matches its index. The comparison ignores case, because the configuration
provider ignores case.

**Exclusions**:

- A non-constant key.
- A receiver whose type is not `IConfiguration`, or that does not implement it.
- A receiver that is an `IConfigurationSection`. Its keys are relative to the section.
- An indexer assignment such as `configuration["Key"] = value`, which defines the key.
- A compilation with no parsable JSON additional file. The rule then cannot
  know the key set, so it stays off.
- A key in the `e128_added_configuration_keys` option.

The rule stays off without `Microsoft.Extensions.Configuration.IConfiguration`
in the compilation. A project must pass its files through
`<AdditionalFiles Include="appsettings.json" />`. A key from an environment
variable, a user secret, or a file outside the project needs an option entry.

No code fix. The repair adds the key to a file, or reads a default.

### E128197: Fixed day count used as a calendar month

Severity: **Info**. Flags an inline `TimeSpan.FromDays(k)` call whose constant
`k` is 28 to 31 and whose value flows into `DateTime` or `DateTimeOffset`
arithmetic. The rule reads the `Add` and `Subtract` methods, the `+` and `-`
operators, and the `+=` and `-=` assignments. A 30-day span is often a fixed
retention window, so the severity stays low.

**Exclusions**:

- A span held in a variable, a constant field, or a computed value.
- A day count outside 28 to 31.
- `DateTime.AddDays`, which asks for a day count.
- A user type named `TimeSpan`.

No code fix. The calendar step belongs to the caller.

### E128198: `Substring` count built from a length minus a constant

Severity: **Warning**. Flags a two-argument `string.Substring` call whose count
is a `Length` value minus a positive constant. A short string makes the count
negative, and the call throws.

The count shapes are `Length - k`, `Length - start - k`, and the parenthesized
forms. A length read on the same string in an enclosing `if` condition or
conditional expression clears the report. So does a preceding `if` with no
`else` that returns, throws, continues, or breaks after a length test. A plain
comparison decides the branch: `Length > k` guards the then branch, and
`Length < k` guards the else branch. Any other condition guards both.

**Exclusions**:

- A subtraction whose right side is a variable.
- A length test on a different string.
- A guard in a caller method, because the rule reads one body.
- A one-argument `Substring` call and a constant count.

No code fix. The guard belongs to the caller.

## Related

- [rule-details-reliability-concurrency.md](rule-details-reliability-concurrency.md):
  the async and concurrent rules
- [rule-details-reliability-resources.md](rule-details-reliability-resources.md):
  resource lifetime and shared state
- [rule-details.md](rule-details.md): the remaining rule details
- [pooled-buffer-rules.md](pooled-buffer-rules.md): buffer ownership rules
- [redundant-rules.md](redundant-rules.md): rules disabled because another analyzer covers the shape
