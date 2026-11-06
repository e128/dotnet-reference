# Reliability Rule Details: Resources And Lifetime
*Updated: 2026-11-06T13:02:22Z*

Behavior notes for the reliability rules that cover resource lifetime and shared
state. The complete rule table, the severity, and the code-fix status live in
`src/E128.Analyzers/README.md`. The other reliability rules live in
[rule-details-reliability.md](rule-details-reliability.md).

### E128192: `GetBuffer` on a hidden `MemoryStream` buffer

Severity: **Warning**. Flags a `MemoryStream.GetBuffer()` call whose receiver
was created from a `byte[]` without `publiclyVisible: true`. The call throws
`UnauthorizedAccessException` for such a stream.

The rule follows a local to its creation in the enclosing body. It follows a
parameter to an assignment in an enclosing method body. It maps arguments to
constructor parameters, so positional and named forms both bind.

**Exclusions**:

- A creation with `publiclyVisible: true`.
- A creation with no `byte[]` argument, such as `new MemoryStream()` or
  `new MemoryStream(16)`. Those constructors expose the buffer.
- A non-constant `publiclyVisible` value.
- A receiver that is a field, a property, or a method result.
- A creation in a field initializer or in another method.
- A receiver type other than `MemoryStream`.

No code fix. `ToArray()` copies the visible length, and the caller may want the
raw buffer.

### E128193: Disposable field overwritten in a loop

Severity: **Warning**. Flags an assignment to a disposable field inside a loop
body when no earlier dispose call on the same field sits in that loop. The
overwritten instance keeps its unmanaged resources until the finalizer runs.

The rule resolves `IDisposable` and `IAsyncDisposable`, then matches a simple
assignment whose left symbol is a field of either type. It compares the dispose
receiver to the field symbol and the spans to the assignment. The suppression
requires a dispose inside the same loop. A dispose before the loop releases only
the first value, so it does not clear the report.

**Exclusions**:

- A dispose call on the same field before the assignment, inside the same loop,
  including `?.` and `this.`.
- A local, a parameter, or a property on the left side.
- A compound assignment.
- A value type or a non-disposable type.
- An assignment outside a loop.
- An assignment in a lambda or a local function nested in a loop.

No code fix. The dispose site depends on the ownership rule.

### E128194: `Lazy<T>` field without thread safety

Severity: **Warning**. Flags a `Lazy<T>` field whose initializer passes
`LazyThreadSafetyMode.None`. Two threads can then build the value twice, and
each caller gets a different instance.

The rule reads a field declarator only. It requires the field type and the
constructor to belong to the BCL `Lazy<T>`. It reads the mode from the
constructor parameter, so positional and named forms both bind.

**Exclusions**:

- An omitted mode, because the default is `ExecutionAndPublication`.
- `ExecutionAndPublication` and `PublicationOnly`.
- A non-constant mode.
- A local, a parameter, or a property initializer.
- A factory method initializer.
- A user type that shadows `Lazy<T>`.

No code fix. The mode is a caller policy.

### E128196: `JsonSerializerOptions` mutated after first use

Severity: **Warning**. Flags a write to a `JsonSerializerOptions` member after the
same body passes the instance to `JsonSerializer`. The serializer freezes the
instance on first use, so the write throws `InvalidOperationException`.

The write shapes are a member assignment and a call on the `Converters` list.
The rule reads the receiver symbol as a local, a parameter, or a field.

**Exclusions**:

- A property receiver, because the getter can return a new instance.
- A write before the first serializer call.
- A write to an instance that the body never passes to the serializer.
- A use in one body and a write in another body.
- A serializer call inside a lambda or a local function.
- A serializer call in the other branch of the same `if` as the write.
- A new instance assigned to the same variable between the call and the write.
- A user type with the same member names, or a serializer type outside
  `System.Text.Json`.

No code fix. The order of the setup calls belongs to the caller.

## Related

- [rule-details-reliability.md](rule-details-reliability.md): the other
  reliability rules
- [rule-details-reliability-concurrency.md](rule-details-reliability-concurrency.md):
  the async and concurrent rules
- [rule-details.md](rule-details.md): the remaining rule details
