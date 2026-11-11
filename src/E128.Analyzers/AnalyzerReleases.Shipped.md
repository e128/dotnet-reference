## Release 2.8.0

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
E128218 | E128-Design | Info | E128-Design | Info | Recognizes a forwarding accessor pair, a mutable struct, and a cross-type write. Adds `e128_state_min_transition_bodies`.

## Release 2.7.0

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
E128062 | E128-Testing | Warning | E128-Testing | Warning | Recognizes Net110 as a known reference-assembly version, so the rule reports in a net11.0 test project.

## Release 2.6.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
E128218 | E128-Design | Info | Boolean flags encode one state.

## Release 2.5.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
E128203 | Performance | Warning | HTTP request inside a loop creates N+1 round trips
E128204 | Performance | Warning | Database command inside a loop runs one round trip per iteration
E128205 | Performance | Warning | Collection filled in a loop has a known final count
E128206 | Performance | Warning | Empty loop body spins the CPU
E128207 | Performance | Warning | Reflection lookup inside a loop
E128208 | Performance | Warning | Concurrent dictionary lookup done twice
E128209 | Performance | Warning | `GetBytes(...).Length` inside a loop allocates a byte array
E128210 | Performance | Warning | Regex instance created inside a loop
E128211 | Performance | Warning | `Task.Delay` in a loop has no cancellation token
E128212 | Performance | Warning | `ToCharArray` result used only for indexing or iteration
E128213 | Performance | Warning | `Activator.CreateInstance` inside a loop
E128214 | Performance | Warning | HTTP handler does not enable automatic decompression
E128215 | Performance | Warning | `StringBuilder` created without capacity for a known append count
E128216 | Performance | Warning | Unbounded channel can grow without limit
E128217 | Performance | Warning | Large struct passed by value

## Release 2.4.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
E128202 | E128-Security | Info | Unvalidated JWT claims used as an identity.

## Release 2.3.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
E128199 | E128-Maintainability | Info | Direct recursion in a method.
E128200 | E128-Unsafe | Warning | Function pointer type instead of a delegate.
E128201 | E128-Maintainability | Info | Conditional directive count past the ceiling.

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
E128187 | E128-Performance | Info | E128-Performance | Warning | Severity lowered. The default cap is the processor count, not one task per item.
E128197 | E128-Reliability | Info | E128-Reliability | Warning | Severity lowered. A 28 to 31 day span is often a fixed retention window, not a calendar month.

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
E128022 | Design | Warning | Removed. Other rules cover this check.

