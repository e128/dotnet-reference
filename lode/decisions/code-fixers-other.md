# Code Fixer Decisions: Testing and Security
*Updated: 2026-11-20T17:16:51Z*

Rationale removed from source comments. Each section names the type that carried it.
Read this page before you change or delete the matching check.

## FileSystemPathCodeFixProvider
- Offers a parameter fix only for an ambiguous name-pattern diagnostic. A use-site parameter diagnostic depends on the parameter being a string, and changing the type would break the method body.

## InsecureRandomInCryptoContextCodeFixProvider
- Binds the replacement before registering it. A shadowing RandomNumberGenerator type, or a missing GetInt32 overload, then means no fix instead of code that does not compile.
- Checks that the plain name does not bind to a shadowing source type. The emitted replacement resolves only after the using directive lands.
- Probes the global-qualified form for API verification. That binding ignores the missing using directive.
- Binds the type name alone. Binding the whole invocation yields the method symbol, never the type.
- Requires a non-empty range. Next(max) accepts 0, and GetInt32(0) throws.

## ProcessStartInfoArgumentCodeFixProvider
- Rewrites only an assignment inside an object initializer. The statement form `psi.Arguments = value;` has no equivalent member-initializer rewrite.

## LowValueTestCodeFixProvider
- Anchors on the test method. The rule reports the method name. As a result, a diagnostic on any other node, or on a method without an xUnit attribute, registers no action.
- Cleans the blank line left when the last member is removed. It keeps the closing brace indentation and leaves the trivia alone when it holds a comment or a directive.

## MissingTraitCategoryCodeFixProvider
- Copies only the indentation from the method's leading trivia. That trivia also holds the doc comment, and copying all of it would duplicate the comment.

## TempDirCleanupCodeFixProvider
- Adds an interface member only when the type does not declare it. A second method with the same signature is CS0111.
- Keeps the formatter annotation on the inserted members. A fix must not reformat members the caller already wrote.

## TestCodeCommentCodeFixProvider
- Looks for the comment in the leading and then the trailing trivia list. A comment on its own line sits in the leading list, and one after code on the same line sits in the trailing list.

## FixedBufferFieldCodeFixProvider
- Offers the fix only for a field that nothing reads or writes. A fixed buffer is pointer-like, and any use of it may stop compiling.
- Nests the buffer struct in the containing type. The name only has to be unique there, and the struct takes the field's accessibility.

## SkipInitCodeFixProvider
- Spells out the type argument for a `var` declaration. `var x = default;` does not compile, which is CS0815.

## UnsafeUnalignedNonBlittableCodeFixProvider
- Requires a ref, in, or out argument. ReadUnaligned takes a byref.

Related: [Decision Records](summary.md) explains how to use this page.
