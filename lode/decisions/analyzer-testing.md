# Analyzer Decisions: Testing
*Updated: 2026-11-20T17:16:51Z*

Rationale removed from source comments. Each section names the type that carried it.
Read this page before you change or delete the matching check.

## AssertionFact
- Materializes the assertion argument subtree once per assertion because six detectors walk it. Walking per detector made each assertion pay six traversals.

## CommittedRedStubAnalyzer
- Keeps every match from the type lookup because two references can declare the same type. The first match alone can miss the test attribute.

## ConstantOperandAssertionAnalyzer
- Treats the skip and Fail family as non-tautological: Skip reports state and Fail fails unconditionally. As a result, neither takes an operand the code under test influences.
- Reports only a literal, not a named constant. Comparing two distinct named constants is a deliberate value pin, while a literal-to-literal comparison can never surprise a reader.

## ForeignTestDisableAnalyzer
- Part B matches test-attribute names by spelling because disabled text holds no binding. No stronger check exists without a semantic model for the disabled region.
- Uses MetadataTypes.Find because it survives an ambiguous reference. As a result, the rule still runs when a type name is ambiguous.
- Stays silent on a non-constant Skip reason, matching how E128168 reads the same value. Two rules must agree on what counts as a reason.
- Leaves an #else or #elif branch alone because that branch is active or condition dependent. As a result, it does not prove a test is disabled.
- Treats only a literal false condition as disabling the branch in every build. A symbol condition evaluates false when undefined, which is undecidable.
- Wraps disabled class members in a type before parsing. This holds because a bare parse of member text yields no method declaration. Each reported span is mapped back past the wrapper prefix.

## LowValueTestAnalyzer.Signals
- Walks each production body once and collects every returned literal. This holds because the echo detector queries the set per assertion literal.
- Matches a literal to the constructor only when the test passed that literal in. A literal the constructor never received asserts something else.
- Counts only a literal in the assertion's own argument. A literal inside the call under test is an input, and a library accessor such as IDocument.QuerySelector is not the call under test.
- Treats an assertion argument that invokes production code as a computed value, not an echoed literal. This holds because the literal is only the shared input.
- Reports an echo only when the expected literal repeats an input. A literal that differs from every input states a result the code computed, which is a real assertion.
- Accepts an exact-equality pin of the whole message but not an Assert.Contains substring check. This holds because a documented hint is the contract.

## LowValueTestAnalyzer
- Disables both rules by default and documents an opt-in through dotnet_diagnostic severity. This holds because either condition can flag an ordinary unit test.
- Puts the condition names into the diagnostic message. A report that says only one low-value condition leaves the reader to guess which detector fired. As a result, the name is part of the contract.
- Collects every test method in a per-compilation closure because DuplicateCoverage needs the whole set before it can decide. The tier decision counts a duplicate beside the pure conditions.
- Builds a duplicate key from the literal-argument call and the body signature. This holds because a key from the call alone collides tests that differ in setup or in the method under test.
- Builds the body signature from tokens. As a result, trivia such as indentation or comments does not change it, and the method name and attributes stay outside the key.
- Does not count a standard-library call as the behavior under test. Two tests that seed the same range but exercise different production paths are not duplicates.
- Treats a call on an interface as a library or seam accessor, not as the concrete code under test. As a result, it neither seeds a duplicate key nor counts as the echoed call.
- Treats a short test that also reaches production as observing something real. Only a test that exercises nothing but its own literals lacks a boundary. A property read is not a call but still reaches production.
- Treats the test class as fixtures and helpers and every other type as code under test. A read of another type reaches a boundary even when it is a property read.
- Matches a test name to the call only when the name carries the method name and almost nothing else. A name that states a behavior and a condition describes the test instead.
- Accepts a call that throws as an assertion. This holds because a fluent rule library reports through the call itself. A helper declared in the test class asserts on the caller's behalf. As a result, its body is not read.
- Does not treat a void call with no assertion as a missing assertion. This holds because the throw is the only observable and a silent validator is the happy-path contract.
- Reports only when every examined assertion is low value. One real assertion gives the test its value, and an exception capture only sets up the assertion that follows it.
- Matches a whole name token, not a substring. This holds because a substring test accepts dialog for catalog.
- Requires the two compared operands to match expression for expression. This holds because a receiver-only comparison cannot tell ranks["A"] from ranks["B"] and reports a real invariant as circular.

## OutOfRepoResolverAssertionAnalyzer
- Treats any of the three xUnit v3 dynamic-skip entry points ahead of the resolver call as a guard.
- Parses the analyzer option once per tree because the option is constant for a tree.
- Relies on DescendantNodes being a pre-order walk. As a result, a node always precedes its descendants and no separate ordering pass is needed.
- Parses the analyzer option once per tree and reuses it for every test method. This holds because the option is constant for a tree.
- Keeps bare Type entries and composed Type.Member entries in one set. This holds because a bare entry never equals a composed string and one set cannot confuse them.
- Treats all four xUnit v3 settable skip properties as guards. This holds because every one makes the test skip conditionally.

## SwallowedAssertionAnalyzer
- Keeps every match from the type lookup because two references can declare the same type. The first match alone can miss the assertion type.

## TempDirCleanupAnalyzer
- Passes the member name in the diagnostic so the fix can name it in the generated cleanup call instead of guessing.
- Rejects a local variable or a parameter target. This holds because the generated Dispose call cannot reach it and the fix would not compile.
- Matches IAsyncLifetime by name only. As a result, a test project can supply its own stand-in interface.

## TestCodeCommentAnalyzer
- Skips the type's own leading trivia. As a result, a license header or a doc comment on the type does not report. DescendantTrivia includes that leading trivia.
- Builds the report location from FullSpan. This holds because a structured trivia's narrow Span excludes the /// marker. Trimming only the trailing newline keeps the visible comment text.

## TestFacts
- LowValueTestAnalyzer treats a method declared in the test class as a fixture or local helper. Its body seeds no duplicate key and echoes no production literal. A call to it still counts as an observation.

## UnconditionalTestDelayAnalyzer
- Checks the method shape before the symbol. This holds because an editor buffer can hold a partial method with no body.
- Binds the symbol after the name gate because the gate rejects only calls the symbol check also rejects. The diagnostic set does not change.
- Leaves a delay inside a loop to E128211, which owns the loop case.
- Requires the CancellationToken type before it can report the Task branch. This holds because a missing symbol cannot prove the token is absent.
- Reads only a literal integer or a TimeSpan factory built from one literal. This holds because a variable or computed duration can encode a real timeout the test chose.

## XunitTestHelper
- Reads the last segment of an attribute name. As a result, a plain, qualified, or alias-qualified spelling all match.

Related: [Decision Records](summary.md) explains how to use this page.