## Release 2.2.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
E128154 | E128-Reliability | Warning | A singleton registration whose implementation type takes a scoped service in its constructor is reported. The singleton keeps the first scoped instance for the life of the application. A second descriptor on the same id reports an Entity Framework Core DbContext registered as a singleton through AddSingleton, TryAddSingleton, AddKeyedSingleton, a typeof(...) overload, or a constant ServiceLifetime.Singleton lifetime.
E128155 | E128-Reliability | Warning | A call in a foreach body that mutates the collection the loop enumerates is reported. The enumerator throws InvalidOperationException on the next step.
E128156 | E128-Security | Error | An HttpClientHandler certificate validation callback that accepts every certificate is reported. A callback that reads the chain or the policy errors is not reported.
E128157 | E128-Security | Warning | An equality, inequality, or SequenceEqual comparison over a secret-derived byte sequence is reported. Use CryptographicOperations.FixedTimeEquals instead.
E128159 | E128-Reliability | Warning | A TaskCompletionSource created without TaskCreationOptions.RunContinuationsAsynchronously is reported. A computed options value is not reported. A second descriptor on the same id reports SetResult, SetException, or SetCanceled on a source stored in a field.
E128160 | E128-Reliability | Warning | A WaitForExit call in a method that redirects a process stream and never reads it is reported. The child blocks once the pipe buffer fills.
E128161 | E128-Reliability | Warning | A Path.Combine call with a constant rooted or parent segment is reported. A rooted segment discards every earlier segment.
E128162 | E128-Reliability | Warning | A read of a local after the same block calls Dispose on it is reported. The object throws ObjectDisposedException on the next member call that reads its state.
E128163 | E128-Reliability | Warning | An ImmutableArray field that no initializer and no assignment reaches is reported. The default array throws NullReferenceException on read.
E128164 | E128-Security | Warning | A string literal that starts with http:// or https:// and carries a host is reported outside a test compilation. Attribute arguments, namespace and schema hosts, and example hosts are not reported. A property initializer on an Options type and a const or static readonly field are not reported; a local const inside a method is.
E128165 | E128-Security | Warning | A credential-named string literal of 8 or more characters, and a connection string that holds a password, are reported outside a test compilation. An empty value, a format placeholder, and an uppercase value joined by underscores are not reported.
E128166 | E128-Security | Warning | An ILogger call that passes a value, or an interpolated hole, named for a credential or personal-data word is reported outside a test compilation. Cancellation tokens and metadata names such as TokenCount are not reported.
E128167 | E128-Style | Warning | A TODO, FIXME, or HACK comment that does not match e128_tracked_ref_pattern is reported. The default pattern accepts AB#123 and #123.
E128168 | E128-Testing | Warning | A Fact or Theory Skip reason that does not match e128_tracked_ref_pattern is reported.
E128169 | E128-Style | Warning | An Obsolete attribute with no message, or an empty message, is reported.
E128170 | E128-Reliability | Warning | A constant index of 1 or more taken from a string.Split result with no Length read in the member is reported. Index 0 and from-end indexes are not reported.
E128171 | E128-Testing | Warning | An xUnit test whose whole body is one Assert.Fail call is reported.
E128172 | E128-Testing | Warning | A catch of Exception or XunitException with no rethrow, around a try block that calls Assert, is reported.
E128173 | E128-Style | Warning | A SuppressMessage or ExcludeFromCodeCoverage attribute with no Justification, or a blank one, is reported.
E128174 | E128-Design | Warning | A constructor or primary constructor with more class or interface parameters than e128_max_ctor_dependencies is reported. The default ceiling is 7.
E128175 | E128-Reliability | Warning | A SemaphoreSlim.Wait or WaitAsync call with no later try/finally that calls Release on the same semaphore is reported. The plain and CancellationToken overloads are checked. A timeout overload and a wait inside a releasing try are not reported.
E128176 | E128-Reliability | Warning | An event subscription in a disposable type whose handler reads the subscriber instance is reported when no Dispose or DisposeAsync method removes the same event. A static handler and a handler that reads only a parameter or a local are not reported.
E128177 | E128-Reliability | Warning | A record member with a mutable collection type is reported, because the generated equality compares it by reference. An immutable collection, an abstract property, a computed property, a static member, and a const field are not reported.
E128178 | E128-Reliability | Error | Two members of one type that map to the same JsonPropertyName value are reported. An ignored member, an indexer, a static member, and an override of the base member are not reported. The rule stays off when System.Text.Json is absent from the compilation.
E128179 | E128-Design | Warning | An AddOptions or Configure registration that binds configuration with no validation call is reported. A bare AddOptions, the delegate overload of Configure, and PostConfigure are not reported.
E128180 | E128-Performance | Warning | An Entity Framework Core materializing call inside a loop body is reported. A call in a foreach source expression, a for initializer, or with no enclosing loop is not reported. The rule stays off when EF Core is absent from the compilation.
E128181 | E128-Reliability | Warning | An assignment of Timeout.InfiniteTimeSpan or TimeSpan.FromMilliseconds(-1) to HttpClient.Timeout is reported. A computed value is not reported.
E128182 | E128-Reliability | Warning | A second enumeration of one IAsyncEnumerable value in one method body is reported. An inline-created sequence, an EnumeratorCancellation parameter, and an iterator with yield are not reported.
E128183 | E128-Reliability | Warning | A Task.WhenAny result that is discarded or stored in a local that the block never reads is reported. An awaited, returned, stored, or passed result is not reported.
E128184 | E128-Security | Info | A recursive Directory.Delete or DirectoryInfo.Delete is reported when the path is a parameter, a member read, or a Path.Combine over either. A path under Path.GetTempPath, a literal, and a StartsWith guard are not reported.
E128185 | E128-Performance | Warning | A SaveChanges or SaveChangesAsync call on an Entity Framework Core context inside a loop body is reported. A call in a foreach source expression, a for initializer, or with no enclosing loop is not reported. The rule stays off when EF Core is absent from the compilation.
E128186 | E128-Reliability | Warning | An await expression inside a finally block is reported. An await in a try block, a catch clause, a local function, or a lambda declared in the finally body is not reported, and neither is an await inside a nested try.
E128187 | E128-Performance | Warning | A Parallel.ForEachAsync call with no ParallelOptions argument, or with an inline initializer that sets no MaxDegreeOfParallelism, is reported. Options held in a variable, a parameter, a field, or a factory call are not reported.
E128188 | E128-Reliability | Warning | A catch filter that reads Message, StackTrace, or ToString on the caught exception is reported. A filter that reads HResult or InnerException, or reads the member on another object, is not reported.
E128189 | E128-Reliability | Warning | A GCHandle.Alloc result stored in a local with no Free call in the same body is reported. A local that is passed to a method, returned, or stored in a field or a collection is not reported.
E128190 | E128-Performance | Warning | A FileStream construction with no asynchronous switch is reported: useAsync omitted or false, and no FileOptions.Asynchronous. A useAsync value of true, an options value with Asynchronous, a non-constant argument, and the SafeFileHandle overload are not reported.
E128191 | E128-Security | Warning | An X509Certificate2 PFX constructor or an X509CertificateLoader.LoadPkcs12 call is reported when the flags omit EphemeralKeySet. A constant flags value with EphemeralKeySet, a non-constant flags value, a byte array constructor, and a constructor with no flags parameter are not reported.
E128192 | E128-Reliability | Warning | A MemoryStream.GetBuffer call is reported when the receiver was created without publiclyVisible: true. A creation with publiclyVisible: true, a non-constant flag, and a receiver that is a field, a property, or a method result are not reported.
E128193 | E128-Reliability | Warning | An assignment to a disposable field inside a loop with no earlier dispose call on that field in that loop is reported. A disposal before the assignment, a local, a parameter, a property, a compound assignment, and an assignment outside a loop are not reported.
E128194 | E128-Reliability | Warning | A Lazy<T> field initialized with LazyThreadSafetyMode.None is reported. An omitted mode, ExecutionAndPublication, PublicationOnly, a non-constant mode, and a local, parameter, or property initializer are not reported.
E128195 | E128-Reliability | Warning | A constant configuration key read through IConfiguration, GetSection, or GetConnectionString is reported when no JSON additional file defines the key. The rule stays off when no parsable JSON additional file is present. A key in the e128_added_configuration_keys option is accepted.
E128196 | E128-Reliability | Warning | A write to a JsonSerializerOptions member after the same body passes the instance to JsonSerializer is reported. The serializer freezes the instance on first use, so the write throws InvalidOperationException.
E128197 | E128-Reliability | Warning | A TimeSpan.FromDays call with a 28 to 31 day constant added to or subtracted from a DateTime or DateTimeOffset is reported. A calendar month has no fixed day count. A span held in a variable is not reported.
E128198 | E128-Reliability | Warning | A two-argument string.Substring call whose count is a Length value minus a positive constant is reported. A short string makes the count negative, so the call throws ArgumentOutOfRangeException. A length test on the same string in an enclosing condition clears the report.

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
E128001 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128002 | E128-Style | Warning | Style | Warning | The category gains the E128- prefix, so category-E128-Style selects the rules of one area.
E128003 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128004 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128005 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128006 | E128-Style | Warning | Style | Warning | The category gains the E128- prefix, so category-E128-Style selects the rules of one area.
E128007 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128008 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128009 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128010 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128011 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128012 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128013 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128014 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128015 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128016 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128017 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128018 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128019 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128020 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128021 | E128-Design | Error | Design | Error | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128023 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128024 | E128-Style | Warning | Style | Warning | The category gains the E128- prefix, so category-E128-Style selects the rules of one area.
E128025 | E128-Style | Warning | Style | Warning | The category gains the E128- prefix, so category-E128-Style selects the rules of one area.
E128026 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128027 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128028 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128029 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128030 | E128-Design | Info | Design | Info | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128031 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128032 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128033 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128034 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128035 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128036 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128037 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128038 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128039 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128040 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128041 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128042 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128043 | E128-Style | Warning | Style | Warning | The category gains the E128- prefix, so category-E128-Style selects the rules of one area.
E128044 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128045 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128046 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128047 | E128-Style | Warning | Style | Warning | The category gains the E128- prefix, so category-E128-Style selects the rules of one area.
E128048 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128049 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128050 | E128-Design | Error | Design | Error | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128051 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128052 | E128-Design | Info | Design | Info | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128053 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128054 | E128-Testing | Warning | Testing | Warning | The category gains the E128- prefix, so category-E128-Testing selects the rules of one area.
E128055 | E128-Style | Warning | Style | Warning | The category gains the E128- prefix, so category-E128-Style selects the rules of one area.
E128056 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128057 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128058 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128059 | E128-Design | Error | Design | Error | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128060 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128061 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128062 | E128-Testing | Warning | Testing | Warning | The category gains the E128- prefix, so category-E128-Testing selects the rules of one area.
E128063 | E128-Style | Error | Style | Error | The category gains the E128- prefix, so category-E128-Style selects the rules of one area.
E128064 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128065 | E128-Style | Warning | Style | Warning | The category gains the E128- prefix, so category-E128-Style selects the rules of one area.
E128066 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128067 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128068 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128069 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128070 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128071 | E128-Security | Warning | Security | Warning | The category gains the E128- prefix, so category-E128-Security selects the rules of one area.
E128072 | E128-Performance | Info | Performance | Info | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128073 | E128-Testing | Warning | Testing | Warning | The category gains the E128- prefix, so category-E128-Testing selects the rules of one area.
E128074 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128075 | E128-Security | Warning | Security | Warning | The category gains the E128- prefix, so category-E128-Security selects the rules of one area.
E128076 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128077 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128078 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128079 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128080 | E128-Design | Error | Design | Error | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128081 | E128-Performance | Info | Performance | Info | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128082 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128083 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128084 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128085 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128086 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128087 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128088 | E128-Maintainability | Warning | Maintainability | Warning | The category gains the E128- prefix, so category-E128-Maintainability selects the rules of one area.
E128089 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128090 | E128-Maintainability | Info | Maintainability | Info | The category gains the E128- prefix, so category-E128-Maintainability selects the rules of one area.
E128091 | E128-Security | Warning | Security | Warning | The category gains the E128- prefix, so category-E128-Security selects the rules of one area.
E128092 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128093 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128094 | E128-Style | Error | Style | Error | The category gains the E128- prefix, so category-E128-Style selects the rules of one area.
E128096 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128097 | E128-Testing | Warning | Testing | Warning | The category gains the E128- prefix, so category-E128-Testing selects the rules of one area.
E128098 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128099 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128100 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128101 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128102 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128103 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128104 | E128-Testing | Warning | Testing | Warning | The category gains the E128- prefix, so category-E128-Testing selects the rules of one area.
E128105 | E128-Testing | Warning | Testing | Warning | The category gains the E128- prefix, so category-E128-Testing selects the rules of one area.
E128106 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128107 | E128-Testing | Info | Testing | Info | The category gains the E128- prefix, so category-E128-Testing selects the rules of one area.
E128108 | E128-Testing | Warning | Testing | Warning | The category gains the E128- prefix, so category-E128-Testing selects the rules of one area.
E128109 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128110 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128111 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128112 | E128-Unsafe | Warning | Unsafe | Warning | The category gains the E128- prefix, so category-E128-Unsafe selects the rules of one area.
E128113 | E128-Unsafe | Warning | Unsafe | Warning | The category gains the E128- prefix, so category-E128-Unsafe selects the rules of one area.
E128114 | E128-Unsafe | Warning | Unsafe | Warning | The category gains the E128- prefix, so category-E128-Unsafe selects the rules of one area.
E128115 | E128-Unsafe | Warning | Unsafe | Warning | The category gains the E128- prefix, so category-E128-Unsafe selects the rules of one area.
E128116 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128117 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128118 | E128-Unsafe | Warning | Unsafe | Warning | The category gains the E128- prefix, so category-E128-Unsafe selects the rules of one area.
E128119 | E128-Unsafe | Warning | Unsafe | Warning | The category gains the E128- prefix, so category-E128-Unsafe selects the rules of one area.
E128120 | E128-Unsafe | Warning | Unsafe | Warning | The category gains the E128- prefix, so category-E128-Unsafe selects the rules of one area.
E128121 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128122 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128123 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128124 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128125 | E128-Unsafe | Warning | Unsafe | Warning | The category gains the E128- prefix, so category-E128-Unsafe selects the rules of one area.
E128126 | E128-Unsafe | Warning | Unsafe | Warning | The category gains the E128- prefix, so category-E128-Unsafe selects the rules of one area.
E128127 | E128-Unsafe | Warning | Unsafe | Warning | The category gains the E128- prefix, so category-E128-Unsafe selects the rules of one area.
E128128 | E128-Unsafe | Warning | Unsafe | Warning | The category gains the E128- prefix, so category-E128-Unsafe selects the rules of one area.
E128129 | E128-Unsafe | Warning | Unsafe | Warning | The category gains the E128- prefix, so category-E128-Unsafe selects the rules of one area.
E128131 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128132 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128133 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128134 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128135 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128136 | E128-Unsafe | Warning | Unsafe | Warning | The category gains the E128- prefix, so category-E128-Unsafe selects the rules of one area.
E128137 | E128-Unsafe | Warning | Unsafe | Warning | The category gains the E128- prefix, so category-E128-Unsafe selects the rules of one area.
E128138 | E128-Unsafe | Warning | Unsafe | Warning | The category gains the E128- prefix, so category-E128-Unsafe selects the rules of one area.
E128139 | E128-Unsafe | Warning | Unsafe | Warning | The category gains the E128- prefix, so category-E128-Unsafe selects the rules of one area.
E128140 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128141 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.
E128142 | E128-Security | Error | Security | Error | The category gains the E128- prefix, so category-E128-Security selects the rules of one area.
E128143 | E128-Security | Error | Security | Error | The category gains the E128- prefix, so category-E128-Security selects the rules of one area.
E128144 | E128-Security | Error | Security | Error | The category gains the E128- prefix, so category-E128-Security selects the rules of one area.
E128145 | E128-Security | Error | Security | Error | The category gains the E128- prefix, so category-E128-Security selects the rules of one area.
E128146 | E128-Security | Error | Security | Error | The category gains the E128- prefix, so category-E128-Security selects the rules of one area.
E128147 | E128-Security | Error | Security | Error | The category gains the E128- prefix, so category-E128-Security selects the rules of one area.
E128148 | E128-Security | Error | Security | Error | The category gains the E128- prefix, so category-E128-Security selects the rules of one area.
E128149 | E128-Security | Error | Security | Error | The category gains the E128- prefix, so category-E128-Security selects the rules of one area.
E128150 | E128-Security | Error | Security | Error | The category gains the E128- prefix, so category-E128-Security selects the rules of one area.
E128151 | E128-Security | Error | Security | Error | The category gains the E128- prefix, so category-E128-Security selects the rules of one area.
E128152 | E128-Performance | Warning | Performance | Warning | A coordinate step adds no loop depth. An inner loop whose trip count reads an element the enclosing loop produced, such as a cell inside a row, no longer counts against that enclosing loop.
E128153 | E128-Performance | Warning | Performance | Warning | The category gains the E128- prefix, so category-E128-Performance selects the rules of one area.

