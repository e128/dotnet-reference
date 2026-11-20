# Analyzer Decisions: Design
*Updated: 2026-11-20T17:16:51Z*

Rationale removed from source comments. Each section names the type that carried it.
Read this page before you change or delete the matching check.

## ByteSizeForDataSizeAnalyzer
- ByteSizeForDataSizeAnalyzer.AnalyzeSymbol returns early when PartialMembers.IsImplementationPart is true. Roslyn raises a symbol action twice for a partial member, once per declaring part. As a result, reporting both would duplicate the diagnostic. Keep the guard so only the defining part reports.

## ClosedHierarchy
- Reports a type only when every derived type lives in the same assembly as the base. A derived type in a second assembly means the hierarchy is open, and adding the closed modifier would break that derivation.

## ConcreteOnlyDiRegistrationAnalyzer
- Resolves its search scope from the enclosing block and falls back to the compilation unit. Top-level statements have no enclosing block. As a result, the fallback is the only way the forwarding and typed-client checks see them.
- Joins interface names with string.Join and skips a single-element branch. string.Join returns a single element unchanged. As a result, a Count == 1 branch would add code without changing the output.
- Passes the fully qualified interface name as the suggested type argument. The code fix writes that text verbatim into Add{Lifetime}<IFoo, T>. As a result, an unqualified name would not compile at the call site.
- ConcreteOnlyDiRegistrationAnalyzer.ScopeContainsGenericCall matches the call shape directly. A text search rebuilt the whole scope text for every flagged registration, which was quadratic on large blocks and allocated the full scope string.

## ConstructorDependencyCeilingAnalyzer
- Reads the configured ceiling once per syntax tree during compilation start. Each tree carries its own analyzer config. As a result, the ceiling can differ per file and must not be cached across trees.
- Registers ConstructorDeclaration but not a record primary constructor. Record primary constructor parameters declare data members rather than injected services. As a result, counting them would produce false reports.
- ConstructorDependencyCeilingAnalyzer.IsDependency rejects special types, records, arrays, structs, enums, and delegates through the type-kind check. The filter relies on TypeKind to exclude a params array. As a result, no separate array check is needed.

## DateTimeDirectUseAnalyzer
- Skips a member access inside a static field initializer. TimeProvider cannot be injected into a static initializer, and patterns such as a static readonly baseline field are deliberate. As a result, the rule must not fire there.

## ImplicitStateMachineAnalyzer
- Reads analyzer options per type, using the type's syntax tree. A project-level .editorconfig applies through the tree. As a result, reading global options only would ignore per-project configuration.
- Treats a getter and setter that only forward one field as a single canonical member. The field is the real state carrier. As a result, counting the property pair as well would double-count one storage location.
- ImplicitStateMachineAnalyzer.IsAccessorBody excludes property accessors from transition detection. An accessor body is storage for a property rather than a transition. As a result, treating it as state evidence would misclassify plain properties.

## MutableCollectionCatalog
- MutableCollectionCatalog.Entry is a plain class because the analyzer assembly targets netstandard2.0, which has no IsExternalInit type. A record would require that type and fail to compile there.

## MutableCollectionExposureAnalyzer
- MutableCollectionExposureAnalyzer.AnalyzeMethod returns early for the implementation part of a partial member. Roslyn raises the symbol action once per declaring part. As a result, the guard prevents a duplicate diagnostic.
- Runs the interface-implementation walk only after every cheaper gate has passed. The walk resolves each interface member and is the expensive step. As a result, running it for every candidate method would slow the analyzer.
- MutableCollectionExposureAnalyzer.AnalyzeProperty returns early for the implementation part of a partial member. Roslyn raises the symbol action once per declaring part. As a result, the guard prevents a duplicate diagnostic.
- Treats an init-only setter as immutable. An init-only accessor can only run during construction. As a result, a property with one does not expose mutable state and must not be skipped.

