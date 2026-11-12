# Reliability Rule Details: Resources And Lifetime
*Updated: 2026-11-12T14:16:45Z*

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

### E128181: `HttpClient.Timeout` set to an infinite value

Severity: **Warning**. Flags an assignment of `Timeout.InfiniteTimeSpan` or
`TimeSpan.FromMilliseconds(-1)` to `HttpClient.Timeout`. A stalled server then
holds the caller and its thread pool slot. `TimeSpan.MaxValue` is not reported,
because the setter throws `ArgumentOutOfRangeException` for any value above
`int.MaxValue` milliseconds.

The rule reads the left side as the `Timeout` property declared on exactly
`System.Net.Http.HttpClient`, so a user type with its own `Timeout` is silent.
The right side must be one of the named forms. A `FromMilliseconds` argument is
read with `GetConstantValue`, and only a constant `-1` reports. A `HasValue`
pattern guards the constant read, so a non-constant argument cannot throw an
analyzer exception. A computed value is skipped, because the rule cannot prove
it is infinite.

One assignment action covers an object initializer, a plain property assignment,
and a lambda body.

No code fix. A safe timeout value is a policy choice.

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

## Related

- [rule-details-reliability.md](rule-details-reliability.md): the other
  reliability rules
- [rule-details-reliability-concurrency.md](rule-details-reliability-concurrency.md):
  the async and concurrent rules
- [rule-details.md](rule-details.md): the remaining rule details