## Release 1.67.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|------
E128153 | Performance | Warning  | List.RemoveAt(0) in loop creates O(n²) complexity

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
E128152 | Performance  | Warning      | Performance  | Warning      | Loop depth counts only varying trip counts, `cubic` becomes a tier, recursion is classified by argument contraction, and memoization needs a read and a write.

## Release 1.66.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|------
E128142 | Security    | Error    | Do not instantiate a Managed or CNG cryptography type
E128143 | Security    | Error    | Use an RSA key of at least 3072 bits or an approved curve
E128144 | Security    | Error    | Use an authenticated cipher mode
E128145 | Security    | Error    | Use a strong PBKDF2 configuration
E128146 | Security    | Error    | Use TLS 1.2 or later
E128147 | Security    | Error    | RNGCryptoServiceProvider is deprecated
E128148 | Security    | Error    | Do not use a cipher excluded by policy
E128149 | Security    | Error    | Use the full 16-byte AES-GCM tag
E128150 | Security    | Error    | Do not use SHA-3, SHAKE or KMAC
E128151 | Security    | Error    | Use an HMAC key at least as long as the hash output
E128152 | Performance | Warning  | Flags complexity past the e128_max_complexity ceiling: nested loops depth 2+ and non-memoized recursion

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
E128071 | Security     | Error        | Security     | Warning      | FIPS cryptography policy. A broken algorithm is a build-breaking defect.
E128075 | Security     | Error        | Security     | Warning      | FIPS cryptography policy. A predictable RNG in a security context is a build-breaking defect.

