# Shared Analyzer Helpers

*Updated: 2026-11-12T15:07:30Z*

`src/E128.Analyzers` and `src/E128.Analyzers.CodeFixers` keep one class per
shared concern. A new rule or fix calls an existing helper before it writes a
local copy. The analyzer assembly holds no Workspaces reference. The fixer
assembly reaches the analyzer internals through `InternalsVisibleTo`.

## Analyzer Helpers

| Helper | Contract |
| --- | --- |
| `MetadataTypes.Unsafe` | The single owner of the `System.Runtime.CompilerServices.Unsafe` metadata name. |
| `MetadataTypes.Find` | Returns every type with the metadata name. Survives an ambiguous reference. |
| `MetadataTypes.Contains` | True when a symbol is one of the resolved types. |
| `MetadataTypes.RegisterForType` | Registers one syntax action per type that carries the metadata name. An ambiguous name registers every match, so the rule still runs. A bound symbol matches one type, so a report cannot duplicate. |
| `OptionReader.ReadInt` | Reads one bounded integer config option. A missing, malformed, or below-minimum value yields the fallback. |
| `TypeSymbols.UnwrapNullable` | Returns `T` for a `Nullable<T>` argument. |
| `TypeSymbols.DerivesFrom` | Walks the base chain. A null base type matches nothing. |
| `TypeSymbols.Matches` | True when a type equals a target. A null target matches nothing. |
| `TypeSymbols.MatchesOriginalDefinition` | True when the original definition of a type equals a target definition. |
| `InterfaceMembers.Implements` | True when a method implements an interface member, including an explicit implementation. An interface or delegate method returns false. |
| `InterfaceMembers.ImplementsMember` | True when the containing type maps some interface member to the method. |
| `NameSuffixes.Matches` | True when a name ends with a suffix at an uppercase-letter position. |
| `ByteSizeType.Is` | True for `Pug.Core.Classes.ByteSize`. |
| `ReturnTypes.Get` | Returns the return type of a method or the type of a property. |
| `ReadOnlyCollectionExposure.Find` | Takes an operation and its containing symbol, not a diagnostic context. Returns the location of a mutable `List` or `Dictionary` field or property that a member exposes as a read-only interface. Unwraps `Task<T>` and `ValueTask<T>`. `MutableCollectionKind` selects the collection. |
| `MutableCollectionCatalog` | One list of mutable collection types, their kind, the read-only interface name that replaces each, and the display template. The exposure rule and the read-only return rules both read it. |
| `PragmaWarningDirectives.Warning` | Enumerates each `#pragma warning` directive with its trivia. `IsDisable` classifies it. The suppression rules share it. |
| `PathNamePatterns` | One path-name classifier. `IsPathName` gates a parameter name, `IsPathCliName` adds the CLI terms, and `Classify` returns `File`, `Directory`, or `Ambiguous`. |
| `DiskIoCatalog.FileMethodKinds` | One map from a `System.IO.File` method name to its `IoKind`. The write, read, and factory predicates derive from it. |
| `TaskTypes.IsTaskOrValueTask` | True for `Task` and `ValueTask`, including their generic forms. |
| `Blittability.IsBlittable` | True when a type copies as a raw bit pattern. A struct that holds a bool or decimal returns false. |
| `TrackerReference` | Reads `e128_tracked_ref_pattern` and matches a work-item reference. A regex timeout counts as no match, so the rule still reports. |
| `InvocationNames.GetSimpleName` | Returns the called name for `Type.Method(...)` and for a bare `Method(...)`. |
| `PartialMembers.IsImplementationPart` | True when a symbol is the implementing source symbol of a partial method, partial property, or a parameter of one. A member-level symbol rule calls it first and skips the event, so one logical member reports once. |
| `IndexedForLoop.TryMatch` | Matches `for (var i = 0; i < source.Length; i++)` and returns the variable, bound owner, and body. |
| `XunitTestHelper.IsAssertType` | True for `Xunit.Assert`. The test-quality rules share it. |
| `Unsafe.OwnerInvocation.TryMatch` | Binds a named call and confirms its containing type is one of the owner types. |
| `Namespaces.Equals` | True when the namespace chain equals a dotted name, ending at the global namespace. |
| `AttributeArguments.IsInside` | True when the node or an ancestor is an attribute argument. |
| `TaskWhenAllCalls.IsWhenAll` | True for a bound `Task.WhenAll` call on the given task type. |
| `InitializerAssignments.Assigns` | True when an object initializer assigns the named property. `GetAssignedName` reads the left side. |
| `ExpressionUnwrapper.Parentheses` | Strips enclosing parentheses. |
| `ExpressionUnwrapper.ParenthesesAndCasts` | Strips parentheses and explicit casts. |
| `ExpressionUnwrapper.Receiver` | Returns the receiver of a member-access invocation. |
| `LoopContext.FindEnclosingLoopBody` | Returns the loop body that runs once per iteration. A loop header, a lambda, or a local function returns null. |
| `LoopContext.FindEnclosingLoop` | Returns the enclosing loop statement. |
| `LoopContext.IsInsideLoopBody` | True when the node runs once per iteration. A `for` initializer or a `foreach` source does not count. A `for` condition and incrementor do count. |
| `LoopContext.IsWithinAnyLoop` | True for any enclosing loop, including a header. Stops at a method, constructor, accessor, operator, or local function. |
| `LoopContext.HasConstantBound` | True when the nearest `for` loop has a numeric literal bound. |
| `LoopContext.ReferencesLoopVariable` | True when the node reads a value that changes per iteration. |
| `LoopContext.ContainsWaitCall` | True when the loop body waits between iterations. |
| `FileReadMethods.Contains` | True when a `System.IO.File` member reads a file. The TOCTOU and process-output rules share the set. |
| `EnclosingBody`, `StatementExits`, `ExclusiveBranches`, `DisposeCalls`, `LoopCount`, `InModifierHelper` | Body, control-flow, disposal, count, and modifier queries. |

