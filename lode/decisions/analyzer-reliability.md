# Analyzer Decisions: Reliability
*Updated: 2026-11-20T17:16:51Z*

Rationale removed from source comments. Each section names the type that carried it.
Read this page before you change or delete the matching check.

## AppSettingsKeyReader
- TryRead parses into a local hash set so a document that fails partway leaves the caller's set unchanged. The set uses OrdinalIgnoreCase because configuration keys are case-insensitive and the only caller builds its set that way.
- Accepts only an object or array root. A lone JSON literal parses as a well-formed document with zero keys, which would arm the rule for every configuration read.
- An explicit grammar validates a JSON literal token instead of double.TryParse. double.TryParse accepts NaN, Infinity, a leading plus sign, a leading zero, and a trailing dot, and the reader must reject all of them.
- IsJsonNumber implements the strict JSON number grammar -?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?. The reader must reject the looser numeric forms that a general number parse would accept.
- SkipTrivia consumes whitespace and both comment forms. An unterminated block comment runs to the end of the text. As a result, the caller fails on the missing value instead of reading comment text as JSON.

## ArrayPoolSqliteParameterSizeAnalyzer
- The Size-assignment index depends only on the enclosing block. ArrayPoolSqliteParameterSizeAnalyzer builds it once per block and shares it across candidates to avoid a per-candidate rescan.

## AsyncDbIoAnalyzer
- Runs both syntactic gates before binding a semantic model. An invocation in an async context is out of scope. As a result, it must not pay for overload resolution.
- The Async sibling check compares names only, with no parameter-shape comparison. The sibling can live on the receiver type or on any DbConnection or DbCommand base. As a result, a name match is sufficient.

## AsyncEnumerableConsumedTwiceAnalyzer
- An iterator method reads its source once per call while it yields items one at a time. AsyncEnumerableConsumedTwiceAnalyzer therefore skips a body that contains a yield statement.
- An assignment to an async-enumerable symbol gives it a new sequence. Earlier enumerations of that symbol are discarded because they no longer describe the same source.
- Enumerations in the two branches of one if never run in the same pass. ExclusiveBranches.Are reports that relationship so the rule does not flag them as a double consumption.
- An async sequence reached through another instance is a different value. Only this and base qualify as the same sequence. As a result, the receiver shape is checked before the symbol is returned.

## AsyncFileIoAnalyzer
- Runs both syntactic checks before the semantic bind. Invocations in async contexts and async calls then never pay for overload resolution.
- The Async sibling check compares names only. BCL Async siblings accept the sync overload's parameters plus an optional trailing CancellationToken. As a result, a name match is sufficient without walking parameter lists.

## AwaitInFinallyAnalyzer
- An await expression, an await using statement, and an await foreach statement all suspend the finally clause. GetAwaitLocation therefore accepts all three node shapes.

## BareParseAnalyzer
- Catch clauses guard only the try block. A call in a catch or finally block runs outside that guard. As a result, the ancestor walk continues to an outer try statement.

## BlockingCallInLockAnalyzer
- A MemoryStream member completes in memory and holds no lock while it runs. BlockingCallInLockAnalyzer therefore exempts MemoryStream-derived types from the blocking-call rule.

## CaptiveDependencyAnalyzer
- A factory or instance argument means the container receives an already built object. As a result, the type arguments do not name what it holds. A service key is not such an argument. As a result, a keyed registration stays in scope.
- The container builds one DbContext instance for the application. As a result, the type arguments name that instance even when a factory or an instance supplies it. The factory result is stored in the singleton slot.
- A method name alone does not prove a registration. This holds because a user helper can carry the same name. The rule requires the symbol to declare on the Entity Framework type before it records a candidate.
- AddDbContext registers the context with the scoped lifetime by default. Only an explicit Singleton context lifetime argument makes the registration a singleton.
- ContextLifetime controls the DbContext itself, while optionsLifetime controls only the options object and stays scoped in a normal application. HasSingletonLifetime reads only the contextLifetime argument.
- GetLifetime covers Add, TryAdd, AddKeyed, and TryAddKeyed registrations. AddHostedService registers a singleton even though its name carries no lifetime.
- The implementationType argument names the object the container builds. A lone serviceType argument names it only when that type is a concrete class. This holds because an interface has no constructor.
- Both type arguments of a registration join the captive-dependency set. A caller can depend on the service interface or on the implementation class, and either dependency captures the same scoped instance.
- AddSingleton<TService, TImplementation> names both types, while AddSingleton<T> names the implementation only when T is a concrete class. An interface type argument names no constructible implementation.
- The report belongs on the registered type rather than on the whole call. The type argument list is absent for an expression-bodied generic call. As a result, the invocation location is the fallback.
- EF Core ships DbContext and its dependency graph ships ServiceLifetime. EfCoreTypes resolves both once per compilation and turns the rule off when either type is absent.