## Release 1.60.4

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|------
E128131 | Performance | Warning  | Parse UTF-8 bytes with the span overload instead of a decoded string
E128132 | Performance | Warning  | Do not use a culture comparer for a dictionary or hash set key
E128133 | Performance | Warning  | Use an alternate lookup instead of a temporary string key
E128134 | Performance | Warning  | Use BitOperations.PopCount instead of a shift loop
E128135 | Performance | Warning  | Use TensorPrimitives.Sum instead of a sum loop
E128136 | Unsafe      | Warning  | Do not pair a pointer parameter with a separate length parameter
E128137 | Unsafe      | Warning  | Do not bind to a non-public BCL member through reflection
E128138 | Unsafe      | Warning  | Do not build dynamic methods with raw IL
E128139 | Unsafe      | Warning  | Do not move a non-blittable type through an unaligned access
E128140 | Performance | Warning  | Use a span bulk method instead of an element loop
E128141 | Performance | Warning  | Use a u8 literal instead of Encoding.UTF8.GetBytes

## Release 1.42.1

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|------
E128112 | Unsafe      | Warning  | Use Unsafe.BitCast instead of Unsafe.As
E128113 | Unsafe      | Warning  | Use the unaligned Unsafe read and write methods
E128114 | Unsafe      | Warning  | Index the array instead of bypassing its bounds check
E128115 | Unsafe      | Warning  | Do not reinterpret between bool and int
E128116 | Performance | Warning  | Do not use MethodImplOptions.AggressiveOptimization
E128117 | Performance | Warning  | Use LibraryImport instead of DllImport
E128118 | Unsafe      | Warning  | Do not reinterpret bytes as a non-blittable type
E128119 | Unsafe      | Warning  | Use an inline array instead of a fixed-size buffer
E128120 | Unsafe      | Warning  | Do not skip local initialization without a measured reason
E128121 | Performance | Warning  | Use SequenceEqual instead of an element-by-element loop
E128122 | Performance | Warning  | Guard an expensive log argument with IsEnabled
E128123 | Performance | Warning  | Use GeneratedRegex for a constant pattern
E128124 | Performance | Warning  | Cache SearchValues in a static readonly field
E128125 | Unsafe      | Warning  | Pin the object explicitly with fixed
E128126 | Unsafe      | Warning  | Do not write to a string through a fixed pointer
E128127 | Unsafe      | Warning  | Do not create a null managed reference
E128128 | Unsafe      | Warning  | Keep the delegate alive while native code holds its function pointer
E128129 | Unsafe      | Warning  | Do not bulk-copy a type that holds references