## OptionsValidationMissingAnalyzer
- Resolves Configure<T>(IConfiguration) on OptionsConfigurationServiceCollectionExtensions. That method lives in a separate extension class and assembly from AddOptions<T> and Configure<T>(Action<T>). As a result, a lookup on the DI extensions type alone misses it.
- Resolves validation methods on OptionsBuilder<T> and on four extension classes. The owning assembly differs by method. As a result, the rule matches every candidate type instead of trusting the method name.
- Turns itself off when neither registration extension type resolves. Without the framework type there is no symbol to match, and falling back to the method name would report on unrelated code.
- Walks only the outermost call of a fluent chain. A validation call that follows the registration sits deeper in the same chain. As a result, starting at an inner node would miss it.
- Skips a registration when an IValidateOptions<T> for the same options type is registered elsewhere. That validator runs for the bound instance. As a result, the options are validated even without a call in the chain.
- Keeps the bind flag of the outermost registration. A chain can hold more than one registration, and the outermost one is the rule's subject. As a result, its bind state decides the report.
- Treats a bare AddOptions<T>() as needing a later Bind call. The parameterless overload carries no configuration source. As a result, the chain must bind one before the options are considered configured.
- Accepts AddOptions<T>() with no arguments or with only the service collection. That parameterless form is the registration a Bind call follows, while the delegate form configures values in code and is outside the rule.
- Treats Configure<T>(IConfiguration) as binding a configuration source. The configuration parameter is not always last. As a result, the rule searches all parameters instead of assuming a position. Configure<T>(Action<T>) does not bind configuration.
- Treats Validate as an instance method on OptionsBuilder<T> and the other validation methods as extensions. The rule checks the containing type for both shapes. As a result, it must test the builder type and the extension list.

## ReadOnlyCollectionExposure
- Unwraps implicit conversions before it inspects the returned value. A List<T> returned as IReadOnlyList<T> is wrapped in an implicit conversion operation. As a result, the declared return type alone does not reveal the underlying collection.
- Only reports a returned field or property reference. A collection the method just constructed has no other reference. As a result, there is nothing for a caller to mutate.

## ReadonlyStructInitPropertyAnalyzer
- Adds init only to an auto-implemented getter. A computed getter reads other state. As a result, an added init would write a synthesized backing field that the getter ignores and the value would read back as default.

## SealedByDefaultAnalyzer
- Captures its candidate and relationship sets inside RegisterCompilationStartAction. Each compilation then gets fresh state, which is required for correct results in incremental and multi-compilation builds.
- Keeps a type unsealed when it appears as a generic constraint. A sealed class cannot be a generic constraint (CS0701). As a result, the rule must not suggest sealing a type that another member constrains.
- Excludes abstract, static, sealed, record, struct, and direct-Object subclasses from candidates. A COM class needs no explicit exclusion because CS0424 forbids a base class on a ComImport type.

## TimeSpanForDurationAnalyzer
- Returns early for the implementation part of a partial member. Roslyn raises the symbol action once per declaring part. As a result, the guard prevents a duplicate diagnostic.

## UnusedInterfaceParamAnalyzer
- Runs cheap gates before the interface-implementation walk. That walk resolves every interface member and is the expensive step. As a result, only methods that can report should reach it.
- Collects referenced names in one body walk. Each parameter then costs a hash lookup instead of a fresh subtree walk, which keeps the rule linear in body size.
- Exempts an underscore-prefixed parameter. The code fix would rename such a parameter to the discard convention anyway. As a result, reporting it would create a fix that changes nothing.

## DirectRecursionAnalyzer
- Does not count a call through another instance as direct recursion even when the receiver type matches. An implicit or explicit this receiver does count.
- Does not count a call inside a lambda as direct recursion: the lambda runs when it runs, not inside the declaring method frame. As a result, a deferred callback such as queue.Post(() => Start()) is excluded.

## TestMethodReflectionAnalyzer
- Resolves symbols only for member accesses that survive the syntax walk. Most accesses sit outside test methods and the walk allocates nothing. An aliased receiver such as BF.Public resolves without text matching.
- Matches a type derived from System.Type, such as TypeBuilder. This holds because it exposes the same reflection methods.
- Relies on the member-access action to report the receiver of an enum member. As a result, the identifier path must not report it again.

Related: [Decision Records](summary.md) explains how to use this page.
