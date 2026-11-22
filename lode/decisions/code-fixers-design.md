# Code Fixer Decisions: Design
*Updated: 2026-11-22T04:00:12Z*

Rationale removed from source comments. Each section names the type that carried it.
Read this page before you change or delete the matching check.

## AsyncDisposableWithoutDisposableCodeFixProvider
- Skips the generated Dispose method when the type already declares a parameterless Dispose. A type satisfies IDisposable through that member, and a second method with the same signature is CS0111.

## AsyncVoidCodeFixProvider
- Declines a method that overrides a base member or implements an interface explicitly. The inherited void return type cannot change in this declaration. As a result, the rewrite would produce CS0508 or CS0738.

## ByteSizeForDataSizeCodeFixProvider
- Offers no fix for a parameter default. A default value must be a compile-time constant, ByteSize.FromBytes is a method call, and a rewritten default gives CS1736.
- Resolves the ByteSize type from the compilation instead of hardcoding a namespace. The type has moved namespaces before, and a hardcoded name would stop compiling after another move.
- Wraps every initializer in a multi-declarator declaration. All declarators share the new type. As a result, an unwrapped initializer would assign an int to ByteSize.

## ConcreteOnlyDiRegistrationCodeFixProvider
- Declines a registration reached through `using static`. An unqualified call has no receiver to rewrite. As a result, a registered action would change nothing.
- Keeps a two-type-argument registration that passes a factory argument. A factory that builds from an argument produces something the container cannot reproduce from the type alone.

## DictionaryAsReadOnlyCodeFixProvider
- Expects the diagnostic on the returned expression, not on the return statement. The analyzer reports the expression, so FindNode returns the identifier or creation that the fix rewrites.

## DirectHttpClientInstantiationCodeFixProvider
- Leaves an object initializer untouched. The replacement is a factory call, and BaseAddress or Timeout set in the initializer would be dropped silently.
- Needs an instance context. The fix adds an instance field and a constructor parameter, which a static member cannot hold and a struct or record cannot take.

## EnumIfElseChainCodeFixProvider
- Maps only an equality chain to case labels. An inequality chain would invert the logic when turned into a switch.
- Appends a break only to a section that does not already leave the switch. A break after a return or throw is unreachable code, which CS0162 reports.
- Takes the operand that is not the enum constant as the switch subject. Taking the left operand by position puts the constant in the subject of a reversed comparison, and every case label becomes unreachable.
- Identifies the enum member by its constant value, not by its type. A variable and a member share the enum type, and only the member is a compile-time constant.

## ExecuteScalarNullGuardCodeFixProvider
- Requires an enclosing statement. The rewrite declares a local beside the statement that reads it, and a field initializer gives that local no scope.
- Wraps a braceless embedded statement in a block. The generated local needs that block. This holds because returning the rewritten tree alone would leave the name undefined.

## FileSystemInfoEqualityCodeFixProvider
- Requires both operands to be a FileSystemInfo. The analyzer reports a comparison where either operand is one, but `.FullName` exists only when both are.

## ListAsReadOnlyCodeFixProvider
- Expects the diagnostic on the returned expression, not on the enclosing return or arrow clause. FindNode therefore returns the expression that the fix wraps.

## PrimaryConstructorBackingFieldCodeFixProvider
- Selects the field declaration and its references from tracked syntax nodes, not from a name search. Two types in one file can declare the same field name, and only the tracked nodes identify the right one.

## StaticNumericIncrementCodeFixProvider
- Rewrites only an increment in statement position. Interlocked returns the new value, which statement position discards, while a value position such as `var v = counter++` would change meaning.
- Declines to remove `static` when the mutation site is in a static member of a static class. An instance field is unreachable there. As a result, the removal would be worse than the original code.

## SyncOverAsyncCodeFixProvider
- Inserts an await only in an ordinary method. A lambda, local function, or property accessor cannot take the async modifier from this fix, and an await there does not compile.

## TaskRunAsyncLambdaCodeFixProvider
- Rewrites only a single-argument Task.Run call. A second argument such as a cancellation token would be dropped, and the work would stop observing cancellation.
- Inlines a block body only when the call is the awaited expression of a statement. A block that returns or yields stays untouched, because inlining would change the control flow.
- Declines a block-local name that resolves in the enclosing scope. The inlined block would collide with that symbol.

## TimeSpanForDurationCodeFixProvider
- Offers no fix for a parameter default. An int literal is not a TimeSpan constant. As a result, the rewrite gives CS1750, and `default` would change the default from seconds to zero.
- Declines a property initializer. The initializer is a number, and changing the property type alone leaves `= 5000` next to TimeSpan, which does not compile.

## UnionSwitchDiscardArmCodeFixProvider
- Declines a `var` arm and a `when`-guarded arm. A var arm binds a value, and a guarded arm would keep its guard on the null arm. As a result, the rewrite would break the arm expression.

## UnusedInterfaceParamCodeFixProvider
- Applies renames in sequence through SequentialRenameFixAllProvider. A batch fixer loses one of two edits in the same document when a rename touches the whole solution.
- Skips an underscore-prefixed name in the single-fix path. The fix-all path skips that name too. As a result, a single fix must not offer a rename to the same text.

Related: [Decision Records](summary.md) explains how to use this page.