## CatchFilterMessageAnalyzer
- The catch variable must bind by symbol, not by name. A lambda parameter or local with the same name otherwise resolves to the wrong declaration.
- A conditional access such as ex?.Message holds the receiver on the ConditionalAccessExpressionSyntax node, not on the member binding. The binding case reads the receiver from that parent node.

## CollectionMutationInEnumerationAnalyzer
- An array element write does not change a collection version. As a result, it is legal during enumeration. CollectionMutationInEnumerationAnalyzer exempts element access on an array type.

## ConcurrencyLimitAnalyzer
- A zero initial count is legal for SemaphoreSlim. Only a negative initial count is a defect in ConcurrencyLimitAnalyzer.
- Binding a semantic model is the expensive part of ConcurrencyLimitAnalyzer. The rule binds only for an object creation whose syntactic name is SemaphoreSlim.
- A MaxDegreeOfParallelism value of -1 means unlimited and is valid. ConcurrencyLimitAnalyzer reports only other negative values and values above the ceiling.

## ConfigurationKeyMissingAnalyzer
- A section read uses keys relative to the section, so ConfigurationKeyMissingAnalyzer leaves those reads alone. The absolute key namespace does not describe them.
- A simple assignment to a configuration indexer defines the key. As a result, it is not a read. A compound assignment still reads the key before it writes.
- A cheap name check runs before the semantic lookup because the lookup costs more on every call. Only a configuration method name reaches symbol resolution.
- GetSection is an interface member, while GetRequiredSection is an extension method on ConfigurationExtensions. IsSectionCall must accept both declaring shapes.
- The added-keys option adds keys on top of a parsed settings file and never activates the rule by itself. Without a parsed file the rule cannot distinguish a missing key from a source it cannot see.

## DiskIoCatalog
- FileMethodKinds is the single source for File method membership, and every predicate in DiskIoCatalog derives from that map. A change to the map therefore updates every rule that reads it.

## DisposableFieldInLoopAnalyzer
- A type parameter constrained to IDisposable or IAsyncDisposable makes a field disposable. DisposableFieldInLoopAnalyzer reads the constraint instead of treating the type parameter as unknown.
- A loop source, condition, initializer, or incrementor runs outside the loop body. DisposableFieldInLoopAnalyzer excludes those positions from the per-iteration check.
- A lambda or local function runs on its own schedule. As a result, it forms a scope boundary. An assignment inside one is not a per-iteration write of the enclosing loop.
- Only a dispose inside the same loop releases the value the previous iteration stored. A dispose before the loop releases only the first value.
- A dispose later in the same iteration releases the value the assignment stored. As a result, the next iteration overwrites a disposed value. An exit statement before the dispose skips it on some paths. As a result, the leak still occurs there.

## DuplicateJsonPropertyNameAnalyzer
- One symbol action covers every declaration of a partial type because Roslyn merges partial declarations into one symbol. The rule therefore collects candidates once per merged type.
- IsExcluded models the System.Text.Json contract. Static members, indexers, explicit interface implementations, and ignored members never enter the contract, while a conditional JsonIgnore keeps the member in it.
- JsonIgnoreCondition.Always has the value 1 and is the default when the attribute names no condition. IgnoresAlways treats an absent condition as Always.
- An override or a member that hides a base member by name replaces it in the JSON contract. As a result, the two never coexist. The override chain is walked because the attribute can sit on a virtual ancestor of the base member.

## EventSubscriptionLeakAnalyzer
- A subscription to an event on the same instance dies when that instance dies. EventSubscriptionLeakAnalyzer exempts a non-static event read through this.
- A removal in another type, such as a nested type, does not clean up this subscriber. The removal must appear inside the subscriber type to count.
- A publisher field that the subscriber owns and disposes dies with the subscriber. No unsubscribe is required in that case.
- A static event has one handler list for every caller. As a result, any removal of it counts. An instance event sits on one publisher object. As a result, its removal must name the same receiver.