## Release 1.42.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|------
E128109 | Reliability | Warning  | stackalloc must be bounded and outside loops
E128110 | Reliability | Warning  | Zero a pooled buffer in a finally block before Return
E128111 | Reliability | Warning  | Pooled buffer used after Return

## Release 1.41.12

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
E128107 | Testing      | Info         | Testing      | Info         | Disabled by default; predicates recalibrated (DuplicateCoverage keys on the whole test body, SelfFulfillingExpected requires same-expression operands, NoAssertion exempts a void-returning production call)
E128108 | Testing      | Warning      | Testing      | Warning      | Disabled by default; predicates recalibrated (DuplicateCoverage keys on the whole test body, SelfFulfillingExpected requires same-expression operands, NoAssertion exempts a void-returning production call)

## Release 1.41.3

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
E128107 | Testing  | Info     | Test matches one low-value test condition
E128108 | Testing  | Warning  | Test matches two or more low-value test conditions

## Release 1.41.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|------
E128101 | Reliability | Warning  | Verify process output file exists after WaitForExit before reading
E128102 | Performance | Warning  | Use SearchValues<char> for IndexOfAny scans
E128103 | Reliability | Warning  | Resolve a service type that has no registration in the compilation
E128104 | Testing     | Warning  | Assert call whose operands are all literals
E128105 | Testing     | Warning  | Test method calls an out-of-repo resolver without a skip guard
E128106 | Performance | Warning  | Index a collection with a bound taken from a different collection