## Code Fix Helpers

| Helper | Contract |
| --- | --- |
| `E128CodeFixProviderBase` | Owns the batch fix-all provider and the root fetch. Override `RegisterFixesAsync` for the normal shape. |
| `CodeFixRegistration.Register` | Registers an action with its mandatory equivalence key. One overload takes a changed document, the other a changed solution. |
| `UsingDirectives.AddIfMissing` | Adds a top-level using in sorted position, or returns the root unchanged. |
| `UsingDirectives.EnsureAddedAsync` | Async form. Fetches the root and applies `AddIfMissing`. |
| `FixSyntax.InsertFieldAsFirstMember` | Inserts a field at the head of a type with the indent of the following member and one blank line. |
| `FixSyntax.ReplaceStatementFromText` | Parses replacement statement text and keeps the original trivia. |
| `FixSyntax.BuildFieldName` | Builds a `_lowerSuffix` field name from receiver text, with a fallback when no letter remains. |
| `FixSyntax.HasMemberNamed` | True when a field, and optionally a property or method, has the name. |
| `FixSyntax.PromoteContainingMethod` | Adds `async` to the method that holds an await and promotes its return type to `Task` or `Task<T>`. |
| `FixSyntax.ReplaceLoopAsync` | Replaces the loop and adds the namespace that the replacement needs. |
| `FixSyntax.WrapInTryCatchAsync` | Wraps a statement in a try block with one typed catch clause and a catch body. |
| `PragmaDirectives.Build` | Builds a pragma warning directive line for a keyword and an error-code list. |
| `FixSyntax.LastIndentation` | Returns the last whitespace trivia anywhere in the list, or an empty string. |
| `FixSyntax.TrailingWhitespace` | Returns the last trivia when it is whitespace, or an empty list. |
| `FixSyntax.ParenthesizeIfNeeded` | Parenthesizes a non-primary expression. |
| `FixSyntax.IndexInto` | Builds an element access on a target that `ParenthesizeIfNeeded` guards. |
| `FixSyntax.MemberIndentation` | Returns the member indent, or four spaces. |
| `FixSyntax.LineBreak` | Returns the end-of-line trivia of the member, or `\n`. |
| `ReadOnlyCollectionFix.AppendAsReadOnlyAsync` | Appends `.AsReadOnly()` to a returned collection and keeps its trivia. |
| `DiRegistrationFix.RewriteToGenericAsync` | Rewrites a DI registration to the two-type-argument overload. A null method name keeps the existing name. |
| `RegexLiteralFix.ApplyRewriteAsync` | Applies a pattern rewrite to a `GeneratedRegex` literal and re-emits it with quotes and escapes. |

## Invariants

- One helper name has one contract. Do not reuse a helper whose boundary differs.
  `FindEnclosingLoopBody` treats a `for` condition as a header.
  `IsInsideLoopBody` treats it as body work. Pick by the question, not by shape.
- A helper stays `internal static`. Never widen a public API to share code.
- A helper reads the same table as the analyzer. A private copy drifts in silence.
- A fix helper returns a changed document or declines at registration. It never
  offers an action that leaves the document unchanged.

## Related

- [Summary](summary.md): reading order and shared invariants
- [Code Fix Patterns](code-fix-patterns.md): decision list, tables, fix-all
- [Code Fix Techniques](code-fix-techniques.md): edit APIs
- [Analyzer Performance](performance.md): cost rules