## FileInfoToctouAnalyzer
- The rule identifies System.IO.File and System.IO.FileSystemInfo by metadata name. Without either symbol the members under test belong to another type. As a result, the rule does not run.
- File.Exists(path) guards the path value passed in, while fileInfo.Exists guards the FileInfo instance. The receiver therefore names the guarded value.
- The hash is computed in the constructor because a seal-side rule bans SymbolEqualityComparer.Default reads inside a custom GetHashCode body. GuardedValue caches the hash at construction.
- A call names no storage location, and two calls to the same helper bind to one method symbol. A call argument therefore cannot identify the guarded value.
- FileInfo.FullName names the same file as fileInfo. As a result, a FileSystemInfo member reduces to the same guarded value as the receiver. Any other member keys on the receiver and member pair, which keeps config.Input and config.Output distinct.

## FileStreamParallelCaptureAnalyzer
- Only a direct anonymous function argument runs on a Parallel worker. A lambda nested deeper, as in a collection factory argument, runs outside the parallel body.
- The call must bind to the framework Parallel type, and the name gate runs first because it costs less than a symbol lookup. ForEachAsync never carries the Parallel name.
- A captured stream is a FileStream local, parameter, or field that the parallel body references by identifier. The walk enters a nested lambda because the closure carries the same position. It skips a local function body because the call site decides when a reference runs.
- A symbol declared inside the parallel lambda belongs to one worker. As a result, the body does not capture it. The lambda node covers its parameter list. As a result, a lambda parameter also counts as declared inside.

## GcHandleFreeAnalyzer
- The bare Alloc call form binds through using static System.Runtime.InteropServices.GCHandle. GcHandleFreeAnalyzer must accept both the qualified and the bare name.
- A lambda or local function body is too small a scope for a GCHandle. The handle outlives that body. As a result, the free search runs over the outermost member body.
- Any use that lets the handle leave the method body makes the handle unreachable to this analysis. Reading the handle in place, such as Target or IsAllocated, does not.

## GeneratedRegexHelpers
- An atomic group never gives back text it matched. As a result, an inner quantifier cannot re-enter it. The group cannot backtrack exponentially and is excluded from the nested-quantifier rule.
- A body that leads with a separator atom different from its trailing atom consumes text the inner quantifier cannot revisit. Every repetition is then disjoint and the group cannot backtrack exponentially.
- A repeated group backtracks exponentially only when its first consuming atom can also match its last. FirstAtomCanAlsoBeLast reads atoms by pattern text, parses a class against a literal, and keeps the flag whenever disjointness is unprovable.
- A shorthand escape and a plain character overlap whenever the shorthand contains the character. A shorthand against a longer atom relies on the class and text comparison paths instead.
- A character class compared against a literal character proves disjointness exactly. Against a dot, a shorthand, or another class the text comparison cannot prove it. As a result, the group stays flagged.
- ClassMatchesChar parses negation, ranges, and escapes, and reports a match for any class shape it cannot model. Reporting a match keeps the caller conservative by leaving the group flagged.
- A closing bracket first in a character class is a literal member, not the class terminator. The scanner advances past it before looking for the close.
- A range starts at a hyphen whose next character is the literal high character before the close. A hyphen before the close stays a literal dash entry.
- A shorthand against another shorthand is assumed to overlap. A shorthand against a class relies on the text comparison because the class membership is not fully modelled.

## GetBufferNotVisibleAnalyzer
- A look-alike type can declare a GetBuffer member. As a result, the name alone does not identify MemoryStream. The symbol must resolve on MemoryStream itself before the rule reports.
- A MemoryStream declaration outside the analyzed body, such as a field initializer, is invisible to the scan. GetBufferNotVisibleAnalyzer therefore cannot prove the buffer hidden for it.
- Only the byte[] constructors of MemoryStream default to a hidden buffer. The empty and capacity constructors expose the buffer, so GetBuffer works on them.
- A non-constant visibility argument may be true at run time. As a result, the rule cannot claim the call throws. Only a proven false constant reports.

## HandRolledProcessCaptureAnalyzer
- An object initializer has no receiver. The visibility flag belongs to the ProcessStartInfo instance the initializer builds, and that instance must reach the process start call.