## Release 1.39.0

### New Rules

Rule ID | Category        | Severity | Notes
--------|-----------------|----------|------
E128094 | Style           | Error    | Pragma warning disable placed before a file-scoped namespace declaration
E128097 | Testing         | Warning  | Comment inside a type that holds an xUnit Fact or Theory member
E128088 | Maintainability | Warning  | Switch over an enum does not case every member and has no default arm

## Release 1.38.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|------
E128093 | Reliability | Warning  | Use async DbConnection/DbCommand *Async overload instead of sync method (outside already-async methods)
E128096 | Reliability | Warning  | Detect synchronous call to async local function via .Result or .Wait()
E128098 | Performance | Warning  | Detect chained string Replace calls
E128099 | Reliability | Warning  | Detect process exit waits without a timeout
E128100 | Reliability | Warning  | Catch filter reads negated token state on OperationCanceledException

## Release 1.35.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|------
E128091 | Security    | Warning  | ProcessStartInfo.Arguments assigned a built string instead of ArgumentList
E128092 | Reliability | Warning  | Use async File.*Async overload instead of sync method (outside already-async methods)

## Release 1.31.0

### New Rules

Rule ID | Category        | Severity | Notes
--------|-----------------|----------|------
E128076 | Reliability     | Warning  | Materialize QuerySelectorAll result before iterating
E128077 | Reliability     | Warning  | TextContent string match requires a preceding length guard
E128078 | Reliability     | Warning  | GetAttribute("href") on element that does not carry href
E128079 | Reliability     | Warning  | CompositeDetection with single generic ID selector lacks specificity
E128080 | Design          | Error    | Use ByteSize for data-size values to avoid unit ambiguity
E128081 | Performance     | Info     | Use StringBuilderPool instead of new StringBuilder()
E128082 | Design          | Warning  | Do not unwrap ByteSize via cast
E128083 | Performance     | Warning  | Use ImmutableCollectionsMarshal.AsImmutableArray instead of ImmutableArray.Create(x.ToArray())
E128084 | Performance     | Warning  | Use CollectionsMarshal.AsSpan with Slice instead of List.GetRange
E128085 | Performance     | Warning  | Use foreach+AddRange instead of SelectMany.ToList
E128086 | Reliability     | Warning  | ArrayPool buffer used as SqliteParameter value without Size
E128087 | Design          | Warning  | Static numeric field should not be incremented with ++/--
E128089 | Reliability     | Warning  | Bare .Parse() call without TryParse
E128090 | Maintainability | Info     | Reflection in test methods — use public API instead

