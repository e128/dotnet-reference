# C# Analyzer Rule Catalog and Fixes

This reference provides comprehensive information about common C# analyzer rules (CA, IDE, CS) that are often suppressed, what they check for, why they matter, and how to fix violations instead of suppressing them.

## General Principle

**Default stance: Fix the root cause, don't suppress.**

Suppressions should be rare exceptions, not the default approach. Before suppressing:
1. Understand what the rule is checking for
2. Try to fix the underlying issue
3. If a fix isn't possible, document why in a comment above the pragma
4. Get human approval before keeping suppressions

## When Suppressions Are Acceptable

- **Test code conventions**: Tests may intentionally violate certain rules (e.g., CA1707 for test naming)
- **Generated code**: Code from third-party generators where modification isn't practical
- **Third-party compatibility**: When matching external API contracts requires violations
- **False positives**: When the analyzer is provably wrong (document why)

## Code Analysis Rules (CA)

### Design Rules (CA1000-CA1999)

#### CA1000: Do not declare static members on generic types
**Why**: Static members on generic types can be confusing because they're shared across all instances of that generic type.
**Fix**: Move static members to a non-generic helper class or make them instance members.

#### CA1001: Types that own disposable fields should be disposable
**Why**: Prevents resource leaks by ensuring cleanup of unmanaged resources.
**Fix**: Implement IDisposable and dispose of owned disposable fields.

#### CA1002: Do not expose generic lists
**Why**: List<T> is a concrete implementation; exposing IList<T> or IReadOnlyList<T> is more flexible.
**Fix**: Change property/parameter types from List<T> to IList<T>, IReadOnlyList<T>, or ICollection<T>.

#### CA1008: Enums should have zero value
**Why**: Default value of enum types is 0; having a 0 value makes the default meaningful.
**Fix**: Add a None = 0 member to the enum, or use [Flags] if it's a bitfield.

#### CA1012: Abstract types should not have public constructors
**Why**: Abstract types cannot be instantiated, so public constructors are misleading.
**Fix**: Make constructors protected instead of public.

#### CA1014: Mark assemblies with CLSCompliantAttribute
**Why**: Indicates whether the assembly is compliant with the Common Language Specification.
**Fix**: Add `[assembly: CLSCompliant(true)]` to AssemblyInfo.cs or suppress if CLS compliance isn't a goal.

#### CA1016: Mark assemblies with AssemblyVersionAttribute
**Why**: Assemblies should have version information for dependency management.
**Fix**: Add version information to .csproj or AssemblyInfo.cs. Modern SDK-style projects auto-generate this.

#### CA1031: Do not catch general exception types
**Why**: Catching `Exception` or `SystemException` can hide bugs and make debugging difficult.
**Fix**: Catch specific exception types you can handle. If you must catch all, document why and consider re-throwing.

#### CA1032: Implement standard exception constructors
**Why**: Exceptions should have consistent constructor patterns for compatibility with serialization and other frameworks.
**Fix**: Add the three standard constructors:
```csharp
public MyException() { }
public MyException(string message) : base(message) { }
public MyException(string message, Exception innerException) : base(message, innerException) { }
```

#### CA1034: Nested types should not be visible
**Why**: Public nested types can be confusing and hard to discover.
**Fix**: Move nested types to separate files or make them private/internal.

#### CA1040: Avoid empty interfaces
**Why**: Empty interfaces provide no contract; use attributes instead.
**Fix**: Add members to the interface or remove it if it's just a marker. Use attributes for marker semantics.

#### CA1041: Provide ObsoleteAttribute message
**Why**: Helps developers understand what to use instead.
**Fix**: Add a message to [Obsolete]: `[Obsolete("Use NewMethod instead")]`

#### CA1044: Properties should not be write only
**Why**: Properties should be readable; write-only is unexpected and limits usability.
**Fix**: Add a getter or convert to a method like SetX().

#### CA1050: Declare types in namespaces
**Why**: Types in the global namespace pollute the global space and risk naming collisions.
**Fix**: Move the type into an appropriate namespace.

#### CA1051: Do not declare visible instance fields
**Why**: Fields expose implementation details; properties provide encapsulation.
**Fix**: Convert public/protected fields to properties.