## HardcodedTmpPathE128Analyzer
- The code fix builds its replacement from PropRelativeSuffix. As a result, the path prefixes live in the analyzer only. This keeps the fix and the analyzer on one source of truth.
- Each temp-path prefix ends with a path separator. The folder names are assembled from parts so the analyzer source never contains a literal temp path that its own rule would flag.

## HttpClientInfiniteTimeoutAnalyzer
- A computed timeout value cannot be proven infinite from source text. IsInfiniteTimeout reads the constant value through the semantic model and compares it with the framework's -1 millisecond marker.
- The infinite marker is exactly -1 milliseconds, and the framework converts the argument without rounding. IsInfiniteDouble compares against double.Epsilon. As a result, only an exact match qualifies.

## HttpClientMissingOceCatchAnalyzer
- TaskCanceledException derives from OperationCanceledException. As a result, a catch of either type handles the cancellation. The rule accepts both catch shapes.
- One walk collects the HttpClient calls, and each try block then tests span containment. Re-walking each try subtree would make the analysis quadratic.
- A custom client type can declare GetAsync and PostAsync too. Only a receiver derived from HttpClient throws the cancellation this rule is about.
- Only System.Exception itself is a broad catch. A derived type is narrower, and a local type that merely shares the name does not derive from System.Exception.

## ImmutableArrayDefaultAnalyzer
- Only the assignment target itself counts as a write. A member access that reads the field on the way to another member does not. A deconstruction target is a tuple. As a result, its elements and nested tuples are walked.
- A ref or out argument lets the callee write the field. ImmutableArrayDefaultAnalyzer treats that argument as an assignment.

## JsonSerializerOptionsMutationAnalyzer
- A new options instance assigned to the same variable starts a fresh, unfrozen object. Earlier mutations of the old instance no longer precede a use of the new one.
- A lambda or local function can run later or never. A call inside one is not a use that precedes the write for JsonSerializerOptionsMutationAnalyzer.

## LazyFieldThreadSafetyAnalyzer
- LazyThreadSafetyMode.None and the isThreadSafe: false overload both mean unsynchronized construction. IsUnsynchronized accepts either form.

## PathCombineSegmentAnalyzer
- The first Path.Combine argument is the base, and a rooted base discards the segments before it. The scan for rooted segments starts at the second argument.

## PoolRentCapacityGuardAnalyzer
- The declaration is resolved through the local's symbol instead of rescanning the method body. That is constant time and cannot match a shadowed name from another scope.

## PooledBufferZeroingAnalyzer
- Only a ZeroMemory statement directly in the finally block runs on every path. A call nested in an if, loop, or lambda may not run. As a result, it does not satisfy the rule.
- Unwrap peels buffer.AsSpan() and MemoryMarshal.AsBytes(x). A slice such as AsSpan(0, n) stays wrapped because it does not span the whole array.

## ProcessLaunchScope
- For this.field, the this symbol is the containing type. As a result, a symbol lookup on this alone would give every field of the type one root. The member access resolves to the field symbol instead.
- A type-qualified chain resolves every static member to the type. The member, not the type, is the root. As a result, two static fields of one type stay distinct.

## ProcessOutputValidatorAnalyzer
- One walk collects both the wait calls and the file reads. The name test inside IsProcessWaitCall rejects almost every invocation before the semantic bind.

## RecordMutableCollectionEqualityAnalyzer
- A record that declares GetHashCode owns its equality, which is the fix the rule asks for. RecordMutableCollectionEqualityAnalyzer stays silent for it.
- A base record that declares GetHashCode owns the equality for derived records. Compiler-generated GetHashCode members are implicit and do not count as ownership.
- A static property never joins record equality. An abstract property has no backing field. As a result, it never joins equality either.

## SemaphoreSlimReleaseAnalyzer
- A lambda or local function runs on its own schedule. As a result, a release inside one may never run at the call site. SemaphoreSlimReleaseAnalyzer treats it as a separate scope.

## StackAllocSafetyAnalyzer
- A guard counts only when its condition tests a name used in the stackalloc length. A guard on an unrelated name does not bound the length.

## SubstringLengthUnderflowAnalyzer
- A condition that reads the string length guards the branch holding the long value. A plain comparison of the length identifies that branch, while any other condition shape guards both branches.
- Length > k and k < length both keep the long string in the then branch. The comparison direction and the side that holds the length decide which branch is guarded.
- A preceding if with no else that returns or throws when the string is short guards every later statement in the block. The rule accepts that early-exit shape.