## Release 1.26.1

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|-------
E128066 | Performance | Warning  | Linear lookup inside loop creates O(n²) complexity
E128067 | Performance | Warning  | String concatenation in loop creates O(n²) allocations
E128068 | Performance | Warning  | Sort operation inside loop creates O(n² log n) complexity
E128069 | Performance | Warning  | List.Insert(0, ...) in loop creates O(n²) complexity
E128070 | Reliability | Warning  | Pool Rent() capacity must be bounded
E128071 | Security    | Warning  | Use a FIPS-approved hash algorithm
E128072 | Performance | Info     | Prefer SHA256.HashData() over SHA256.Create()
E128073 | Testing     | Warning  | Test method missing [Trait("Category", ...)] attribute
E128074 | Design      | Warning  | Readonly struct property should use init accessor
E128075 | Security    | Warning  | Use RandomNumberGenerator instead of Random in crypto context

## Release 1.23.3

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|-------
E128055 | Style       | Warning  | Unbalanced pragma warning disable — disable without matching restore
E128056 | Reliability | Warning  | FileInfo.Exists TOCTOU race — check then read without try/catch
E128057 | Reliability | Warning  | Unprotected cleanup in finally block — File/Directory.Delete without try/catch
E128058 | Design      | Warning  | List<T> returned as IReadOnlyList<T> without .AsReadOnly()
E128059 | Design      | Error    | Interface method parameter unused in implementation
E128060 | Design      | Warning  | Dictionary<K,V> returned as IReadOnlyDictionary<K,V> without .AsReadOnly()
E128061 | Design      | Warning  | Static readonly array should be ImmutableArray<T>
E128062 | Testing     | Warning  | Test uses outdated ReferenceAssemblies — does not match project target framework
E128063 | Style       | Error    | Private static member name contains a mid-name underscore
E128064 | Reliability | Warning  | Disk write-then-read round-trip — use the in-memory value instead of reading back from disk
E128065 | Style       | Warning  | Pragma bundling — #pragma warning disable with multiple IDs; use one pragma per ID

## Release 1.8.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
E128054 | Testing  | Warning  | Class creates temp directory without cleanup interface

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
E128003 | Design       | Warning      | Reliability  | Warning      | Recategorized to match Design/ directory
E128004 | Design       | Warning      | Reliability  | Warning      | Recategorized to match Design/ directory

