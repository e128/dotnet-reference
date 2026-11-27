# Rule Details: Candidates Batch
*Updated: 2026-11-27T16:16:52Z*

The 2026-11-27 batch ships eight rules from the analyzer candidate register and
closes one detection gap in E128033. The package README carries the rule text.
This file records the detection shape, the exclusions, and the fix status.

## Rules

| ID      | Name                              | Category        | Severity | Fix |
| ------- | --------------------------------- | --------------- | -------- | --- |
| E128256 | log-path-leak                     | E128-Security   | Warning  | Yes |
| E128257 | options-property-not-public       | E128-Design     | Warning  | No  |
| E128258 | service-lookup-without-key        | E128-Reliability| Warning  | No  |
| E128259 | plural-twin-type-name             | E128-Design     | Info     | No  |
| E128260 | retained-memory-after-dispose     | E128-Reliability| Warning  | No  |
| E128261 | padded-struct-marshal-copy        | E128-Unsafe     | Warning  | No  |
| E128262 | http-context-accessor-in-service  | E128-Design     | Info     | No  |
| E128263 | path-containment-prefix           | E128-Security   | Warning  | Yes |

## E128256: Path In A Logger Call

The rule registers on an invocation. It resolves `ILogger` and
`LoggerExtensions`. It recognizes a `[LoggerMessage]` partial method by its
attribute.

It scans the invocation syntax for two shapes:

- a member access named `FullName` whose symbol is a `FileSystemInfo` property.
- an invocation of `Path.GetFullPath`.

It reports each hit. The fix changes `.FullName` to `.Name`. The
`Path.GetFullPath` form has no fix.

An exception message is exempt. A file name is exempt. A test compilation is
skipped through `XunitTestHelper.IsTestCompilation`.

## E128257: Non-Public Options Property

The rule resolves `OptionsBuilder<T>` and
`OptionsConfigurationServiceCollectionExtensions`. It finds the options type in
three registration forms:

- `AddOptions<T>().Bind(...)`
- `AddOptions<T>().BindConfiguration(...)`
- `services.Configure<T>(...)`

It reports each non-public property with a setter on the options type. It
reports one diagnostic per options type, so several registrations do not
duplicate. No fix, because a private property can become public by intent only.

## E128258: Service Lookup Without A Key

The rule recognizes `Enumerable.First` and `Enumerable.Single`. The receiver
must be a `GetServices<T>()` call on
`ServiceProviderServiceExtensions`. It reports the throwing call and names the
`OrDefault` replacement. `FirstOrDefault` and `SingleOrDefault` stay silent.
No fix.

## E128259: Plural Twin Type Name

The rule runs as a compilation action. It walks every namespace and compares
each type name to its siblings. It reports a type whose name equals a sibling
name plus `s` or `es`, when the type is not an `IEnumerable`. The diagnostic
carries the `CompilationEnd` tag. No fix, because a rename spans the solution.

## E128260: Retained Memory After Dispose

The rule registers on a `using` statement. It resolves `IMemoryOwner<T>` and
`MemoryManager<T>`. It reads the owner from the using declaration or
expression.

It finds a local declared before the using and assigned inside it from
`owner.Memory` or `owner.Memory.Slice(...)`. It reports the first read of that
local after the using statement ends. A read inside the using scope stays
silent. A non-owner disposable stays silent. No fix.

The narrow form misses an alias stored in a field.

## E128261: Padded Struct Marshal Copy

The rule recognizes six marshal calls: `MemoryMarshal.AsBytes`,
`MemoryMarshal.Cast`, `Unsafe.As`, `Unsafe.Copy`, `Buffer.BlockCopy`, and
`Marshal.StructureToPtr`. It resolves the source struct type.

It computes padding for `LayoutKind.Explicit` from the `[FieldOffset]` values
that declare the layout and the declared `Size`. A gap between fields and a
trailing declared size both count. It reports the call with the padding count.

The rule reads primitive and enum fields only. A sequential layout, a nested
struct field, a pointer field, and a `bool` field stay silent. No fix.

## E128262: IHttpContextAccessor In A Service

The rule registers on a constructor. It reports an `IHttpContextAccessor`
parameter when the containing type is outside the allowlist. The allowlist
holds four shapes:

- a type that derives from `ComponentBase`
- a type that implements `IMiddleware`, `IHostedService`, or an MVC filter
- a type with `Invoke` or `InvokeAsync` that takes an `HttpContext`
- any type when the accessor type is absent from the compilation

No fix. Default severity is Info.

## E128263: Containment Test Without A Separator

The rule registers on a `StartsWith` invocation whose receiver is `string`. It
resolves the receiver in three forms: a `Path.GetFullPath` call, a
`FileSystemInfo.FullName` access, or a local initialized from either.

It reports when the root argument carries no trailing separator. It stays
silent in four cases:

- the argument is a `+` expression with `Path.DirectorySeparatorChar` or
  `Path.AltDirectorySeparatorChar`
- the argument is a constant that ends with `/` or `\`
- a local root was declared or assigned earlier with a separator append
- the receiver is not a resolved path

The fix wraps the root with `Path.TrimEndingDirectorySeparator(root) +
Path.DirectorySeparatorChar` and adds `using System.IO;`. It is absent when the
target framework has no `Path.TrimEndingDirectorySeparator`.

## E128033 Gap Closed

`OptionsBindInitAnalyzer` matched `OptionsBuilder<T>.Bind` by the containing
type of the method. The real `Bind` is an extension method, so the check
returned null and the rule never fired on the canonical call. The fix resolves
the receiver type instead. It also accepts `BindConfiguration`. Two tests use a
fake extension class in `Microsoft.Extensions.DependencyInjection`.

## Related

- [summary.md](summary.md): analyzer domain snapshot
- [rule-details.md](rule-details.md): the earlier per-rule detection shapes
- `lode/dotnet/analyzer-candidates/pugworks-review-sweep.md`: the source read