## TaskCompletionSourceOptionsAnalyzer
- An Interlocked exchange over a field elects one winning caller. As a result, the completion runs at most once. The throwing completion form cannot observe a second caller.
- A TaskCompletionSource constructor with no options parameter always runs continuations inline. The rule therefore treats the absent options list as unsafe.

## TaskFromResultSyncIoAnalyzer
- Each owning type is resolved once per compilation. A symbol compare rejects a user type that shares the metadata name, which a display-string compare would match.
- The await and sync-I/O scans depend only on the enclosing method, not on an individual FromResult call. The result is cached per method across all calls in it.
- The code fix leaves a FromResult inside a lambda or local function alone. Only a FromResult in the method body can therefore be reported.
- The code fix uses the same symbol match predicate as the analyzer. A user type that shares the metadata name never gets a rewrite.

## TextContentGuardAnalyzer
- Span containment answers the ancestry question in constant time. A subtree walk per operand made the && chain analysis quadratic in the chain length.

## UnboundedTaskWhenAllAnalyzer
- Task.WaitAsync(TimeSpan) applies a timeout and is not a concurrency gate. UnboundedTaskWhenAllAnalyzer excludes it from the fan-out check.
- A materializer can sit between the fan-out and the WhenAll, as in Task.WhenAll(items.Select(...).ToArray()). The fan-out is unchanged. As a result, the rule looks through the materializer instead of rejecting the shape.
- The fan-out is the same for an async lambda and for a plain lambda that returns a task. Only the bound return type distinguishes the two.

## UnguardedSplitIndexAnalyzer
- A length check on the local before the index access counts as a guard, and the check may take several shapes. A check after the access cannot guard it, and the rule reports nothing when it cannot model control flow.

## UnregisteredServiceResolveAnalyzer
- GetService<T>() is deliberately absent from ResolveMethodNames. It returns null on a miss. As a result, a null probe is a legitimate existence check rather than a failure.
- HttpClient, HybridCache, IServer, and TracerProvider arrive through non-generic extensions that carry no type argument. The registration collector cannot observe them. As a result, their names are listed in FrameworkProvidedServices.
- The collectors live in the compilation-start closure. Each compilation therefore gets its own state, and the analyzer host cannot leak registrations across compilations.
- Two types that share a short name across namespaces are distinct services. The registration set is keyed on the symbol, not on the name.
- A registration overload taking Type arguments carries open generics such as typeof(IRepo<>). An unbound generic cannot equal a constructed resolve. As a result, that path stays name-keyed.
- The name AddHttpClient belongs to several unrelated types. Matching the declaring type keeps a local helper with the same name from counting as a registration.
- The parameterless CreateClient is an extension method on its own type. As a result, it never matches the interface member. It resolves the default client, which needs no registration.
- The named-client APIs carry exactly one string parameter, the client name. The rule reads the constant of the argument bound to that parameter, which keeps it independent of argument order.
- One diagnostic is reported per unregistered client name, at the first use. A concurrent bag enumerates in no fixed order. As a result, the location is chosen by file and span to keep repeated runs identical.
- The registration set is symbol-keyed, while the open-generic, configured, and framework checks stay name-based. An unbound generic has no constructed symbol, the option arrives as text, and the allowlist holds plain names.

## UseAfterDisposeAnalyzer
- Only a Dispose statement directly in a block runs on every path through that block. A braceless if, else, loop, or switch section runs on some paths only.
- A new assignment ends the disposed state. The scan stops instead of reporting later reads. This holds because those reads see a fresh object.
- A second Dispose is harmless and is not reported. Only a member access reads the disposed state. As a result, nameof, a null test, and a comparison are not uses.
- A nested function body runs at a time the block order does not describe. A local function can run before a Dispose call that precedes its body in source text.

## WhenAnyResultUnobservedAnalyzer
- The name check rejects almost every invocation before the symbol lookup runs. The symbol decides the match. As a result, a user method named WhenAny stays silent.
- The enclosing block is the whole scope of the analysis. Every read of the local in that block hands the task to something that can observe it. As a result, only a local with no read is reported.

Related: [Decision Records](summary.md) explains how to use this page.