#### CA1052: Static holder types should be sealed and static
**Why**: Classes containing only static members shouldn't be instantiable.
**Fix**: Mark the class as `static` (C# 2.0+) or `sealed` with a private constructor.

#### CA1054, CA1055, CA1056: URI parameters/properties/return values should be System.Uri
**Why**: System.Uri provides validation, parsing, and manipulation capabilities that strings lack.
**Fix**: Change string parameters to `Uri` type. Use `Uri.TryCreate()` for validation.

#### CA1062: Validate arguments of public methods
**Why**: Prevents NullReferenceException by validating inputs at the boundary.
**Fix**: Add null checks: `ArgumentNullException.ThrowIfNull(parameter)` or use nullable reference types.

#### CA1063: Implement IDisposable correctly
**Why**: Ensures resources are cleaned up properly and follows the dispose pattern.
**Fix**: Follow the standard dispose pattern with protected Dispose(bool disposing) method.

#### CA1064: Exceptions should be public
**Why**: Exceptions that cross assembly boundaries need to be catchable by callers.
**Fix**: Make exception classes public.

#### CA1065: Do not raise exceptions in unexpected locations
**Why**: Certain methods (property getters, Equals, GetHashCode, static constructors) shouldn't throw.
**Fix**: Return default values or refactor to a method that can throw.

#### CA1066: Type should implement IEquatable<T>
**Why**: Provides type-safe equality comparison without boxing.
**Fix**: Implement `IEquatable<T>` when overriding Equals.

#### CA1067, CA1068: Override Equals/GetHashCode when implementing IEquatable
**Why**: Ensures consistency between equality comparison methods.
**Fix**: Override Equals and GetHashCode when implementing IEquatable<T>.

#### CA1069: Enums should not have duplicate values
**Why**: Multiple names for the same value is confusing except in [Flags] enums.
**Fix**: Remove duplicate values or add [Flags] if it's a bitfield.

#### CA1070: Do not declare event fields as virtual
**Why**: Event field declarations generate both add/remove methods; virtual events need explicit add/remove accessors.
**Fix**: Declare event with explicit add/remove accessors or make it non-virtual.

### Globalization Rules (CA2000-CA2999)

#### CA2000: Dispose objects before losing scope
**Why**: Prevents resource leaks of unmanaged resources.
**Fix**: Use `using` statements or ensure Dispose is called in finally blocks.

#### CA2002: Do not lock on objects with weak identity
**Why**: Objects with weak identity (strings, Type objects) can be shared across app domains, causing deadlocks.
**Fix**: Lock on a private readonly object: `private readonly object _lock = new();`

#### CA2007: Do not directly await a Task
**Why**: Can cause deadlocks in UI contexts where SynchronizationContext matters.
**Fix**: Use `.ConfigureAwait(false)` in library code, or suppress in app code where context is needed.

#### CA2008: Do not create tasks without passing a TaskScheduler
**Why**: Explicit TaskScheduler specification avoids unexpected scheduling behavior.
**Fix**: Pass TaskScheduler.Default or TaskScheduler.Current explicitly, or use Task.Run().

#### CA2009: Do not call ToImmutableCollection on an ImmutableCollection value
**Why**: Redundant and creates unnecessary allocations.
**Fix**: Remove the ToImmutableX call if the value is already immutable.

#### CA2011: Do not assign property within its setter
**Why**: Causes infinite recursion.
**Fix**: Assign to the backing field instead: `_field = value;`

#### CA2012: Use ValueTasks correctly
**Why**: ValueTask has specific usage constraints; misuse can cause bugs.
**Fix**: Only await ValueTask once, don't store for later use, and prefer Task for complex scenarios.

#### CA2014: Do not use stackalloc in loops
**Why**: Can cause stack overflow since stack space isn't reclaimed until method returns.
**Fix**: Move stackalloc outside the loop or use heap allocation.

#### CA2015: Do not define finalizers for types derived from MemoryManager<T>
**Why**: MemoryManager has its own finalization logic.
**Fix**: Remove the finalizer.

#### CA2016: Forward CancellationToken to methods that take one
**Why**: Enables cooperative cancellation throughout the call chain.
**Fix**: Pass the CancellationToken parameter to async methods that accept one.

#### CA2201: Do not raise reserved exception types
**Why**: Types like Exception, SystemException, ApplicationException are too generic.
**Fix**: Throw specific exception types (ArgumentNullException, InvalidOperationException, etc.).

#### CA2207: Initialize value type static fields inline
**Why**: Static constructors on value types have performance implications.
**Fix**: Initialize static fields inline: `private static int _count = 0;`

#### CA2208: Instantiate argument exceptions correctly
**Why**: ArgumentException's paramName should match the actual parameter name.
**Fix**: Use `nameof(parameter)` for the paramName argument.

#### CA2213: Disposable fields should be disposed
**Why**: Prevents resource leaks.
**Fix**: Dispose fields in the Dispose method.

#### CA2214: Do not call overridable methods in constructors
**Why**: Virtual methods can be called before derived class constructor runs, causing subtle bugs.
**Fix**: Make the method non-virtual, or call it after construction.

#### CA2215: Dispose methods should call base class dispose
**Why**: Ensures the full dispose chain is executed.
**Fix**: Call `base.Dispose(disposing)` in Dispose method.

#### CA2216: Disposable types should declare finalizer
**Why**: Types with native resources should have finalizers as a safety net.
**Fix**: Add finalizer if holding native resources, or suppress if only holding managed resources.

#### CA2225: Operator overloads have named alternates
**Why**: Languages without operator overloading need named methods.
**Fix**: Provide named methods (e.g., `Add()` for `operator +`).

#### CA2227: Collection properties should be read only
**Why**: Replacing a collection breaks change notification; mutate the collection instead.
**Fix**: Remove the setter and initialize the collection in the constructor.

#### CA2229: Implement serialization constructors
**Why**: Required for types marked [Serializable].
**Fix**: Add protected constructor: `protected MyType(SerializationInfo info, StreamingContext context)`

#### CA2231: Overload operator equals on overriding ValueType.Equals
**Why**: Value types should have consistent equality semantics.
**Fix**: Implement `operator ==` and `operator !=` when overriding Equals.

#### CA2234: Pass System.Uri objects instead of strings
**Why**: Uri provides better validation and manipulation.
**Fix**: Change method to accept Uri instead of string.

#### CA2237: Mark ISerializable types with SerializableAttribute
**Why**: Required for binary serialization.
**Fix**: Add `[Serializable]` attribute.

#### CA2241: Provide correct arguments to formatting methods
**Why**: Format string placeholders must match the number of arguments.
**Fix**: Ensure format string placeholders ({0}, {1}, etc.) match arguments.

#### CA2242: Test for NaN correctly
**Why**: NaN != NaN by IEEE 754 specification.
**Fix**: Use `double.IsNaN(value)` instead of `value == double.NaN`.

#### CA2243: Attribute string literals should parse correctly
**Why**: String literals in assembly attributes should be valid (e.g., valid versions).
**Fix**: Fix the string format in the attribute.

#### CA2244: Do not duplicate indexed element initializations
**Why**: Duplicate keys overwrite each other unexpectedly.
**Fix**: Remove duplicate initializations.

#### CA2245: Do not assign a property to itself
**Why**: Self-assignment is usually a typo.
**Fix**: Assign to the correct property or backing field.

#### CA2246: Do not assign a symbol and its member in the same statement
**Why**: Evaluation order can cause unexpected behavior.
**Fix**: Split into separate statements.

#### CA2247: Argument to TaskCompletionSource should be TaskCreationOptions
**Why**: TaskContinuationOptions is for continuations, not TCS.
**Fix**: Use TaskCreationOptions enum instead.

#### CA2248: Provide correct enum argument to Enum.HasFlag
**Why**: HasFlag should receive the same enum type.
**Fix**: Cast the argument to the correct enum type.

#### CA2249: Use String.Contains instead of String.IndexOf
**Why**: Contains is more readable and intention-revealing.
**Fix**: Replace `s.IndexOf("x") >= 0` with `s.Contains("x")`.

### Performance Rules (CA1800-CA1899)

#### CA1802: Use literals where appropriate
**Why**: Readonly fields with constant values should be const for better performance.
**Fix**: Change `private static readonly int X = 42;` to `private const int X = 42;`

#### CA1805: Do not initialize unnecessarily
**Why**: Value types are already zero-initialized.
**Fix**: Remove explicit initialization to default values.

#### CA1806: Do not ignore method results
**Why**: Calling a method without using its return value is usually a mistake.
**Fix**: Use the return value or remove the call if it has no side effects.

#### CA1810: Initialize reference type static fields inline
**Why**: Avoids the performance cost of explicit static constructors.
**Fix**: Initialize fields inline: `private static List<int> Items = new();`

#### CA1812: Avoid uninstantiated internal classes
**Why**: Dead code that should be removed or used.
**Fix**: Remove the class or add code that uses it.

#### CA1813: Avoid unsealed attributes
**Why**: Sealed attributes can be optimized by the runtime.
**Fix**: Mark attribute classes as sealed.

#### CA1814: Prefer jagged arrays over multidimensional
**Why**: Jagged arrays have better performance characteristics.
**Fix**: Replace `int[,]` with `int[][]`.

#### CA1815: Override equals and operator equals on value types
**Why**: Default Equals uses reflection; override for performance.
**Fix**: Implement Equals, GetHashCode, operator==, operator!=.

#### CA1816: Dispose methods should call SuppressFinalize
**Why**: Prevents unnecessary finalization when Dispose was called.
**Fix**: Call `GC.SuppressFinalize(this)` in Dispose.

#### CA1819: Properties should not return arrays
**Why**: Arrays are mutable and can be modified by callers; properties return the same reference.
**Fix**: Return ReadOnlySpan<T>, IReadOnlyList<T>, or a copy of the array.

#### CA1820: Test for empty strings using string length
**Why**: `s.Length == 0` is faster than `s == ""`.
**Fix**: Use `string.IsNullOrEmpty(s)` or `s.Length == 0`.

#### CA1821: Remove empty finalizers
**Why**: Empty finalizers cause unnecessary finalization overhead.
**Fix**: Remove the empty finalizer.

#### CA1822: Mark members as static
**Why**: Members that don't access instance state should be static.
**Fix**: Add static keyword to methods/properties that don't use `this`.

#### CA1823: Avoid unused private fields
**Why**: Dead code that should be removed.
**Fix**: Remove unused fields or use them.

#### CA1824: Mark assemblies with NeutralResourcesLanguageAttribute
**Why**: Improves satellite assembly probing performance.
**Fix**: Add `[assembly: NeutralResourcesLanguage("en-US")]` if appropriate.

#### CA1825: Avoid zero-length array allocations
**Why**: Use Array.Empty<T>() to avoid unnecessary allocations.
**Fix**: Replace `new T[0]` with `Array.Empty<T>()`.

#### CA1826: Use property instead of Linq Enumerable method
**Why**: Properties like Count are more efficient than Count().
**Fix**: Replace `list.Count()` with `list.Count` for collections.

#### CA1827: Do not use Count/LongCount when Any can be used
**Why**: Any() short-circuits and is more efficient for existence checks.
**Fix**: Replace `Count() > 0` with `Any()`.

#### CA1828: Do not use CountAsync/LongCountAsync when AnyAsync can be used
**Why**: Same as CA1827 but for async LINQ.
**Fix**: Replace `await CountAsync() > 0` with `await AnyAsync()`.

#### CA1829: Use Length/Count property instead of Enumerable.Count method
**Why**: Properties avoid LINQ overhead.
**Fix**: Use .Count or .Length property directly.

#### CA1830: Prefer strongly-typed Append and Insert method overloads on StringBuilder
**Why**: Avoids boxing of value types.
**Fix**: Use `sb.Append(123)` instead of `sb.Append(123.ToString())`.

#### CA1831, CA1832, CA1833: Use AsSpan instead of Range-based indexers
**Why**: AsSpan avoids allocations for substring operations.
**Fix**: Use `s.AsSpan(start, length)` instead of `s[start..end]`.

#### CA1834: Use StringBuilder.Append(char) for single characters
**Why**: Append(char) is more efficient than Append(string) for single characters.
**Fix**: Replace `sb.Append("x")` with `sb.Append('x')`.

#### CA1835: Prefer memory-based overloads of ReadAsync/WriteAsync
**Why**: Memory<T> based APIs avoid allocations.
**Fix**: Use Memory<byte> or ReadOnlyMemory<byte> overloads.

#### CA1836: Prefer IsEmpty over Count when available
**Why**: IsEmpty can be O(1) while Count might enumerate the whole collection.
**Fix**: Replace `Count == 0` with `IsEmpty` where available.

#### CA1837: Use Environment.ProcessId instead of Process.GetCurrentProcess().Id
**Why**: Environment.ProcessId is more efficient.
**Fix**: Replace `Process.GetCurrentProcess().Id` with `Environment.ProcessId`.

#### CA1838: Avoid StringBuilder parameters for P/Invokes
**Why**: StringBuilder for P/Invoke has unexpected semantics.
**Fix**: Use string or char[] with proper marshaling.

#### CA1839: Use Environment.ProcessPath instead of Process.GetCurrentProcess().MainModule.FileName
**Why**: Environment.ProcessPath is more efficient and reliable.
**Fix**: Replace `Process.GetCurrentProcess().MainModule.FileName` with `Environment.ProcessPath`.

#### CA1840: Use Environment.CurrentManagedThreadId instead of Thread.CurrentThread.ManagedThreadId
**Why**: More efficient property access.
**Fix**: Replace `Thread.CurrentThread.ManagedThreadId` with `Environment.CurrentManagedThreadId`.

#### CA1841: Prefer Dictionary.Contains methods
**Why**: ContainsKey is clearer than accessing Keys collection.
**Fix**: Replace `dict.Keys.Contains(key)` with `dict.ContainsKey(key)`.

#### CA1842: Do not use WhenAll with a single task
**Why**: Unnecessary overhead when only one task.
**Fix**: Just await the single task directly.

#### CA1843: Do not use WhenAny with a single task
**Why**: Unnecessary overhead when only one task.
**Fix**: Just await the single task directly.

#### CA1844: Provide memory-based overrides of async methods
**Why**: Enables better performance with Span<T>/Memory<T>.
**Fix**: Add Memory<byte> overloads alongside byte[] overloads.

#### CA1845: Use span-based string.Concat
**Why**: Avoids allocations for string concatenation.
**Fix**: Use span-based overloads where available.

#### CA1846: Prefer AsSpan over Substring
**Why**: AsSpan avoids string allocation.
**Fix**: Replace `s.Substring(start, length)` with `s.AsSpan(start, length)`.

#### CA1847: Use String.Contains(char) instead of String.Contains(string) with single character
**Why**: char overload is more efficient.
**Fix**: Replace `s.Contains("x")` with `s.Contains('x')`.

#### CA1848: Use LoggerMessage delegates for high-performance logging
**Why**: Avoids boxing and string formatting overhead.
**Fix**: Use LoggerMessage.Define for frequently-called log statements.

#### CA1849: Call async methods when in an async method
**Why**: Calling synchronous versions of async APIs can cause deadlocks.
**Fix**: Use the async version and await it.

#### CA1850: Prefer static HashData method over ComputeHash
**Why**: Static method avoids unnecessary allocations.
**Fix**: Use `SHA256.HashData(data)` instead of `new SHA256().ComputeHash(data)`.

## IDE Rules (IDE0001-IDE9999)

### IDE00xx: Code Style

#### IDE0001: Simplify name
**Why**: Unnecessary qualification clutters code.
**Fix**: Remove redundant namespace qualification.

#### IDE0002: Simplify member access
**Why**: `this.` is often unnecessary.
**Fix**: Remove `this.` unless required for disambiguation.

#### IDE0003: Remove this or Me qualification
**Why**: Cleaner code when not needed.
**Fix**: Remove `this.` prefix from member access.

#### IDE0004: Remove unnecessary cast
**Why**: Redundant casts clutter code.
**Fix**: Remove the cast if the type is already correct.

#### IDE0005: Remove unnecessary using directive
**Why**: Unused imports slow compilation and clutter code.
**Fix**: Remove unused using statements (usually auto-fixed by IDE).

#### IDE0007, IDE0008: Use explicit/implicit type (var)
**Why**: Consistency in var usage improves readability.
**Fix**: Use var where type is obvious, explicit type otherwise (per team convention).

#### IDE0009: Use this qualifier
**Why**: Explicit this improves clarity in some codebases.
**Fix**: Add `this.` prefix (depends on editorconfig setting).

#### IDE0010: Add missing switch cases
**Why**: Ensures all enum values are handled.
**Fix**: Add cases for missing enum values or add a default case.

#### IDE0011: Add braces
**Why**: Braces prevent certain classes of bugs.
**Fix**: Add braces around single-statement blocks.

#### IDE0016: Use throw expression
**Why**: More concise null checking.
**Fix**: Replace `if (x == null) throw new...` with `x ?? throw new...`.

#### IDE0017: Use object initializer
**Why**: More concise and readable.
**Fix**: Replace separate assignment statements with object initializer.

#### IDE0018: Inline variable declaration
**Why**: Declare variables closer to usage.
**Fix**: Declare variables inline (e.g., in out parameters).

#### IDE0019: Use pattern matching to avoid 'as' followed by null check
**Why**: Pattern matching is more concise and type-safe.
**Fix**: Replace `var x = obj as Type; if (x != null)` with `if (obj is Type x)`.

#### IDE0020, IDE0038: Use pattern matching to avoid is check followed by cast
**Why**: Pattern matching avoids redundant cast.
**Fix**: Replace `if (obj is Type) { var x = (Type)obj; }` with `if (obj is Type x)`.

#### IDE0021: Use expression body for constructors
**Why**: Concise syntax for simple constructors.
**Fix**: Replace block body with `=>` expression.

#### IDE0022: Use expression body for methods
**Why**: Concise syntax for simple methods.
**Fix**: Replace block body with `=>` expression.

#### IDE0023, IDE0024: Use expression body for operators
**Why**: Concise syntax for simple operators.
**Fix**: Replace block body with `=>` expression.

#### IDE0025: Use expression body for properties
**Why**: Concise syntax for simple properties.
**Fix**: Replace block body with `=>` expression.

#### IDE0026: Use expression body for indexers
**Why**: Concise syntax for simple indexers.
**Fix**: Replace block body with `=>` expression.

#### IDE0027: Use expression body for accessors
**Why**: Concise syntax for simple get/set accessors.
**Fix**: Replace block body with `=>` expression.

#### IDE0028: Use collection initializers
**Why**: More concise initialization.
**Fix**: Use collection initializer syntax.

#### IDE0029, IDE0030: Use coalesce expression
**Why**: Null coalescing is more concise.
**Fix**: Replace `x != null ? x : y` with `x ?? y`.

#### IDE0031: Use null propagation
**Why**: More concise null checking.
**Fix**: Replace `x != null ? x.Property : null` with `x?.Property`.

#### IDE0032: Use auto property
**Why**: Simpler syntax when backing field logic isn't needed.
**Fix**: Replace field + property with auto-property.

#### IDE0033: Use explicitly provided tuple name
**Why**: Named tuple elements improve readability.
**Fix**: Use `.Name` instead of `.Item1`.

#### IDE0034: Simplify default expression
**Why**: `default` is more concise than `default(T)`.
**Fix**: Replace `default(T)` with `default`.

#### IDE0035: Remove unreachable code
**Why**: Dead code should be removed.
**Fix**: Delete unreachable code.

#### IDE0036: Order modifiers
**Why**: Consistent modifier order improves readability.
**Fix**: Reorder modifiers to match convention (public static readonly, etc.).

#### IDE0037: Use inferred member name
**Why**: More concise tuple/anonymous type initialization.
**Fix**: Remove redundant member names when name can be inferred.

#### IDE0039: Use local function instead of lambda
**Why**: Local functions have better performance and scoping.
**Fix**: Convert lambda to local function where appropriate.

#### IDE0040: Add accessibility modifiers
**Why**: Explicit accessibility improves clarity.
**Fix**: Add public/private/internal/protected modifier.

#### IDE0041: Use is null check
**Why**: `is null` is more readable than `== null` for reference types.
**Fix**: Replace `x == null` with `x is null`.

#### IDE0042: Deconstruct variable declaration
**Why**: Deconstruction is more concise for tuples.
**Fix**: Use `var (x, y) = tuple;` instead of `var x = tuple.Item1;`.

#### IDE0044: Add readonly modifier
**Why**: Readonly fields prevent accidental modification.
**Fix**: Add readonly keyword to fields that are never reassigned.

#### IDE0045: Use conditional expression for assignment
**Why**: Ternary operator is more concise.
**Fix**: Replace if-else assignment with `x = condition ? a : b;`.

#### IDE0046: Use conditional expression for return
**Why**: Ternary operator is more concise for returns.
**Fix**: Replace if-else return with `return condition ? a : b;`.

#### IDE0047, IDE0048: Use parentheses for clarity
**Why**: Parentheses make operator precedence explicit.
**Fix**: Add or remove parentheses per editorconfig setting.

#### IDE0049: Use language keywords instead of framework type names
**Why**: `int` is more idiomatic than `Int32`.
**Fix**: Replace `Int32` with `int`, `String` with `string`, etc.

#### IDE0050: Convert anonymous type to tuple
**Why**: Tuples are simpler and more efficient.
**Fix**: Replace anonymous type with tuple where appropriate.

#### IDE0051: Remove unused private member
**Why**: Dead code should be removed.
**Fix**: Delete unused private members.

#### IDE0052: Remove unread private member
**Why**: Write-only members are usually mistakes.
**Fix**: Remove member or use its value.

#### IDE0053: Use expression body for lambdas
**Why**: More concise lambda syntax.
**Fix**: Replace `x => { return x * 2; }` with `x => x * 2`.

#### IDE0054, IDE0074: Use compound assignment
**Why**: `+=` is more concise than `x = x + 1`.
**Fix**: Use compound assignment operators.

#### IDE0055: Fix formatting
**Why**: Consistent formatting improves readability.
**Fix**: Apply editorconfig formatting rules (usually auto-fixed by IDE).

#### IDE0056: Use index operator
**Why**: `^` operator is more concise for end-relative indexing.
**Fix**: Replace `arr[arr.Length - 1]` with `arr[^1]`.

#### IDE0057: Use range operator
**Why**: `..` operator is more concise for slicing.
**Fix**: Replace `arr.Substring(1, 3)` with `arr[1..4]`.

#### IDE0058: Remove unnecessary expression value
**Why**: Expression value is unused.
**Fix**: Assign the result or remove the expression if it has no side effects.

#### IDE0059: Unnecessary assignment of a value
**Why**: Value is assigned but never used.
**Fix**: Remove the assignment or use the value.

#### IDE0060: Remove unused parameter
**Why**: Unused parameters clutter the signature.
**Fix**: Remove unused parameter or use discard `_` if required by interface.

#### IDE0061: Use expression body for local functions
**Why**: More concise syntax for simple local functions.
**Fix**: Replace block body with `=>` expression.

#### IDE0062: Make local function static
**Why**: Static local functions can't capture variables accidentally.
**Fix**: Add static keyword to local function that doesn't capture.

#### IDE0063: Use simple using statement
**Why**: Simplified using reduces nesting.
**Fix**: Replace `using (var x = ...) { }` with `using var x = ...;`.

#### IDE0064: Make struct fields writable
**Why**: Readonly struct fields can't be modified in-place.
**Fix**: Remove readonly from fields if the struct needs to be mutable.

#### IDE0065: using directive placement
**Why**: Consistent placement improves readability.
**Fix**: Move using directives inside or outside namespace per editorconfig.

#### IDE0066: Use switch expression
**Why**: Switch expressions are more concise and functional.
**Fix**: Convert switch statement to switch expression where appropriate.

#### IDE0070: Use System.HashCode.Combine
**Why**: Safer and easier than manual hash code combination.
**Fix**: Replace manual hash combination with `HashCode.Combine()`.

#### IDE0071: Simplify interpolation
**Why**: Remove unnecessary ToString() in string interpolation.
**Fix**: Replace `$"{x.ToString()}"` with `$"{x}"`.

#### IDE0072: Add missing switch cases
**Why**: Ensures all cases are handled in switch expression.
**Fix**: Add missing cases or add discard pattern.

#### IDE0073: Use file header
**Why**: Consistent file headers (copyright, etc.).
**Fix**: Add required file header per editorconfig.

#### IDE0075: Simplify conditional expression
**Why**: `condition ? true : false` is redundant.
**Fix**: Replace with just `condition`.

#### IDE0076, IDE0077: Avoid legacy format target in global SuppressMessageAttribute
**Why**: Use modern format for suppressions.
**Fix**: Update suppression attribute format.

#### IDE0078: Use pattern matching
**Why**: Pattern matching is more expressive.
**Fix**: Replace type checks with pattern matching.

#### IDE0079: Remove unnecessary suppression
**Why**: Suppressions for rules that aren't violated clutter code.
**Fix**: Remove the suppression pragma or attribute.

#### IDE0080: Remove unnecessary suppression operator
**Why**: `!` null-forgiving operator is unnecessary when not needed.
**Fix**: Remove unnecessary `!` operators.

#### IDE0081: Remove ByVal (VB only)
**Why**: ByVal is implicit in VB.NET.
**Fix**: Remove ByVal keyword.

#### IDE0082: Convert typeof to nameof
**Why**: nameof is safer for property names.
**Fix**: Replace `typeof(T).Name` with `nameof(T)` where appropriate.

#### IDE0083: Use pattern matching (not pattern)
**Why**: `is not null` is more readable than `!(x is null)`.
**Fix**: Use not pattern.

#### IDE0090: Simplify new expression
**Why**: Target-typed new is more concise in C# 9+.
**Fix**: Replace `new Type()` with `new()` when type is obvious.

#### IDE0100: Remove unnecessary equality operator
**Why**: Redundant comparison in pattern matching.
**Fix**: Simplify pattern.

#### IDE0110: Remove unnecessary discard
**Why**: Discard is unnecessary in some contexts.
**Fix**: Remove `_` discard where not needed.

#### IDE0120: Simplify LINQ expression
**Why**: Some LINQ chains can be simplified.
**Fix**: Simplify the LINQ expression.

#### IDE0130: Namespace does not match folder structure
**Why**: Consistent namespace-folder mapping improves navigation.
**Fix**: Adjust namespace to match folder structure.

#### IDE0150: Prefer null check over type check
**Why**: `is not null` is clearer than `is object`.
**Fix**: Replace type check with null check.

#### IDE0160, IDE0161: Use block-scoped/file-scoped namespace
**Why**: File-scoped namespaces reduce indentation.
**Fix**: Convert to file-scoped namespace: `namespace X;`.

#### IDE0170: Simplify property pattern
**Why**: Nested patterns can be flattened.
**Fix**: Simplify property pattern matching.

#### IDE0180: Use tuple to swap values
**Why**: Tuple deconstruction is clearer for swaps.
**Fix**: Replace `temp = a; a = b; b = temp;` with `(a, b) = (b, a);`.

#### IDE0200: Remove unnecessary lambda expression
**Why**: Method group is more concise than wrapping lambda.
**Fix**: Replace `x => Method(x)` with `Method`.

#### IDE0210, IDE0211: Use top-level statements / explicit Main
**Why**: Consistency in program entry point style.
**Fix**: Convert to/from top-level statements per project convention.

#### IDE0220: foreach cast
**Why**: Add explicit cast in foreach when needed.
**Fix**: Add cast: `foreach (Type item in collection)`.

#### IDE0230: Use UTF-8 string literal
**Why**: UTF-8 literals avoid encoding overhead.
**Fix**: Use `"text"u8` for UTF-8 byte arrays.

#### IDE0240, IDE0241: Nullable directive is redundant/unnecessary
**Why**: Redundant nullable context directives.
**Fix**: Remove unnecessary `#nullable` directives.

#### IDE0250: Struct can be made readonly
**Why**: Readonly structs enable compiler optimizations.
**Fix**: Add readonly modifier to struct.

#### IDE0251: Member can be made readonly
**Why**: Readonly members enable compiler optimizations in structs.
**Fix**: Add readonly modifier to struct member.

#### IDE0260, IDE0261: Use pattern matching / explicit type
**Why**: Consistency in pattern matching vs explicit type.
**Fix**: Use pattern matching or explicit type per editorconfig.

#### IDE0270: Use coalesce expression
**Why**: Null coalescing is more concise.
**Fix**: Replace if-null-assign pattern with `??=`.

#### IDE0280: Use nameof
**Why**: nameof is refactor-safe.
**Fix**: Replace string literals with `nameof()`.

#### IDE0290: Use primary constructor
**Why**: Primary constructors reduce boilerplate in C# 12+.
**Fix**: Convert constructor parameters to primary constructor.

#### IDE1005: Use conditional delegate call
**Why**: Null-conditional delegate invocation is safer.
**Fix**: Replace `if (handler != null) handler(...);` with `handler?.Invoke(...);`.

#### IDE2000: Avoid multiple blank lines
**Why**: Consistent spacing improves readability.
**Fix**: Remove extra blank lines.

## Compiler Rules (CS)

Most CS warnings shouldn't be suppressed as they indicate actual compiler issues. Common ones:

#### CS0108: Member hides inherited member; missing 'new' keyword
**Fix**: Add `new` keyword if hiding is intentional, or rename the member.

#### CS0114: Member hides inherited member; missing 'override' or 'new' keyword
**Fix**: Add `override` or `new` keyword as appropriate.

#### CS0162: Unreachable code detected
**Fix**: Remove dead code or fix the logic.

#### CS0168, CS0219: Variable is declared but never used / assigned but never used
**Fix**: Remove the variable or use it.

#### CS0618: Type or member is obsolete
**Fix**: Use the recommended replacement or suppress if upgrade isn't feasible yet.

#### CS0649: Field is never assigned to
**Fix**: Remove the field, assign to it, or mark with `[Obsolete]` if it's legacy.

#### CS1591: Missing XML comment for publicly visible type or member
**Fix**: Add XML doc comments or adjust editorconfig to not require them.

#### CS8600-CS8669: Nullable reference types warnings
**Fix**: Properly handle nullability with `?` annotations or null checks.

## AsyncFixer Rules

AsyncFixer analyzes async/await patterns and prevents common mistakes.

#### AsyncFixer01: Unnecessary async/await usage
**Why**: Adding async/await when not needed adds overhead and complexity.
**Fix**: Remove async/await and directly return the Task if no await is needed.

#### AsyncFixer02: Long-running or blocking operations inside async method
**Why**: Blocking operations defeat the purpose of async and can cause thread pool starvation.
**Fix**: Replace blocking calls with async equivalents (Task.Run for CPU-bound, async I/O for I/O-bound).

#### AsyncFixer03: Fire & forget async void methods
**Why**: Async void methods can't be awaited and exceptions are unhandled, causing crashes.
**Fix**: Change return type to Task and ensure the method is awaited.

#### AsyncFixer04: Fire & forget async call inside using block
**Why**: The using block may dispose resources before the async operation completes.
**Fix**: Await the async call before exiting the using block.

#### AsyncFixer05: Downcasting from nested task to outer task
**Why**: Task<Task<T>> needs double awaiting; direct cast loses the inner result.
**Fix**: Use await twice or call Unwrap() to flatten the nested task.

#### AsyncifyInvocation, AsyncifyVariable: Use Task Async
**Why**: Suggests converting synchronous calls to async equivalents.
**Fix**: Use async versions of methods where available (ReadAsync, WriteAsync, etc.).

## Meziantou.Analyzer Rules (MA)

Meziantou.Analyzer is a comprehensive analyzer covering performance, correctness, and best practices.

#### MA0001: StringComparison is missing
**Why**: String comparisons without StringComparison can have culture-dependent behavior.
**Fix**: Add StringComparison parameter (Ordinal, OrdinalIgnoreCase, etc.).

#### MA0002: IEqualityComparer<string> is missing
**Why**: String dictionaries/sets need explicit comparer for correct behavior.
**Fix**: Specify StringComparer.Ordinal or StringComparer.OrdinalIgnoreCase.

#### MA0003: Add parameter name to improve readability
**Why**: Named parameters improve call-site readability for booleans and magic values.
**Fix**: Add parameter names at call sites (e.g., `Method(enabled: true)`).

#### MA0004: Use ConfigureAwait(false)
**Why**: Library code shouldn't capture SynchronizationContext; prevents deadlocks.
**Fix**: Add .ConfigureAwait(false) to all awaits in library code.

#### MA0005: Use Array.Empty<T>()
**Why**: Array.Empty<T>() reuses a singleton instance; new T[0] allocates.
**Fix**: Replace `new T[0]` with `Array.Empty<T>()`.

#### MA0006: Use String.Equals instead of equality operator
**Why**: Operator doesn't allow specifying StringComparison.
**Fix**: Use `string.Equals(a, b, StringComparison.Ordinal)`.

#### MA0007: Add comma after last enum value
**Why**: Makes diffs cleaner when adding new enum values.
**Fix**: Add trailing comma to last enum member.

#### MA0008: Add StructLayoutAttribute
**Why**: Explicit layout prevents unexpected struct packing and P/Invoke issues.
**Fix**: Add `[StructLayout(LayoutKind.Auto)]` or appropriate layout.

#### MA0009: Add null check after as
**Why**: 'as' can return null; should be checked before use.
**Fix**: Check for null or use 'is' pattern matching instead.

#### MA0010: Mark attributes with AttributeUsageAttribute
**Why**: Specifies where the attribute can be applied and inheritance behavior.
**Fix**: Add `[AttributeUsage(AttributeTargets.Class)]` or appropriate targets.

#### MA0012: Do not raise reserved exception type
**Why**: Generic exceptions like Exception are too broad.
**Fix**: Throw specific exception types (ArgumentException, InvalidOperationException, etc.).

#### MA0015: Specify parameter name in ArgumentException
**Why**: Makes debugging easier by identifying which parameter was invalid.
**Fix**: Pass parameter name: `throw new ArgumentException("message", nameof(param))`.

#### MA0017: Abstract types should not have public constructors
**Why**: Abstract types can't be instantiated; public constructor is misleading.
**Fix**: Make constructor protected.

#### MA0020: Use direct methods instead of LINQ
**Why**: Direct array/list access is faster than LINQ for simple operations.
**Fix**: Replace `list.FirstOrDefault()` with `list.Count > 0 ? list[0] : default`.

#### MA0021: Use StringComparer.GetHashCode instead of string.GetHashCode
**Why**: String.GetHashCode() is culture-sensitive and randomized.
**Fix**: Use `StringComparer.Ordinal.GetHashCode(str)`.

#### MA0022: Return Task.FromResult instead of returning null
**Why**: Returning null Task causes NullReferenceException when awaited.
**Fix**: Return `Task.FromResult<T>(default)` or `Task.CompletedTask`.

#### MA0023: Add RegexOptions.ExplicitCapture
**Why**: Explicit capture improves Regex performance by disabling implicit groups.
**Fix**: Add RegexOptions.ExplicitCapture to Regex constructor.

#### MA0024: Use explicit StringComparer when possible
**Why**: Dictionaries with string keys need explicit comparer for correctness.
**Fix**: Specify comparer in constructor: `new Dictionary<string, T>(StringComparer.Ordinal)`.

#### MA0026: Fix TODO comment
**Why**: TODO comments should be tracked in issue tracker, not code.
**Fix**: Create an issue and reference it, or fix the TODO immediately.

#### MA0027: Prefer rethrowing exception implicitly (throw vs throw ex)
**Why**: `throw ex` resets the stack trace; `throw` preserves it.
**Fix**: Use `throw;` without the exception variable.

#### MA0029: Combine LINQ methods
**Why**: Multiple LINQ iterations are inefficient; combine into one.
**Fix**: Replace `Where().Select()` with single Select that filters.

#### MA0030: Remove useless DefaultValue attribute
**Why**: DefaultValue doesn't set the actual default; it's just metadata.
**Fix**: Remove attribute or set default in constructor.

#### MA0031: Optimize Enumerable.Count usage
**Why**: Calling Count() on collections with Count property is inefficient.
**Fix**: Use .Count property directly.

#### MA0032: Specify a cancellation token
**Why**: Enables cooperative cancellation of long-running operations.
**Fix**: Add CancellationToken parameter and pass it through.

#### MA0033: Do not tag instance fields with ThreadStaticAttribute
**Why**: ThreadStatic only works on static fields.
**Fix**: Make field static or use AsyncLocal<T> for instance fields.

#### MA0035: Do not use dangerous threading methods
**Why**: Thread.Suspend/Resume/Abort are deprecated and unsafe.
**Fix**: Use cooperative cancellation with CancellationToken.

#### MA0039: Do not write custom certificate validation
**Why**: Custom validation often has security holes.
**Fix**: Use built-in certificate validation or consult security expert.

#### MA0040: Flow the cancellation token when available
**Why**: Enables cancellation throughout the call chain.
**Fix**: Pass CancellationToken to called methods.

#### MA0042: Do not use blocking calls in async method
**Why**: Blocking defeats async benefits and can cause deadlocks.
**Fix**: Use async equivalents (Task.WaitAsync, not Wait; await, not Result).

#### MA0044: Remove useless ToString call
**Why**: String interpolation and concatenation automatically call ToString.
**Fix**: Remove redundant .ToString() calls.

#### MA0045: Do not use blocking call (make method async)
**Why**: Blocking synchronous calls should be converted to async.
**Fix**: Make the method async and use await.

#### MA0048: File name must match type name
**Why**: Consistent naming improves navigation.
**Fix**: Rename file to match the primary type it contains.

#### MA0050: Validate arguments correctly in iterator methods
**Why**: Iterator methods don't execute argument validation until enumeration starts.
**Fix**: Split into wrapper method that validates and inner iterator.

#### MA0051: Method is too long (60+ lines)
**Why**: Long methods are hard to understand and test.
**Fix**: Extract smaller methods from the long method.

#### MA0052: Replace constant Enum.ToString with nameof
**Why**: nameof is refactor-safe and compile-time checked.
**Fix**: Replace `MyEnum.Value.ToString()` with `nameof(MyEnum.Value)`.

#### MA0054: Embed caught exception as innerException
**Why**: Preserves full stack trace and exception chain.
**Fix**: Pass caught exception to new exception's constructor.

#### MA0055: Do not use finalizer
**Why**: Finalizers are complex, non-deterministic, and rarely needed.
**Fix**: Use IDisposable or SafeHandle instead.

#### MA0056: Do not call overridable members in constructor
**Why**: Virtual methods can be called before derived class constructor runs.
**Fix**: Call method after construction or make it non-virtual.

#### MA0060: Use return value of Stream.Read/ReadAsync
**Why**: Read doesn't always read the requested number of bytes.
**Fix**: Check return value and loop until all bytes are read.

#### MA0061: Method overrides should not change default values
**Why**: Default values are resolved at compile-time, causing confusion.
**Fix**: Use the same default value as base method.

#### MA0062: Non-flags enums should not have FlagsAttribute
**Why**: Flags attribute is for bitfields only.
**Fix**: Remove [Flags] or make enum properly support bitwise operations.

#### MA0063: Use Where before OrderBy
**Why**: Filtering before sorting is more efficient.
**Fix**: Reorder LINQ: `.Where(predicate).OrderBy(key)`.

#### MA0064: Avoid locking on publicly accessible instance
**Why**: Public locks can cause deadlocks if external code locks the same object.
**Fix**: Lock on a private readonly object.

#### MA0065: Default ValueType Equals/GetHashCode is used for struct
**Why**: Default implementation uses reflection, which is slow.
**Fix**: Override Equals, GetHashCode, operator==, operator!=.

#### MA0072: Do not throw from finally block
**Why**: Exceptions from finally blocks hide the original exception.
**Fix**: Catch and log exceptions in finally, don't rethrow.

#### MA0076: Do not use implicit culture-sensitive ToString
**Why**: ToString() without format provider is culture-dependent.
**Fix**: Use `.ToString(CultureInfo.InvariantCulture)`.

#### MA0077: Equals(T) should implement IEquatable<T>
**Why**: Type-safe equality without boxing.
**Fix**: Implement `IEquatable<T>` interface.

#### MA0078: Use Cast instead of Select to cast
**Why**: Cast is more efficient and intention-revealing than Select.
**Fix**: Replace `.Select(x => (T)x)` with `.Cast<T>()`.

#### MA0079, MA0080: Use a cancellation token using .WithCancellation()
**Why**: IAsyncEnumerable should support cancellation.
**Fix**: Add .WithCancellation(token) to async enumerable.

#### MA0082: NaN should not be used in comparisons
**Why**: NaN != NaN by specification; comparisons always return false.
**Fix**: Use `double.IsNaN(value)`.

#### MA0084: Local variables should not hide other symbols
**Why**: Shadowing makes code confusing.
**Fix**: Rename the local variable.

#### MA0085: Anonymous delegates should not unsubscribe from events
**Why**: Unsubscribe requires reference equality; lambdas create new instances.
**Fix**: Store delegate in field and unsubscribe using that reference.

#### MA0086: Do not throw from finalizer
**Why**: Finalizer exceptions crash the process.
**Fix**: Catch and log all exceptions in finalizer.

#### MA0089: Optimize string method usage
**Why**: Some string operations have more efficient alternatives.
**Fix**: Use specific recommendations (e.g., StartsWith instead of IndexOf == 0).

#### MA0091: Sender should be this for instance events
**Why**: Event sender convention is that sender is the raising object.
**Fix**: Pass `this` as sender argument.

#### MA0093: EventArgs should not be null
**Why**: EventArgs.Empty is the convention for events without data.
**Fix**: Pass `EventArgs.Empty` instead of null.

#### MA0100: Await task before disposing resources
**Why**: using statement disposes before async operation completes.
**Fix**: Use `await using` or await the task before exiting using.

#### MA0103: Use SequenceEqual instead of equality operator
**Why**: Arrays compare by reference, not contents.
**Fix**: Use `arr1.SequenceEqual(arr2)`.

#### MA0105: Use lambda parameters instead of closure
**Why**: Capturing variables in closures can cause performance and correctness issues.
**Fix**: Pass captured variables as lambda parameters.

#### MA0110: Use Regex source generator
**Why**: Source generators improve Regex performance.
**Fix**: Use `[GeneratedRegex]` attribute with partial method.

#### MA0111: Use Memory/Span overloads
**Why**: Memory/Span overloads avoid allocations.
**Fix**: Use overloads that accept Span<T> or Memory<T>.

#### MA0112: Use Enumerable.TryGetNonEnumeratedCount
**Why**: Avoids unnecessary enumeration when count is readily available.
**Fix**: Use TryGetNonEnumeratedCount before enumerating.

#### MA0129: Await task in using statement
**Why**: using disposes before async task completes.
**Fix**: Use `await using` for IAsyncDisposable.

#### MA0130: GetType() should not be used on System.Type
**Why**: typeof is compile-time and more efficient.
**Fix**: Use `typeof(T)` instead of `t.GetType()` when T is known.

#### MA0131: ThrowIfNull should not be used with non-nullable types
**Why**: Non-nullable value types can never be null.
**Fix**: Remove the null check for value types.

#### MA0134: Observe result of async calls
**Why**: Not awaiting or storing Task can hide exceptions.
**Fix**: Await the task or assign to variable.

#### MA0140: Both if and else branches have identical code
**Why**: Duplicate code should be extracted.
**Fix**: Move common code outside the if/else.

#### MA0143: Primary constructor parameters should be readonly
**Why**: Primary constructor parameters are captured; reassigning is confusing.
**Fix**: Don't reassign primary constructor parameters.

#### MA0147: Avoid async void method for delegate
**Why**: Async void can't be awaited and exceptions are unhandled.
**Fix**: Use `Func<Task>` instead of `Action` for async delegates.

#### MA0150: Do not call default ToString explicitly
**Why**: String interpolation calls ToString automatically.
**Fix**: Remove `.ToString()` in string interpolations.

#### MA0151: DebuggerDisplay must contain valid members
**Why**: Invalid member names cause exceptions in debugger.
**Fix**: Use valid property/field names in DebuggerDisplay format string.

#### MA0160: Use ContainsKey instead of TryGetValue when value is not used
**Why**: ContainsKey is more intention-revealing.
**Fix**: Replace `TryGetValue(key, out _)` with `ContainsKey(key)`.

#### MA0163: UseShellExecute must be false when redirecting I/O
**Why**: Windows Process API requirement.
**Fix**: Set `UseShellExecute = false` when redirecting StandardOutput/Input/Error.

#### MA0172: Both sides of logical operation are identical
**Why**: Likely a copy-paste error.
**Fix**: Fix the condition or remove redundant check.

## Roslynator Rules (RCS)

Roslynator provides code analysis and refactorings for C#.

#### RCS0063: Use linefeed as newline
**Why**: Consistent line endings across platforms (LF vs CRLF).
**Fix**: Configure git to use LF line endings.

#### RCS1036: Remove redundant empty line
**Why**: Unnecessary blank lines reduce code density.
**Fix**: Remove extra blank lines.

#### RCS1068: Simplify logical negation
**Why**: Double negation (!!) is confusing.
**Fix**: Simplify to positive form.

#### RCS1197: Optimize StringBuilder.Append/AppendLine
**Why**: Multiple Append calls can be combined for better performance.
**Fix**: Combine sequential Append calls.

## SonarAnalyzer Rules (S)

SonarAnalyzer focuses on code quality, bugs, and vulnerabilities.

#### S108: Nested blocks should not be empty
**Why**: Empty blocks are usually mistakes or unfinished code.
**Fix**: Remove empty block or add TODO comment if incomplete.

#### S125: Sections of code should not be commented out
**Why**: Commented code rots and clutters the codebase.
**Fix**: Delete commented code (version control preserves history).

#### S583: Jump statements should not be redundant
**Why**: Unreachable return/continue/break statements are confusing.
**Fix**: Remove redundant jump statements.

#### S1048: Destructors should not throw exceptions
**Why**: Exceptions in finalizers crash the process.
**Fix**: Catch and log all exceptions in destructors.

#### S1135: Track uses of TODO tags
**Why**: TODOs should be tracked in issue system.
**Fix**: Create issue and reference it in comment.

#### S1244: Floating point equality comparisons
**Why**: Floating point math has rounding errors; exact equality often fails.
**Fix**: Use tolerance-based comparison: `Math.Abs(a - b) < epsilon`.

#### S2306: async/await should be used properly
**Why**: Incorrect async patterns cause deadlocks or hidden exceptions.
**Fix**: Follow async best practices (ConfigureAwait, avoid async void, etc.).

#### S2325: Methods that don't access instance data should be static
**Why**: Static methods are more efficient and clearer in intent.
**Fix**: Add static keyword.

#### S2551: Shared resources should not be used for locking
**Why**: Locking on strings, types, or this can cause external deadlocks.
**Fix**: Lock on private readonly object.

#### S2930: IDisposables should be disposed
**Why**: Prevents resource leaks.
**Fix**: Use using statement or call Dispose in finally.

#### S2931: Classes with IDisposable members should implement IDisposable
**Why**: Owning disposable fields requires cleanup.
**Fix**: Implement IDisposable and dispose owned fields.

#### S2953: Methods should not have identical implementations
**Why**: Duplicate code should be extracted to shared method.
**Fix**: Extract common implementation.

#### S4136: Method overloads should be grouped together
**Why**: Grouping overloads improves readability.
**Fix**: Move overloaded methods adjacent to each other.

#### S4586: Non-async Task/Task<T> methods should not return null
**Why**: Returning null Task causes NullReferenceException when awaited.
**Fix**: Return `Task.CompletedTask` or `Task.FromResult<T>(default)`.

## Visual Studio Threading Analyzer (VSTHRD)

VSTHRD analyzes threading patterns in Visual Studio and .NET applications.

#### VSTHRD001: Avoid legacy thread switching APIs
**Why**: Legacy APIs are obsolete and have better alternatives.
**Fix**: Use modern async patterns instead of Begin/End methods.

#### VSTHRD002: Avoid problematic synchronous waits
**Why**: Synchronous waits on async work can deadlock.
**Fix**: Use async/await instead of Wait(), Result, GetAwaiter().GetResult().

#### VSTHRD100: Avoid async void methods
**Why**: Async void can't be awaited and exceptions crash the process.
**Fix**: Change return type to Task.

#### VSTHRD101: Avoid unsupported async delegates
**Why**: Some delegate types don't support async.
**Fix**: Use Func<Task> instead of Action for async delegates.

#### VSTHRD103: Call async methods when in an async method
**Why**: Calling sync from async loses async benefits.
**Fix**: Use async versions of methods (ReadAsync, not Read).

#### VSTHRD107: Await Task within using expression
**Why**: using disposes before async task completes.
**Fix**: Await the task before exiting using block.

#### VSTHRD110: Observe result of async calls
**Why**: Unobserved tasks hide exceptions.
**Fix**: Await or store the returned Task.

#### VSTHRD111: Use ConfigureAwait(bool)
**Why**: Explicit ConfigureAwait prevents accidental context capture.
**Fix**: Add .ConfigureAwait(false) in library code.

#### VSTHRD114: Avoid returning a null Task
**Why**: Null Task causes NullReferenceException when awaited.
**Fix**: Return Task.CompletedTask or Task.FromResult.

#### VSTHRD200: Use "Async" suffix for async methods
**Why**: Naming convention makes async methods easily recognizable.
**Fix**: Rename method to include Async suffix.

## xUnit Analyzer Rules

xUnit analyzers ensure proper test structure and assertions.

#### xUnit1004: Test methods should not be skipped
**Why**: Skipped tests hide problems and rot over time.
**Fix**: Remove Skip attribute or fix the test.

#### xUnit1033: Add matching constructor argument for fixture
**Why**: Fixture constructor parameters must match fixture type.
**Fix**: Add constructor parameter with correct fixture type.

#### xUnit1042: MemberData returns untyped data rows
**Why**: Untyped data makes tests fragile.
**Fix**: Use TheoryData<T> for type-safe test data.

#### xUnit1044, xUnit1045: TheoryData type arguments not serializable
**Why**: xUnit serializes test data for parallel execution.
**Fix**: Ensure TheoryData type arguments are serializable.

#### xUnit1046, xUnit1047: TheoryDataRow arguments not serializable
**Why**: Test data must be serializable for xUnit to pass between processes.
**Fix**: Use serializable types in TheoryDataRow.

#### xUnit1050: ClassData returns untyped data rows
**Why**: Type safety prevents runtime errors in tests.
**Fix**: Use TheoryData<T> instead of object arrays.

#### xUnit2001: Do not use invalid equality check
**Why**: Some equality checks don't work as expected.
**Fix**: Use proper xUnit assertion methods.

#### xUnit2019: Obsolete throws check
**Why**: Assert.Throws usage is outdated.
**Fix**: Use newer assertion syntax.

#### xUnit2022: Boolean assertions should not be negated
**Why**: Assert.True(!condition) is less clear than Assert.False(condition).
**Fix**: Use the positive assertion form.

#### xUnit2023: Do not use collection methods for single-item
**Why**: Assert.Single is clearer than Assert.Collection with one item.
**Fix**: Use Assert.Single for single-item checks.

#### xUnit2024: Do not use boolean asserts for simple equality
**Why**: Assert.Equal is clearer than Assert.True(a == b).
**Fix**: Use Assert.Equal/Assert.NotEqual.

#### xUnit2025: Boolean assertion can be simplified
**Why**: Some boolean assertions have simpler alternatives.
**Fix**: Use suggested simpler assertion.

#### xUnit2032: Assignable from assertions confusingly named
**Why**: IsAssignableFrom checks if parameter can be assigned TO type, not FROM.
**Fix**: Understand correct usage or use clearer assertion.

## SharpSource Analyzer Rules (SS)

SharpSource provides comprehensive analysis for common C# mistakes and anti-patterns.

#### SS001: Async methods should return Task instead of void
**Why**: Async void can't be awaited and exceptions are unhandled.
**Fix**: Change return type to Task.

#### SS002: Use DateTime.UtcNow instead of DateTime.Now
**Why**: DateTime.Now is local time and can have DST issues.
**Fix**: Use DateTime.UtcNow for timestamps, DateTimeOffset for user-facing times.

#### SS003: Integer division may cause implicit rounding
**Why**: Integer division truncates rather than rounds (5/2 == 2, not 3).
**Fix**: Cast to double for fractional results: `(double)a / b`.

#### SS004, SS005: Type used as Dictionary key should override Equals/GetHashCode
**Why**: Dictionary uses these methods; incorrect implementation causes bugs.
**Fix**: Override Equals and GetHashCode properly.

#### SS006: Throwing null will always result in NullReferenceException
**Why**: `throw null` always throws NullReferenceException.
**Fix**: Throw an actual exception instance.

#### SS007: Use the nameof() operator for arguments
**Why**: nameof is refactor-safe and compile-time checked.
**Fix**: Use `nameof(parameter)` instead of `"parameter"`.

#### SS008: GetHashCode() refers to mutable member
**Why**: Changing hash code after adding to Dictionary/HashSet causes loss.
**Fix**: Base hash code on immutable fields only.

#### SS009: Use the right overloaded Equals() method
**Why**: Using wrong Equals overload can cause boxing or incorrect comparison.
**Fix**: Use the type-specific Equals overload.

#### SS010: Use Guid.NewGuid() instead of new Guid()
**Why**: `new Guid()` creates empty GUID (all zeros), not a new unique GUID.
**Fix**: Use `Guid.NewGuid()` for new GUIDs.

#### SS012: Parameter with default value placed before regular parameter
**Why**: Optional parameters must come after required parameters.
**Fix**: Reorder parameters.

#### SS013: Use throw instead of throw ex to preserve stack trace
**Why**: `throw ex` resets stack trace; `throw` preserves it.
**Fix**: Use `throw;` without exception variable.

#### SS017: Struct should implement Equals/GetHashCode/ToString
**Why**: Default implementations use reflection and are slow.
**Fix**: Override Equals, GetHashCode, ToString for structs.

#### SS018: Enum without default member may cause issues
**Why**: Enum default value is 0; should have explicit 0 member.
**Fix**: Add a None = 0 member.

#### SS019: Switch is missing default label
**Why**: Unhandled values cause silent failures.
**Fix**: Add default case.

#### SS020: Unused exception caught
**Why**: Catching and ignoring exceptions hides problems.
**Fix**: Log the exception or remove the catch.

#### SS021: Incorrect string.Format argument count
**Why**: Mismatched placeholder count causes exceptions.
**Fix**: Match placeholders to arguments.

#### SS022: Only catch exceptions in a non-async context
**Why**: Catching exceptions in async void can miss unhandled exceptions.
**Fix**: Use async Task instead of async void.

#### SS023-SS031: Do not throw exceptions from specific locations
**Why**: Exceptions from property getters, operators, Dispose, finalizers, GetHashCode, Equals, static constructors, ToString cause serious problems.
**Fix**: Return default values or refactor to a method that can throw.

#### SS032: Use await instead of Thread.Sleep in async method
**Why**: Thread.Sleep blocks threads; Task.Delay yields properly.
**Fix**: Replace `Thread.Sleep(ms)` with `await Task.Delay(ms)`.

#### SS033: Access an awaited task result directly
**Why**: Awaiting then accessing Result is redundant.
**Fix**: Use await result directly: `var x = await task;`.

#### SS034, SS035: Avoid accessing Task.Result / Task.Wait
**Why**: Can cause deadlocks when synchronously waiting on async work.
**Fix**: Use async/await properly.

#### SS036: Use checked arithmetic for explicit overflow
**Why**: Integer overflow silently wraps without checked.
**Fix**: Use checked block or checked arithmetic.

#### SS037: Use IHttpClientFactory instead of new HttpClient
**Why**: Creating HttpClient instances exhausts sockets.
**Fix**: Use IHttpClientFactory or reuse a static HttpClient.

#### SS038: Recursive struct layout
**Why**: Struct containing itself causes infinite size.
**Fix**: Change to class or use indirection (reference type field).

#### SS039: Enum default must be "Unknown"/"None"
**Why**: Consistency in default enum value naming.
**Fix**: Name the 0 value None or Unknown (NOTE: This rule may be too prescriptive for some projects).

#### SS040: Do not write to static fields from instance methods
**Why**: Thread safety issues and confusing behavior.
**Fix**: Make method static or remove static field write.

#### SS041: Avoid materializing enumerables before immediate enumeration
**Why**: ToList() before foreach wastes memory.
**Fix**: Enumerate directly without ToList().

#### SS042: Do not concatenate strings in a loop
**Why**: String concatenation in loops is O(n²) due to immutability.
**Fix**: Use StringBuilder.

#### SS043: Static initializer accesses uninitialized static field
**Why**: Field initialization order is undefined; can read default value.
**Fix**: Reorder initializations or use static constructor.

#### SS044: Unique enum values may be intended as flags
**Why**: Enum with power-of-2 values should probably have [Flags].
**Fix**: Add [Flags] attribute.

#### SS045: Do not use async void lambda
**Why**: Async void lambdas can't be awaited and exceptions are unhandled.
**Fix**: Use `Func<Task>` instead of `Action`.

#### SS046: Synchronous call in async method
**Why**: Mixing sync and async defeats the purpose.
**Fix**: Use async versions of methods.

#### SS047: Apply Where before Select for LINQ performance
**Why**: Filtering before projection avoids unnecessary work.
**Fix**: Reorder to `.Where(predicate).Select(projection)`.

#### SS048: Missing ConfigureAwait(false)
**Why**: Library code shouldn't capture context.
**Fix**: Add .ConfigureAwait(false).

#### SS049: Dereferencing a possibly-null reference
**Why**: Null reference exceptions.
**Fix**: Add null check or use null-conditional operator.

#### SS050, SS051: Enum value has explicit value / default value is not zero
**Why**: Explicit values can cause maintenance issues; non-zero default is unexpected.
**Fix**: Remove explicit values or ensure 0 default.

#### SS052: Element order does not match documentation order
**Why**: XML doc order should match code order for readability.
**Fix**: Reorder documentation or code.

#### SS053: Use TryGetValue instead of ContainsKey + indexer
**Why**: TryGetValue is more efficient (single lookup vs two).
**Fix**: Replace ContainsKey + indexer with TryGetValue.

#### SS054: Use element access instead of LINQ method
**Why**: Indexer is more efficient than LINQ for lists/arrays.
**Fix**: Use `list[index]` instead of `list.ElementAt(index)`.

#### SS055: Attribute access can be simplified
**Why**: Some attribute patterns have simpler alternatives.
**Fix**: Use suggested simplification.

#### SS056: Collection lookup can be simplified
**Why**: Some lookup patterns can be optimized.
**Fix**: Use suggested optimization.

#### SS057: Collection modified during enumeration
**Why**: Modifying collection while enumerating throws InvalidOperationException.
**Fix**: Collect items to modify first, then modify after enumeration.

#### SS058: Use StringBuilder for concatenation in loops
**Why**: String concatenation is expensive in loops.
**Fix**: Use StringBuilder.

#### SS059: Use DisposeAsync for IAsyncDisposable
**Why**: IAsyncDisposable requires async disposal.
**Fix**: Use `await using` or call DisposeAsync.

#### SS060: Use IsEmpty on ConcurrentDictionary
**Why**: IsEmpty is more efficient than Count == 0 for concurrent collections.
**Fix**: Replace `dict.Count == 0` with `dict.IsEmpty`.

#### SS061: Use pattern matching instead of sequential checks
**Why**: Pattern matching is more concise and performant.
**Fix**: Convert to pattern matching syntax.

#### SS062: Value type compared to null
**Why**: Value types can never be null (unless Nullable<T>).
**Fix**: Remove null check or check for Nullable<T>.HasValue.

#### SS063: Use of new() can be simplified
**Why**: Target-typed new is more concise.
**Fix**: Replace `new Type()` with `new()` where type is inferred.

#### SS064: Unnecessary enum comparison value
**Why**: Some enum comparisons are redundant.
**Fix**: Simplify the comparison.

#### SS065: Unnecessary range on string expression
**Why**: Some range operations are redundant or can be simplified.
**Fix**: Simplify the range expression.

## StyleCop Rules (SA)

StyleCop rules are primarily stylistic. Consider whether the codebase benefits from strict enforcement:

#### SA1000-SA1999: Spacing, ordering, documentation rules
**Fix**: Apply the editorconfig settings or run `dotnet format`.

Most StyleCop rules should be fixed by automated formatters rather than suppressed individually.

## Summary

**Priority for fixing suppressions:**
1. **Security rules (CA2xxx, CA3xxx, CA5xxx)** - Prevent vulnerabilities
2. **Correctness rules (MA, SS, VSTHRD)** - Prevent bugs and crashes
3. **Performance rules (CA18xx, MA)** - Improve efficiency
4. **CA/IDE rules** - Design and code quality
5. **Nullable warnings (CS8xxx)** - Prevent null reference exceptions
6. **xUnit rules** - Test quality and correctness
7. **AsyncFixer, VSTHRD** - Async/await correctness
8. **Roslynator, SonarAnalyzer** - Code quality
9. **SA rules** - Stylistic (lowest priority, often auto-fixable)

**When reviewing suppressions, ask:**
- Why was this suppressed?
- Can we fix the root cause?
- Is there a better pattern?
- Should this be in editorconfig instead?
- Is this a test-specific exception?

**Analyzer Packages Used in This Codebase:**
- **Microsoft.CodeAnalysis.NetAnalyzers** - CA rules
- **Microsoft.CodeAnalysis.CSharp.CodeStyle** - IDE rules
- **AsyncFixer** - Async/await analysis
- **Meziantou.Analyzer** - Comprehensive analysis
- **Roslynator.Analyzers** - Code analysis and refactorings
- **SonarAnalyzer.CSharp** - Code quality and bugs
- **Microsoft.VisualStudio.Threading.Analyzers** - Threading patterns
- **xunit.analyzers** - xUnit test patterns
- **SharpSource** - Common C# mistakes