## Release 1.6.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|-------
E128031 | Reliability | Warning  | AddSingleton factory returns IDisposable
E128032 | Design      | Warning  | Concrete-only DI registration with available interface
E128033 | Reliability | Warning  | Options class bound via .Bind() has init-only property
E128034 | Reliability | Warning  | Constructor news a DI-registered type
E128035 | Reliability | Warning  | Concrete-type DI dependency without direct registration
E128036 | Design      | Warning  | Task.Run wrapping async lambda — unnecessary thread pool hop
E128037 | Reliability | Warning  | Unbounded Task.WhenAll over async Select
E128038 | Reliability | Warning  | Task.WhenAll async lambda missing CancellationToken propagation
E128039 | Reliability | Warning  | Catch filter must exclude OperationCanceledException
E128040 | Reliability | Warning  | Concurrency limit must be positive
E128041 | Reliability | Warning  | JsonDocument.RootElement must not escape the document's using scope
E128042 | Design      | Warning  | Convert.ToInt32/ToInt64 wrapping ExecuteScalar without null guard
E128043 | Style       | Warning  | Do not use the null-forgiving operator
E128044 | Design      | Warning  | Type implements IAsyncDisposable but not IDisposable
E128045 | Design      | Warning  | Avoid direct System.Console usage
E128046 | Design      | Warning  | Class has excessive user-defined inheritance depth
E128047 | Style       | Warning  | #pragma warning disable without justification comment
E128048 | Design      | Warning  | Use switch instead of if/else-if chain on enum values
E128049 | Design      | Warning  | Avoid [DynamicallyAccessedMembers] attribute
E128050 | Design      | Error    | Use TimeSpan for duration values
E128051 | Reliability | Warning  | HttpClient missing OperationCanceledException catch
E128052 | Design      | Info     | Mutable collection on public API
E128053 | Design      | Warning  | Collection-of-string path parameter

## Release 1.5.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|-------
E128027 | Performance | Warning  | Use FrozenSet/FrozenDictionary for static readonly collections
E128028 | Reliability | Warning  | Task.FromResult wraps sync I/O that has an async alternative
E128029 | Performance | Warning  | Replace multi-string OR-chain with HashSet.Contains
E128030 | Design      | Info     | Do not compare FileSystemInfo types by reference

## Release 1.4.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|-------
E128019 | Design      | Warning  | Do not pass CancellationToken by 'in' reference
E128020 | Reliability | Warning  | Do not use 'in' modifier with mutable structs
E128021 | Design      | Error    | Do not use 'in' modifier with ref struct parameters
E128022 | Design      | Warning  | Remove ConfigureAwait(false) in application code
E128023 | Reliability | Warning  | Avoid hardcoded /tmp path — use Path.GetTempPath()
E128024 | Style       | Warning  | Non-XML-doc comment above method declaration
E128025 | Style       | Warning  | Guid.NewGuid() used in temp file path — use GetRandomFileName
E128026 | Performance | Warning  | Redundant HashSet allocation in FrozenSet creation

## Release 1.3.5

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|-------
E128015 | Performance | Warning  | Use string interpolation instead of string.Format
E128016 | Reliability | Warning  | DateTime.Parse/ParseExact missing DateTimeStyles parameter
E128017 | Design      | Warning  | Use primary constructor parameter directly
E128018 | Performance | Warning  | Use ToArray() instead of ToList() for read-only foreach iteration

## Release 1.3.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|-------
E128011 | Reliability | Warning  | [GeneratedRegex] attribute is missing matchTimeoutMilliseconds
E128012 | Reliability | Warning  | RegexOptions.Compiled is redundant in [GeneratedRegex]
E128013 | Reliability | Warning  | [GeneratedRegex] pattern has overlapping quantifiers
E128014 | Reliability | Warning  | [GeneratedRegex] pattern has nested quantifiers

## Release 1.2.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|-------
E128009 | Performance | Warning  | Use MinBy/MaxBy instead of OrderBy().First()
E128010 | Performance | Warning  | HttpClient call missing HttpCompletionOption.ResponseHeadersRead

## Release 1.1.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|-------
E128006 | Style       | Warning  | Use Encoding.UTF8 instead of Encoding.Default
E128007 | Design      | Warning  | Avoid async void methods (non-event-handler)
E128008 | Design      | Warning  | Avoid sync-over-async (.Result / .GetAwaiter().GetResult())

## Release 1.0.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|-------
E128001 | Design      | Warning  | Use FileInfo or DirectoryInfo instead of string for file system paths
E128002 | Style       | Warning  | Use string.Empty instead of ""
E128003 | Reliability | Warning  | Use TimeProvider instead of DateTime/DateTimeOffset direct access
E128004 | Reliability | Warning  | Use IHttpClientFactory instead of new HttpClient()
E128005 | Design      | Warning  | Seal classes that have no subclasses
