# Reliability Rule Details
*Updated: 2026-11-12T21:11:18Z*

Behavior notes for the reliability rules. The complete rule table, the
severity, and the code-fix status live in `src/E128.Analyzers/README.md`. The
async and concurrent rules live in
[rule-details-reliability-concurrency.md](rule-details-reliability-concurrency.md).
The resource and lifetime rules live in
[rule-details-reliability-resources.md](rule-details-reliability-resources.md).
The remaining rules live in [rule-details.md](rule-details.md).

### E128177: Record member with a mutable collection type

Severity: **Warning**. Flags a record member whose type is a mutable collection.
The generated `Equals` compares that member by reference, so two records with
equal contents compare unequal, and a mutation after construction breaks
hash-based lookup.

The rule reports a positional parameter, an auto-property, and a field. An
abstract property and a computed property do not join the generated equality, so
they are skipped. A static member and a const field are skipped for the same
reason. Both a record class and a record struct are checked. A base record that
declares `GetHashCode` owns the equality, so the derived member stays silent. A
compiler-generated member is implicit and does not count.

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

### E128195: Configuration key absent from the appsettings files

Severity: **Warning**. Flags a constant configuration key that no
`appsettings*.json` additional file defines. A missing key returns null, or
throws at startup when the reader calls the required form. The rule reads only
those files.

The rule reads the key from three sites:

- An `IConfiguration` indexer with one argument.
- `GetSection` or `GetRequiredSection` on `IConfiguration`.
- `GetConnectionString(name)` on `ConfigurationExtensions`, which maps to
  `ConnectionStrings:name`.

At compilation start the rule reads each `appsettings*.json` additional file
with `AppSettingsKeyReader`, a hand-written scanner that accepts comments and a
trailing comma. The reader requires an object or array root, so a lone literal
does not parse as a zero-key document. A file that fails to parse is skipped.
The reader flattens each document to colon-separated paths and adds every
intermediate node, so a section read matches a prefix and an array element
matches its index. The comparison ignores case, because the configuration
provider ignores case. A simple assignment defines the key and is not read. A
compound assignment reads the key before it writes, so it still reports.

**Exclusions**:

- A non-constant key.
- A receiver whose type is not `IConfiguration`, or that does not implement it.
- A receiver that is an `IConfigurationSection`. Its keys are relative to the section.
- An indexer assignment such as `configuration["Key"] = value`, which defines the key.
- A compilation with no parsable `appsettings*.json` additional file. The rule
  then cannot know the key set, so it stays off.
- A key in the `e128_added_configuration_keys` option. The option adds keys on
  top of the parsed files and never activates the rule alone.

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

### E128013 and E128014: `[GeneratedRegex]` quantifier heuristics

Severity: **Warning**. `GeneratedRegexHelpers` scans the pattern literal of a
`[GeneratedRegex]` attribute. `HasOverlappingQuantifiers` detects E128013,
which risks quadratic backtracking. `HasNestedQuantifier` detects E128014
through `FindNestedQuantifierSpan`.

E128014 flags an unbounded outer quantifier (`*`, `+`, or `{n,}`) over a group
whose inner scan finds `*` or `+` at group depth 1. A bounded `{n}` or `{n,m}`
outer repeat is linear and stays silent. An atomic `(?>...)` group stays
silent. A group that leads with a distinct separator atom, such as
`(?:/[^\s:"']+)+`, also stays silent: `FirstAtomCanAlsoBeLast` exempts it when
the first and last consuming atoms cannot match the same text.

Both scans treat character class contents as literals. `BuildCharacterClassMask`
marks every index inside a `[...]` class in one pass, and each scan skips masked
indices. The mask handles a leading `^`, a leading literal `]`, and escapes. A
quantifier inside a class, such as the `*` in `([-*_a])+`, never reports.

**Known limits**:

- A positive class that holds whitespace, such as `[ ]`, counts as disjoint
  from `\s`, so the overlap check can miss a true positive.
- `FirstAtomCanAlsoBeLast` compares atom tokens without expanding class
  contents. A class that can match the other atom can exempt a group.
- The heuristics are string scans, not a pattern parse. No public
  `RegexParser` API exists, so a full parse is not available.

Both rules have code fixes. The fix helpers reuse the analyzer detection so the
rewrite targets the reported element. See
[code-fix-output.md](code-fix-output.md) for the literal rewrite rules.

## Related

- [rule-details-reliability-concurrency.md](rule-details-reliability-concurrency.md):
  the async and concurrent rules
- [rule-details-reliability-resources.md](rule-details-reliability-resources.md):
  resource lifetime and shared state
- [rule-details.md](rule-details.md): the remaining rule details
- [pooled-buffer-rules.md](pooled-buffer-rules.md): buffer ownership rules
- [redundant-rules.md](redundant-rules.md): rules that another analyzer also covers
