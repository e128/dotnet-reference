diff --git a/AGENTS.md b/AGENTS.md
index dedcede..7f46934 100644
--- a/AGENTS.md
+++ b/AGENTS.md
@@ -34,6 +34,11 @@ of what is portable versus Claude-only.
 - **Build** — `scripts/check.sh` is the single shell lint, format, build, and
   test gate. It runs `scripts/lint-shell.sh` first. Never call raw
   `shellcheck` in a gate.
+- **Subagents** — fan out to five subagents by default when five genuinely
+  independent workstreams exist and parallel work is safe. Use fewer when work
+  overlaps, the task does not justify five, coordination costs outweigh the
+  gain, or a tool or permission blocks independent execution. Never create work
+  to hit the count. Avoid overlapping edits and synthesize the findings.
 - **Verify before you claim done.** Re-run the build and the affected tests
   after the last edit. Never assume a fix works. When a build or a run fails,
   report it. Never claim success unless the build and the tests pass.
diff --git a/lode/dotnet/analyzer-candidates.md b/lode/dotnet/analyzer-candidates.md
index 9cab1ce..f54bb76 100644
--- a/lode/dotnet/analyzer-candidates.md
+++ b/lode/dotnet/analyzer-candidates.md
@@ -1,5 +1,5 @@
 # Analyzer Candidates
-*Updated: 2026-11-01T20:56:22Z*
+*Updated: 2026-11-03T03:05:44Z*
 *Source: analyzer-review-miner 2026-04-15, evidence window 2026-04-12..2026-04-15*
 
 No open candidate remains from the 2026-04-15 mining pass. Each of the three
@@ -46,9 +46,19 @@ rule description and the fix shape live in `src/E128.Analyzers/README.md`.
 | E128139         | Unsafe code | `bool` read     | `Unsafe/`          |
 | E128140-E128141 | Performance | every rule      | `Performance/`     |
 
-The unused IDs between E128001 and E128141 are E128095 and E128130. The next
+The unused IDs between E128001 and E128151 are E128095 and E128130. The next
 new rule takes E128130, so the guidance range stays contiguous.
 
+## FIPS Cryptography Rules
+
+Rules E128071, E128075, and E128142 to E128151 implement the FIPS policy set.
+See `src/E128.Analyzers/README.md` for each rule description. Shared symbol and
+constant helpers live in `FipsSymbolHelpers.cs`.
+
+| ID range        | Domain   | Code fix | Category directory |
+| --------------- | -------- | -------- | ------------------ |
+| E128142-E128151 | Security | no rule  | `Security/`        |
+
 These rules have no code fix. The remediation depends on intent that the syntax
 tree does not carry.
 
diff --git a/lode/dotnet/analyzers.md b/lode/dotnet/analyzers.md
index 435d3f0..83b1cf8 100644
--- a/lode/dotnet/analyzers.md
+++ b/lode/dotnet/analyzers.md
@@ -1,5 +1,5 @@
 # .NET 10 Roslyn Analyzers
-*Updated: 2026-11-01T20:55:29Z*
+*Updated: 2026-11-03T03:07:49Z*
 
 ## Strategy: Deny by Default
 
@@ -100,7 +100,7 @@ The analyzer project targets `netstandard2.0` (required for Roslyn analyzers) wh
 
 `src/E128.Analyzers/` is a solution-local Roslyn analyzer project. It is wired via `Directory.Build.targets` as a `ProjectReference` with `OutputItemType="Analyzer"`. It applies to all projects except the analyzer itself (excluded via `IsRoslynComponent` condition). Severity is governed by `.globalconfig` (blanket error by default).
 
-Rules span categories: Design, Reliability, Performance, Unsafe, Maintainability, Security, Style, Testing, and FileSystem. Most rules ship with a code fix. Several rules carry a diagnostic only, for example `E128045` Direct Console usage, `E128046` Excessive inheritance, E128125 to E128129, and E128136 to E128138, because the remediation is context-specific. See `src/E128.Analyzers/README.md` for the complete rule table, code-fix status, and usage examples.
+Rules span categories: Design, Reliability, Performance, Unsafe, Maintainability, Security, Style, Testing, and FileSystem. Most rules ship with a code fix. Several rules carry a diagnostic only, because the remediation is context-specific. Examples are `E128045` Direct Console usage, `E128046` Excessive inheritance, E128125 to E128129, E128136 to E128138, and the FIPS cryptography rules E128142 to E128151. E128071 and E128075 ship a code fix. See `src/E128.Analyzers/README.md` for the complete rule table, code-fix status, and usage examples.
 
 Redundant E128 rules are disabled in `.globalconfig`, whether or not a code fix ships. See [redundant-rules.md](analyzers/redundant-rules.md).
 
@@ -111,11 +111,14 @@ Key rules by category (not exhaustive):
 | Design      | Sealed-by-default, async void, sync-over-async, ConfigureAwait, TimeProvider, DI, ImmutableArray   |
 | Reliability | GeneratedRegex safety, DateTime roundtrip, Task.WhenAll, JsonDocument lifetime, pool Rent() guard, stackalloc bounds, pool zeroing |
 | Performance | MinBy/MaxBy, HttpCompletionOption, FrozenSet, O(n²) loops, `SearchValues<char>` (E128102), UTF-8 parsing, alternate lookups (E128131 to E128135), span bulk methods (E128140), u8 literals (E128141) |
+| Security    | FIPS cryptography rules: unapproved hash names, `System.Random` in security code, managed and CNG type creation, RSA key size, cipher mode, PBKDF2 work factor, TLS versions, deprecated RNG, excluded cipher names, AES-GCM tag size, SHA-3 policy, HMAC key length (E128071, E128075, E128142 to E128151) |
 | Unsafe      | `Unsafe.As` to `Unsafe.BitCast`, unaligned reads, unaligned non-blittable reads (E128139), bounds bypass, bool and int reinterpret, non-blittable `MemoryMarshal`, fixed buffers, `SkipInit`, unpinned pointers, string writes, null byref, delegate lifetime, bulk copy, pointer and length pairs, nonpublic reflection, raw IL (E128112 to E128129, E128136 to E128139) |
 | Style       | string.Empty, Encoding.UTF8, XML doc comments, null-forgiving operator                             |
 | Maintainability | See the README rule table                                                                      |
 | Testing     | Temp directory cleanup, stale ReferenceAssemblies, low-value test conditions E128107/E128108       |
 
+The FIPS rule set reports an error and fails the build by default. It has its own page. See [fips-rules.md](analyzers/fips-rules.md) for the policy values and the shared helper.
+
 ### E128061: Static readonly array → immutable type
 
 Flags `private static readonly T[]` and `internal static readonly T[]` fields. Arrays are reference types. The `readonly` keyword prevents reassignment but callers can still mutate contents via the indexer. The code fix reads a constant `byte` sequence as a `ReadOnlySpan<byte>` property, which the compiler stores as read-only data. For every other element type it replaces `T[]` with `ImmutableArray<T>` and unwraps `new T[]` / `new[]` initializers to collection expressions.
diff --git a/lode/dotnet/analyzers/code-fix-output.md b/lode/dotnet/analyzers/code-fix-output.md
index 4ef55d1..48ae783 100644
--- a/lode/dotnet/analyzers/code-fix-output.md
+++ b/lode/dotnet/analyzers/code-fix-output.md
@@ -1,8 +1,10 @@
 # Code Fix Output
-*Updated: 2026-11-01T20:55:29Z*
+*Updated: 2026-11-03T03:14:50Z*
 
-Rules for the code a fix writes. A fix that emits a broken member costs more than
-a missing fix: the apply path writes it into the document.
+Rules for the text a fix writes and the text around it. A fix that emits a
+broken member costs more than a missing fix: the apply path writes it into the
+document. The rewrite shapes live in
+[code-fix-patterns.md](code-fix-patterns.md).
 
 ## Emit Valid Source Text
 
@@ -17,6 +19,10 @@ sb.Append('"').Append(literal).Append('"');
 sb.Append(SymbolDisplay.FormatLiteral(literal, quote: true));
 ```
 
+`SyntaxFactory.Literal(value)` takes token text, not a value. It emits an
+unquoted literal. Pass `SymbolDisplay.FormatLiteral(value, quote: true)` as the
+text, and the value as the second argument. E128013 follows this rule.
+
 `SyntaxFactory.ParseMemberDeclaration` returns null when the text is malformed.
 A fallback branch that emits a stub member hides the defect and writes broken
 code into the document. Use a formatter for each value and the parse holds.
@@ -28,6 +34,13 @@ parses the InlineArray struct before it registers and offers no fix on a null.
 leaves the document unchanged. It validates the whole member before it
 registers, so every offered fix changes code.
 
+## Text in an Interpolated String
+
+An `InterpolatedStringTextToken` carries two texts. `Text` is source text, so
+escape it. `ValueText` is the runtime value, so keep it raw. Set both to the
+escaped text and a tree comparison against parsed source fails. Set both raw
+and a quote or a line break breaks the emitted literal.
+
 ## Place a New Node with the Formatter
 
 Attach `Formatter.Annotation` to every node a fix inserts or reshapes. The
@@ -63,6 +76,60 @@ type reformats every member that the caller already wrote. E128054 inserts an
 Keep manual trivia when a token replaces an absent token, and when the test pins
 a blank line that the formatter would collapse.
 
+## Copy Only the Indentation
+
+A method leading trivia holds the doc comment as well as the indentation.
+Copying the whole list onto a new attribute list duplicates the comment.
+
+Copy the last whitespace trivia instead. E128073 follows this rule.
+
+```csharp
+var indentation = leadingTrivia.Last().IsKind(SyntaxKind.WhitespaceTrivia)
+    ? SyntaxFactory.TriviaList(leadingTrivia.Last())
+    : default;
+```
+
+## Trim Trailing Whitespace When Appending
+
+A fix that appends to a line can leave trailing whitespace behind. Reverse the
+trailing trivia, skip the whitespace, then reverse back:
+
+```csharp
+private static T WithoutTrailingSpaces<T>(T node)
+    where T : SyntaxNode
+{
+    var trailing = node.GetTrailingTrivia().Reverse()
+        .SkipWhile(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia))
+        .Reverse();
+
+    return node.WithTrailingTrivia(trailing);
+}
+```
+
+Reach this helper only for an appended line. A node the fix inserts takes the
+formatter rule instead. See § Place a New Node with the Formatter.
+
+## Trivia That Is Absent
+
+A leading-trivia list may hold no whitespace. `indentation.Last()` throws on an
+empty list. Branch on the count and emit the line break alone. E128051 follows
+this rule.
+
+## A Comment Inside a Directive
+
+A structured directive holds its own trailing comment. A split of one pragma
+into two drops that comment unless the last line keeps it.
+
+Copy the tail of the last ID token onto the last split directive. E128065
+does this. Leading trivia on the end-of-directive token does not render.
+
+## Insert Above the First Member
+
+A new field goes under the open brace. Give the field the indentation of the
+first member only. Prepend two line feeds to the existing leading trivia of
+that member. The doc comment then stays attached to the member it documents.
+E128102 and E128029 follow this rule.
+
 ## Insert a Member Only Once
 
 A fix that adds a member must look for a member with the same name and arity
@@ -76,6 +143,23 @@ only and skip a member that exists.
 The analyzer gate is not a substitute: it checks the interface list, not the
 member list.
 
+## Removing a Member Node
+
+`RemoveNode(node, SyntaxRemoveOptions.KeepNoTrivia)` drops the trivia of the
+removed node. The line break that ended the previous token stays. That result
+is correct for the common case.
+
+A blank line between the removed member and the closing brace survives the
+removal. Trim that line only when the leading trivia of the closing brace holds
+whitespace and line breaks alone. Keep the whitespace, and drop the line
+breaks. Never clear the whole trivia list.
+
+Clearing the whole list moves the closing brace to column 0 when the type is
+nested. It also joins a trailing comment and the brace onto one line, which
+comments the brace out.
+
+`LowValueTestCodeFixProvider` follows this rule.
+
 ## Guard the Rewrite Shape
 
 A fix must produce code that compiles. Test the shape that breaks it, not only
@@ -137,6 +221,5 @@ the common shape.
 
 - [Code Fix Patterns](code-fix-patterns.md): decision list, using directives,
   fix-all strategy, registration
-- [Code Fix Trivia](code-fix-trivia.md): absent trivia, literals, member removal
 - [Code Fix Techniques](code-fix-techniques.md): DocumentEditor, speculative
   bind, generator
diff --git a/lode/dotnet/analyzers/code-fix-patterns.md b/lode/dotnet/analyzers/code-fix-patterns.md
index 0429740..0a3162f 100644
--- a/lode/dotnet/analyzers/code-fix-patterns.md
+++ b/lode/dotnet/analyzers/code-fix-patterns.md
@@ -1,24 +1,50 @@
 # Code Fix Patterns
-*Updated: 2026-11-02T18:23:33Z*
+*Updated: 2026-11-03T03:14:50Z*
 
 Implementation patterns and gotchas for Roslyn code fix providers in E128.Analyzers.
+The emitted text rules live in [code-fix-output.md](code-fix-output.md). The
+advanced edit APIs live in [code-fix-techniques.md](code-fix-techniques.md).
 
 ## Decide Before You Write the Fix
 
-Answer these five questions before the first line of a fix provider. Each
+Answer these six questions before the first line of a fix provider. Each
 answer prevents a defect that the test suite will not catch on its own.
 
 1. Does the rewritten text parse? Emit a value through its formatter and a
-   type through `SymbolDisplayFormat`. See § Emit Valid Source Text.
+   type through `SymbolDisplayFormat`. See [code-fix-output.md](code-fix-output.md).
 2. Does the walk cross a lambda or a local function? Stop at the boundary.
    See § Stop at a Nested Function.
 3. Does the fix read a table the analyzer owns? Read the same gated table.
    See § One Table for Analyzer and Fix.
-4. Does the fix insert a member? Return null from `GetFixAllProvider()`. See
-   § FixAll That Inserts a Member. Then check that no member with that name and
-   arity exists. See § Insert a Member Only Once.
+4. Does the fix insert a member? Return null from `GetFixAllProvider()`, then
+   check that no member shares the name and arity. See
+   [code-fix-techniques.md](code-fix-techniques.md) § Fix-All Providers and
+   [code-fix-output.md](code-fix-output.md) § Insert a Member Only Once.
 5. Does the fix remove a member? Trim the trivia, never clear it. See
-   § Removing a Member Node.
+   [code-fix-output.md](code-fix-output.md) § Removing a Member Node.
+6. Does the remedy name an API that the caller can already reach? When the
+   only correct repair is a different call, the library must expose that call.
+   See § Code-Aware Library.
+
+## Code-Aware Library
+
+A library that ships an analyzer with it controls the remedy. The analyzer
+points at the canonical entry point, and the fix never invents a call.
+
+The `System.Collections.Immutable` guide shows the reason. `new ImmutableArray<int>()`
+produces a struct with a null backing array, so `.Length` throws at run time.
+The fix replaces the construction expression with a name the library owns:
+
+```csharp
+var generator = SyntaxGenerator.GetGenerator(document);
+var memberAccess = generator.MemberAccessExpression(objectCreation.Type, "Empty");
+var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
+return document.WithSyntaxRoot(root.ReplaceNode(objectCreation, memberAccess));
+```
+
+Apply this rule this way. Gate the analyzer on the API, as
+[new-analyzer-checklist.md](new-analyzer-checklist.md) § Gate on API
+Availability requires. When no such API exists, report without a fix.
 
 ## Add a Using Directive
 
@@ -43,16 +69,21 @@ the real output. Run the code fix test before you finalize the expected text.
 The helper handles a top-level using on the compilation unit only. A file with
 a namespace-scoped using list needs a different path.
 
-## BatchFixer vs SequentialRenameFixAllProvider
+## Span Widening
 
-- **`WellKnownFixAllProviders.BatchFixer`**: standard choice for most code fixes. Computes all fixes from the original snapshot and merges. Works well when fixes are independent local edits (expression replacements, type swaps).
-- **`SequentialRenameFixAllProvider`**: required when the fix uses `Renamer.RenameSymbolAsync`. BatchFixer fails when multiple renames touch the same document because the rename API modifies the solution globally. See [Code Fix Techniques](code-fix-techniques.md) § Sequential Rename Needs Its Own Fix-All. E128059 and E128063 use it.
-- **`DocumentBasedFixAllProvider`**: required when a later fix must see an earlier edit in the same document. Apply each diagnostic to the current document in descending span order. Use it for a fix that re-binds a document, and for a fix that removes or re-nests a node that a second diagnostic sits inside.
+A diagnostic often lands on a narrower node than the edit replaces. Report on
+the token that names the defect, then widen the edit target before you replace.
 
-## Emit Valid Source Text
+```csharp
+if (root.FindNode(context.Span, getInnermostNodeForTie: true)
+        .FirstAncestorOrSelf<ObjectCreationExpressionSyntax>() is not { } creation)
+{
+    return;
+}
+```
 
-A token carries the unescaped value, so a hand-written quote breaks the parse.
-See [Code Fix Output](code-fix-output.md) § Emit Valid Source Text.
+`root.FindNode(span, getInnermostNodeForTie: true)` is the repo idiom. A token
+walk with `FindToken(span.Start)` reaches the same node and costs more.
 
 ## A Fix Without a Rewritable Receiver
 
@@ -95,25 +126,6 @@ Apply the boundary to the node that is reported, not only to the scans. A rule
 that scans the method body but reports every candidate node still names a node
 that the fix refuses to touch.
 
-## FixAll That Inserts a Member
-
-`BatchFixer` computes every fix from the same snapshot and merges the results.
-Two fixes in one type that insert the same member name each pass their own
-conflict check on the original tree. The merge then emits the member twice.
-
-Return `null` from `GetFixAllProvider()` for a fix that inserts a field or a
-property. Keep that fix single-diagnostic. E128029 follows this rule.
-
-## Insert a Member Only Once
-
-A second member with the same name and arity is CS0111. See
-[Code Fix Output](code-fix-output.md) § Insert a Member Only Once.
-
-## Place a New Node with the Formatter
-
-Attach `Formatter.Annotation` to a node the fix inserts or reshapes. See
-[Code Fix Output](code-fix-output.md) § Place a New Node with the Formatter.
-
 ## One Table for Analyzer and Fix
 
 A lookup table that the analyzer reports from must be the same table the fix
@@ -125,7 +137,7 @@ E128028 held the sync I/O names twice. The analyzer table listed `File.Copy`,
 `await` and left the blocking call in place. The compiler then reported CS1998.
 
 Declare the table `internal static readonly` on the analyzer and let the fix
-provider read it. Report only what the fix can repair. E128028 now omits a
+provider read it. Report only what the fix can repair. E128028 omits a
 method unless an awaitable overload exists.
 
 Gate the table once. E128028 builds the pair list at compilation start and the
@@ -138,16 +150,10 @@ Pass a computed value through `diagnostic.Properties` instead of copying the
 table. E128023 puts the detected path literal in the analyzer and hands the
 relative suffix to the fix. The path prefixes live in one file.
 
-## Guard the Rewrite Shape
-
-A fix must produce code that compiles. See
-[Code Fix Output](code-fix-output.md) § Guard the Rewrite Shape for the full
-list of shapes that break a rewrite.
-
 ## Decline at Registration
 
-A fix that returns the document unchanged offers the user a dead action. The
-harness fails that case with "a fix was offered that made no changes".
+A fix that returns the document unchanged offers a dead action. The harness
+fails that case with "a fix was offered that made no changes".
 
 Do the whole shape check at registration, in the override the provider
 defines (`RegisterCodeFixesAsync`, or `RegisterFixesAsync(context, root)`
@@ -186,7 +192,7 @@ carry a qualified receiver such as `System.Text.Encoding`. E128006 swaps
 
 ## Related
 
-- [Code Fix Trivia](code-fix-trivia.md): absent trivia, literals, member removal
+- [Code Fix Output](code-fix-output.md): emitted text, trivia, rewrite shapes
 - [Code Fix Techniques](code-fix-techniques.md): DocumentEditor, speculative bind, generator
 - [New Analyzer Checklist](new-analyzer-checklist.md)
 - [Analyzers](../analyzers.md): main analyzer documentation
diff --git a/lode/dotnet/analyzers/code-fix-techniques.md b/lode/dotnet/analyzers/code-fix-techniques.md
index b8a3b3e..f75aa3f 100644
--- a/lode/dotnet/analyzers/code-fix-techniques.md
+++ b/lode/dotnet/analyzers/code-fix-techniques.md
@@ -1,5 +1,5 @@
 # Code Fix Techniques
-*Updated: 2026-11-01T18:21:44Z*
+*Updated: 2026-11-03T03:14:50Z*
 
 Techniques that prove a fix writes correct code. The shape rules live in
 [Code Fix Output](code-fix-output.md). The decision list lives in
@@ -19,8 +19,7 @@ Override `RegisterCodeFixesAsync` directly when the fix reads the diagnostic
 alone and never touches the root, or when the provider needs a semantic model
 read before the root fetch order matters. Silence a fix-all with
 `GetFixAllProvider()` returning `null` only when a merge would corrupt the
-output. See [Code Fix Patterns](code-fix-patterns.md) § FixAll That Inserts a
-Member.
+output. See § Fix-All Providers.
 
 ## Use DocumentEditor for a Multi-Node Edit
 
@@ -126,6 +125,43 @@ and the flow of a value between statements.
 Keep the syntax gate in front of the operation when the rule fires rarely. See
 [Analyzer Performance](performance.md) § Bind Late.
 
+## Fix-All Providers
+
+`CodeFixProvider.GetFixAllProvider` is virtual and returns null. A provider
+that does not override it offers no fix-all action. `E128CodeFixProviderBase`
+owns the override, so a derived provider inherits the batch provider.
+
+- **`WellKnownFixAllProviders.BatchFixer`**: the standard choice. It computes
+  every fix from the original snapshot and merges the results in parallel. Use
+  it when the fixes are independent local edits, such as an expression
+  replacement or a type swap. It handles the Document, Project, Solution,
+  ContainingMember and ContainingType scopes.
+- **`DocumentBasedFixAllProvider`**: required when a later fix must see an
+  earlier edit in the same document. Apply each diagnostic to the current
+  document in descending span order. Use it for a fix that re-binds a document,
+  and for a fix that removes or re-nests a node that a second diagnostic sits
+  inside.
+- **Null**: required for a fix that inserts a member. BatchFixer computes every
+  fix from one snapshot, so two inserts in one type each pass their own conflict
+  check and the merge emits the member twice. E128029 follows this rule.
+
+BatchFixer merges only `ApplyChangesOperation` instances. Any other operation
+that a provider emits is dropped without a warning. Keep a custom operation out
+of a fix path that fix-all must cover.
+
+`CodeAction.Create` groups fixes for fix-all by the equivalence key. Pass the
+three-argument overload. A two-argument call leaves the key null, and fix-all
+cannot separate the action from a sibling action on the same diagnostic.
+
+```csharp
+context.RegisterCodeFix(
+    CodeAction.Create(
+        title: "Use ImmutableArray<T>.Empty",
+        createChangedDocument: c => ChangeAsync(document, creation, c),
+        equivalenceKey: "UseImmutableArrayEmpty"),
+    diagnostic);
+```
+
 ## Sequential Rename Needs Its Own Fix-All
 
 `SequentialRenameFixAllProvider` in `E128.Analyzers.Style` serves every fix that
diff --git a/lode/dotnet/analyzers/code-fix-trivia.md b/lode/dotnet/analyzers/code-fix-trivia.md
deleted file mode 100644
index 032c9ed..0000000
--- a/lode/dotnet/analyzers/code-fix-trivia.md
+++ /dev/null
@@ -1,74 +0,0 @@
-# Code Fix Trivia
-*Updated: 2026-11-01T20:55:29Z*
-
-How a code fix keeps the text around the node it rewrites. See
-[Code Fix Patterns](code-fix-patterns.md) for the rewrite shapes.
-
-## Copy Only the Indentation
-
-A method leading trivia holds the doc comment as well as the indentation.
-Copying the whole list onto a new attribute list duplicates the comment.
-
-Copy the last whitespace trivia instead. E128073 follows this rule.
-
-```csharp
-var indentation = leadingTrivia.Last().IsKind(SyntaxKind.WhitespaceTrivia)
-    ? SyntaxFactory.TriviaList(leadingTrivia.Last())
-    : default;
-```
-
-## A Comment Inside a Directive
-
-A structured directive holds its own trailing comment. A split of one pragma
-into two drops that comment unless the last line keeps it.
-
-Copy the tail of the last ID token onto the last split directive. E128065
-does this. Leading trivia on the end-of-directive token does not render.
-
-## Trivia That Is Absent
-
-A leading-trivia list may hold no whitespace. `indentation.Last()` throws on an
-empty list. Branch on the count and emit the line break alone. E128051 follows
-this rule.
-
-## An Unquoted Replacement Literal
-
-`SyntaxFactory.Literal(value)` takes token text, not a value. It emits an
-unquoted literal. Pass `SymbolDisplay.FormatLiteral(value, quote: true)` as the
-text, and the value as the second argument. E128013 follows this rule.
-
-## Text in an Interpolated String
-
-An `InterpolatedStringTextToken` carries two texts. `Text` is source text, so
-escape it. `ValueText` is the runtime value, so keep it raw. Set both to the
-escaped text and a tree comparison against parsed source fails. Set both raw
-and a quote or a line break breaks the emitted literal.
-
-## Insert Above the First Member
-
-A new field goes under the open brace. Give the field the indentation of the
-first member only. Prepend two line feeds to the existing leading trivia of
-that member. The doc comment then stays attached to the member it documents.
-E128102 and E128029 follow this rule.
-
-## Removing a Member Node
-
-`RemoveNode(node, SyntaxRemoveOptions.KeepNoTrivia)` drops the trivia of the
-removed node. The line break that ended the previous token stays. That result
-is correct for the common case.
-
-A blank line between the removed member and the closing brace survives the
-removal. Trim that line only when the leading trivia of the closing brace holds
-whitespace and line breaks alone. Keep the whitespace, and drop the line
-breaks. Never clear the whole trivia list.
-
-Clearing the whole list moves the closing brace to column 0 when the type is
-nested. It also joins a trailing comment and the brace onto one line, which
-comments the brace out.
-
-`LowValueTestCodeFixProvider` follows this rule.
-
-## Related
-
-- [Code Fix Patterns](code-fix-patterns.md)
-- [New Analyzer Checklist](new-analyzer-checklist.md)
diff --git a/lode/dotnet/analyzers/configuration.md b/lode/dotnet/analyzers/configuration.md
new file mode 100644
index 0000000..8323c9e
--- /dev/null
+++ b/lode/dotnet/analyzers/configuration.md
@@ -0,0 +1,162 @@
+# Analyzer Configuration
+
+*Updated: 2026-11-03T03:14:50Z*
+
+How a rule reads a configuration value. The project-level file split lives in
+[Analyzers](../analyzers.md) § Configuration Split. This file covers the
+analyzer side: which API reads the value, and which key name belongs in the
+config file.
+
+## Two Option Surfaces
+
+`AnalyzerConfigOptionsProvider` exposes three members. Choose by scope, not by
+convenience.
+
+| Member                    | Scope                            | Source                                     |
+| ------------------------- | -------------------------------- | ------------------------------------------ |
+| `GlobalOptions`           | One per compilation              | `.globalconfig`, `build_property.*`        |
+| `GetOptions(SyntaxTree)`  | One per source file              | `.editorconfig` section for that path      |
+| `GetOptions(AdditionalText)` | One per additional file       | `.editorconfig` section for that path      |
+
+A project-wide switch belongs on `GlobalOptions`. A per-directory or
+per-file-name switch belongs on `GetOptions(tree)`.
+
+`UnregisteredServiceResolveAnalyzer` reads `e128_registered_services` from
+`GlobalOptions`, because a service list applies to the whole project.
+`StaleReferenceAssembliesAnalyzer` reads `e128_minimum_framework_version` from
+`GetOptions(tree)`, so a path-scoped `.editorconfig` can override the project
+value. The repo sets the project value in `tests/.globalconfig`.
+
+The code fix reaches the same surface through
+`document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider`. Read the value
+there, never from a second copy of the configuration.
+
+## Key Naming
+
+The analyzer owns the key text, and the compiler enforces no namespace for it.
+Use a lowercase `snake_case` name with the `e128_` prefix, so a key in a shared
+`.globalconfig` cannot collide with a third-party analyzer.
+
+The compiler driver consumes only a `.severity` key. Every other key reaches
+the analyzer as an opaque string, and the analyzer parses it.
+
+## Severity Keys
+
+Three keys exist, from narrow to broad:
+
+```ini
+dotnet_diagnostic.E128028.severity = error
+dotnet_analyzer_diagnostic.category-Reliability.severity = warning
+dotnet_analyzer_diagnostic.severity = error
+```
+
+The prefix differs on purpose. One rule uses `dotnet_diagnostic`. A bulk key
+uses `dotnet_analyzer_diagnostic`.
+
+Precedence runs rule ID, then category, then blanket. A rule-specific key wins
+over both bulk forms.
+
+A bulk key reaches only a rule whose default is enabled. A rule that declares
+`isEnabledByDefault: false` stays silent until a rule-specific key turns it on.
+
+## MSBuild Properties
+
+An MSBuild property reaches an analyzer only after the build opts it in.
+
+```xml
+<ItemGroup>
+  <CompilerVisibleProperty Include="RootNamespace" />
+</ItemGroup>
+```
+
+The key then carries a `build_property.` prefix:
+
+```csharp
+context.Options.AnalyzerConfigOptionsProvider.GlobalOptions
+    .TryGetValue("build_property.RootNamespace", out var rootNamespace);
+```
+
+The bare property name returns nothing. The prefix is the load-bearing part.
+
+Ship the `CompilerVisibleProperty` item in a `.props` file under `build/` in the
+package, so a consumer inherits it without an edit. See
+[package-layout.md](package-layout.md) § NuGet Layout Rule.
+
+A missing key and a wrong value both read as absent. Test the value with
+`string.Equals(value, "true", StringComparison.Ordinal)`. Never compare a
+string with `==` in analyzer code. See
+[new-analyzer-checklist.md](new-analyzer-checklist.md) § Netstandard2.0
+Constraints.
+
+## Additional Files
+
+A `.editorconfig` holds key and value pairs. A file with structure of its own
+belongs in `AdditionalFiles`, and the analyzer parses it.
+
+```xml
+<ItemGroup>
+  <AdditionalFiles Include="e128-services.txt" />
+</ItemGroup>
+```
+
+Read the file once in `RegisterCompilationStartAction`, so the cost lands once
+per compilation.
+
+```csharp
+foreach (var file in start.Options.AdditionalFiles)
+{
+    var text = file.GetText(start.CancellationToken);
+    if (text is null)
+    {
+        continue;
+    }
+
+    foreach (var line in text.Lines)
+    {
+        if (line.Span.IsEmpty)
+        {
+            continue;
+        }
+        // Parse the line.
+    }
+}
+```
+
+The analyzer owns the file format. Text and JSON both work. `file.Path` names
+the file when several entries exist.
+
+The repo already uses the item for `AnalyzerReleases.Shipped.md`,
+`AnalyzerReleases.Unshipped.md`, `PublicAPI.Shipped.txt`, and
+`PublicAPI.Unshipped.txt`. The Roslyn release-tracking analyzer reads them. See
+[release-tracking.md](../release-tracking.md).
+
+## Generated Code Key
+
+`generated_code = true` under a path section marks files as generated for the
+code-analysis driver:
+
+```ini
+[*.MyGenerated.cs]
+generated_code = true
+```
+
+The key changes the driver only. The rule still chooses its own flags. See
+[new-analyzer-checklist.md](new-analyzer-checklist.md) § Generated Code.
+
+## Suppressors Are Not a Configuration Surface
+
+A `DiagnosticSuppressor` derives from `DiagnosticAnalyzer`, but the compiler
+seals its `Initialize`, so it registers no analysis action. It also cannot
+report a new diagnostic, and it cannot suppress a diagnostic whose default
+severity is `Error`.
+
+This repo ships every rule at the severity the policy demands, so a suppressor
+would never apply. A consumer that needs an exemption edits `.globalconfig`.
+Do not add a suppressor to answer a false positive. Fix the rule instead.
+
+## Related
+
+- [Analyzers](../analyzers.md): severity strategy and the file split
+- [New Analyzer Checklist](new-analyzer-checklist.md): registration and gates
+- [Package Layout](package-layout.md): shipping a props file
+- [Analyzer Testing](testing.md): supplying an option in a test
diff --git a/lode/dotnet/analyzers/fips-rules.md b/lode/dotnet/analyzers/fips-rules.md
new file mode 100644
index 0000000..92b306b
--- /dev/null
+++ b/lode/dotnet/analyzers/fips-rules.md
@@ -0,0 +1,117 @@
+# FIPS Cryptography Rules
+*Updated: 2026-11-03T03:14:50Z*
+
+The Security analyzer set enforces organization cryptography policy. All rules
+report an error, so a policy break fails the build by default. E128071 and
+E128075 are the only rules with a code fix. The rule text lives
+in `src/E128.Analyzers/README.md`. This file records the policy values and the
+invariants that the rule set shares.
+
+## Rule Map
+
+| Rule    | Policy subject                                  |
+| ------- | ----------------------------------------------- |
+| E128071 | Blocked hash, cipher and padding algorithms     |
+| E128075 | `System.Random` in a security context           |
+| E128142 | Managed and CNG concrete type creation          |
+| E128143 | RSA key size and named curve                    |
+| E128144 | Cipher mode                                     |
+| E128145 | PBKDF2 work factor and PRF                      |
+| E128146 | TLS protocol version                            |
+| E128147 | Obsolete `RNGCryptoServiceProvider`             |
+| E128148 | Ciphers excluded by name                        |
+| E128149 | AES-GCM tag size                                |
+| E128150 | SHA-3, SHAKE and KMAC policy block              |
+| E128151 | HMAC key length                                 |
+
+## Policy Values
+
+- Every rule reports an error. A policy break fails the build by default, so a
+  consumer must fix the code or lower the severity in its own configuration.
+- RSA keys use at least 3072 bits. Approved curves are P-384 and P-521.
+- PBKDF2 uses at least 600,000 iterations.
+- AES-GCM uses the full 16-byte tag.
+- An HMAC key is at least as long as the hash output.
+- TLS is 1.2 or later.
+- Organization policy blocks SHA-3, SHAKE and KMAC. NIST does not block them.
+  The rule reports policy, not a NIST prohibition.
+- DSA signature generation is blocked. DSA verification of an existing
+  signature stays allowed.
+- The E128071 fix maps each blocked hash type to its SHA-2 replacement, so the
+  digest of existing data changes. The E128075 fix maps `Random.Shared.Next` to a
+  single replacement. The other rules leave the replacement choice to the caller.
+- Every rule analyzes generated code with
+  `GeneratedCodeAnalysisFlags.Analyze | ReportDiagnostics`. Generated crypto
+  code runs in production, so it meets the same policy.
+
+## One Diagnostic per Type
+
+E128071 reports a blocked type. E128148 reports a type whose name matches an
+excluded cipher. `FipsTypeClassifier` gives each type one verdict, so the two
+rules never report the same type. E128148 skips a type that E128071 blocks,
+because the type or a base type is in the E128071 set. E128071 stops its
+factory-name analysis when E128148 owns the type, so a call site also produces
+one diagnostic.
+
+E128148 matches a role word only when it ends the type name. `IdeaEngine`
+reports. `IdeaRankingAlgorithm` and `IdeaManagedService` do not.
+
+`FipsTypeClassifier` caches the verdict per compilation, so each rule reads the
+shared verdict and holds no private copy.
+
+Both rules inspect the static members `Create`, `HashData`, `TryHashData` and
+`HashDataAsync`, which produce a value without `new`.
+
+## Shared Helper
+
+`FipsSymbolHelpers` in `src/E128.Analyzers/Security/` holds symbol matching,
+constant resolution, hash-name reporting and buffer-length reading. Rules call it
+instead of matching source text. `FipsTypeClassifier` holds the type verdict.
+
+Invariants:
+
+- Type matching uses symbols. A `using` alias or `using static` cannot hide a
+  blocked type.
+- E128071 reads the assignment target member name before it walks the
+  assignment. A deconstruction assignment always enters the walk, because its
+  targets appear only after the split.
+- A hash name resolves through a static property, a constructor, a
+  `FromOid` call, a conditional, a `??` or a switch expression. It also resolves
+  through an unreassigned local or a `readonly` field that no constructor and no
+  `init` setter writes.
+- A name that appears more than once at one call site or one store reports once.
+- A collection probe such as `Equals` or `Contains` on a framework type is
+  skipped. A method that application code declares is not skipped.
+- A buffer length resolves from an array creation, a collection expression, a
+  `stackalloc`, `Encoding.GetBytes` and `Convert.FromBase64String` or
+  `Convert.FromHexString` with a constant argument.
+- A buffer length also resolves through a slice or a range: `AsSpan`, `AsMemory`,
+  `Slice`, `RuntimeHelpers.GetSubArray`, an index expression and an array range.
+  A non-constant bound makes the length unknown.
+- A compile-time unknown value is not reported. The rule does not guess.
+- No rule reads memory or a runtime value.
+
+## Hash Name Stores
+
+A `HashAlgorithmName` that a wrapper receives as a variable is not resolved. The
+store check reports a mutable field initializer, a property initializer and an
+assignment to a `HashAlgorithmName` target. A `readonly` field resolves at each
+use, so its initializer is not reported twice.
+
+```csharp
+private HashAlgorithmName _name = HashAlgorithmName.SHA1;          // reported
+private static readonly HashAlgorithmName Ok = HashAlgorithmName.SHA256; // not reported
+```
+
+## Adding a Rule
+
+Follow [new-analyzer-checklist.md](new-analyzer-checklist.md). Assign the next
+free id from [analyzer-candidates.md](../analyzer-candidates.md). Add a row to
+`AnalyzerReleases.Unshipped.md` and the README table in the same change. Run
+`scripts/readme-table-diff.sh --analyzer`.
+
+## Related
+
+- [analyzers.md](../analyzers.md): analyzer strategy and the category map
+- [analyzer-candidates.md](../analyzer-candidates.md): the ID map and free IDs
+- [../../terminology.md](../../terminology.md): the FIPS term
diff --git a/lode/dotnet/analyzers/new-analyzer-checklist.md b/lode/dotnet/analyzers/new-analyzer-checklist.md
index 8541b5e..df69ba8 100644
--- a/lode/dotnet/analyzers/new-analyzer-checklist.md
+++ b/lode/dotnet/analyzers/new-analyzer-checklist.md
@@ -1,5 +1,5 @@
 # New Analyzer Checklist
-*Updated: 2026-11-02T18:23:33Z*
+*Updated: 2026-11-03T03:14:55Z*
 
 Pre-flight checks and common pitfalls when writing new E128.Analyzers rules.
 
@@ -81,24 +81,10 @@ A rule can also need one member, not a whole type. E128072 resolves
 `System.Security.Cryptography.SHA256` and then looks for a `HashData` member.
 .NET 5 adds that member, so the rule stays silent on an older target.
 
-## Register Both Object Creation Forms
-
-`new Foo { ... }` reports as `ObjectCreationExpression`. The target-typed form
-`Foo x = new() { ... }` reports as `ImplicitObjectCreationExpression`. Register
-both, and take `BaseObjectCreationExpressionSyntax` in the handler.
-
-```csharp
-context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
-context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ImplicitObjectCreationExpression);
-```
-
-E128071 and E128091 follow this shape. A rule that registers one form misses
-half of the object initializers in modern code.
-
-Gate the remedy, not the API the rule names. E128028 maps each sync I/O call to
-its async replacement. `File.ReadAllTextAsync` arrived after `File.ReadAllText`,
-so a netstandard2.0 target has no remedy for the pair. The rule keeps a pair
-only when the async member exists in the compilation.
+Gate the remedy, not the API that the rule names. E128028 maps each sync I/O
+call to its async replacement. `File.ReadAllTextAsync` arrived after
+`File.ReadAllText`, so a netstandard2.0 target has no remedy for the pair. The
+rule keeps a pair only when the async member exists in the compilation.
 
 ```csharp
 var owner = compilation.GetTypeByMetadataName(entry.Value);
@@ -116,6 +102,20 @@ action, and Roslyn reports that as AD0001 for every consumer.
 Test the gate. Set `ReferenceAssemblies` to
 `ReferenceAssemblies.NetStandard.NetStandard20` and expect no diagnostic.
 
+## Register Both Object Creation Forms
+
+`new Foo { ... }` reports as `ObjectCreationExpression`. The target-typed form
+`Foo x = new() { ... }` reports as `ImplicitObjectCreationExpression`. Register
+both, and take `BaseObjectCreationExpressionSyntax` in the handler.
+
+```csharp
+context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
+context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ImplicitObjectCreationExpression);
+```
+
+E128071 and E128091 follow this shape. A rule that registers one form misses
+half of the object initializers in modern code.
+
 ## Match Symbols, Not Names
 
 Report a construct only after the semantic model confirms the symbol. A check
@@ -195,24 +195,49 @@ comparison loses that order.
   same type. `MetadataTypes` wraps the `GetTypesByMetadataName` lookup for that
   case.
 
+## Expect Incomplete Code
+
+An analyzer sees an editor buffer that can fail to compile. Guard every
+semantic read.
+
+- `GetSymbolInfo` returns null while a `using` directive is missing. Cast with
+  `as` and test for null.
+- A conversion test rejects a user-defined conversion and a conversion that does
+  not exist. Check `ClassifyConversion` before the fix rewrites a value.
+- A reference type takes only `null` as a constant. `string` also takes a
+  literal.
+
 ## Generated Code
 
 - Pass `GeneratedCodeAnalysisFlags.None` to `ConfigureGeneratedCodeAnalysis`
   for a rule that must ignore generated code. That is the default shape.
 - Opt in only when the generated file is the subject of the rule. Add a comment
   that states why.
+- The FIPS cryptography rules opt in with `Analyze | ReportDiagnostics`. Code
+  that a source generator emits still runs in production, so generated crypto
+  code must meet the same policy. See [fips-rules.md](fips-rules.md).
 
 ## Keep the Package README Current
 
-`src/E128.Analyzers/README.md` is the NuGet package page (`PackageReadmeFile`). It is the primary documentation surface for the package and is copied to nuget.org. After any analyzer change (new rule, renamed rule, changed category/severity, added/removed code fix), update the README's rule tables, "What each rule catches" sections, and code examples to match. A stale rule table makes the package look unmaintained.
+`src/E128.Analyzers/README.md` ships as the NuGet package page
+(`PackageReadmeFile`) and appears on nuget.org. Update its rule tables, its
+"What each rule catches" sections, and its code examples in the same change as
+any rule edit. Cover a new rule, a rename, a category change, a severity change,
+and a new or removed code fix. A stale rule table makes the package look
+unmaintained.
+
+## Test Infrastructure
+
+The harness types, markup forms, and option injection live in
+[testing.md](testing.md). Read that file before you write the test file.
 
-## Test Infrastructure Notes
+Two rules belong here, because they change the source layout:
 
-- `CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>` and `CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>` from `Microsoft.CodeAnalysis.CSharp.Testing` are the standard test harness. Most analyzer test files build a `test.RunAsync()` call directly, not the static `CSharpAnalyzerVerifier` helper.
-- Code fix tests route through the shared runner in `tests/E128.Analyzers.Tests/CodeFixTestRunner.cs`. Call `CodeFixTestRunner.RunAsync` for a fix and `NoFixAsync` for a decline. The shared stub types live in `tests/E128.Analyzers.Tests/TestStubs.cs`.
-- New code fix providers inherit `E128CodeFixProviderBase` in `src/E128.Analyzers.CodeFixers`. The base owns the batch fix-all provider and the syntax root fetch. Override the protected `RegisterFixesAsync(context, root)` hook. Override `RegisterCodeFixesAsync` only when the fix never reads the root.
-- Test `ReferenceAssemblies` default to the SDK version configured in `tests/.globalconfig` via `e128_minimum_framework_version`
-- xUnit v3 MTP runner requires `scripts/test.sh`. Raw `dotnet test --filter` does not work
+- A new code fix provider inherits `E128CodeFixProviderBase` in
+  `src/E128.Analyzers.CodeFixers`. Override the protected
+  `RegisterFixesAsync(context, root)` hook.
+- Code fix tests route through `CodeFixTestRunner` in
+  `tests/E128.Analyzers.Tests/CodeFixTestRunner.cs`.
 
 ## Related
 
diff --git a/lode/dotnet/analyzers/package-layout.md b/lode/dotnet/analyzers/package-layout.md
index 0cc8a89..c8d23c0 100644
--- a/lode/dotnet/analyzers/package-layout.md
+++ b/lode/dotnet/analyzers/package-layout.md
@@ -1,5 +1,5 @@
 # Analyzer Package Layout
-*Updated: 2026-11-01T20:55:29Z*
+*Updated: 2026-11-03T03:13:32Z*
 
 How the analyzer assembly is built, packed, and loaded. See
 [Analyzers](../analyzers.md) for the rule strategy.
@@ -29,6 +29,21 @@ the project again and imports the NuGet props that the restore wrote. Without
 that property, MSBuild reuses the evaluation from the restore call and the pack
 fails with CS0518 on a clean checkout.
 
+## NuGet Layout Rule
+
+NuGet defines the analyzer path. `dotnet` is the only allowed framework name,
+because Roslyn is the only host that runs an analyzer.
+
+```text
+analyzers/{framework_name}{version}/{supported_architecture}/{language}/{name}.dll
+```
+
+- Omit the language folder to apply the analyzer to every language.
+- Omit the framework folder to apply it to every target.
+- A supporting non-DLL file ships through a `.props` or `.targets` file. Put the
+  `.props` file in `build/`.
+- The pack target names both DLLs with `PackagePath` set to `analyzers/dotnet/cs`.
+
 ## Per-Roslyn-Version Assemblies
 
 A package can also ship one assembly per Roslyn version, at
@@ -52,6 +67,35 @@ version costs three files and no edits.
 - **Test parallelism**: every test compiles a project. `xunit.runner.json` caps
   the analyzer test project at four threads to hold peak memory down.
 
+## Package Metadata
+
+`src/E128.Analyzers/E128.Analyzers.csproj` sets the packaging flags. Each flag
+has one job.
+
+| Property                          | Value  | Effect                                              |
+| --------------------------------- | ------ | --------------------------------------------------- |
+| `IncludeBuildOutput`              | false  | The DLLs pack through `PackagePath`, not through `lib` |
+| `DevelopmentDependency`           | true   | The consumer does not get a transitive compile reference |
+| `SuppressDependenciesWhenPacking` | true   | The nuspec lists no dependency, so Roslyn does not flow to the consumer |
+| `PackageLicenseExpression`        | MIT    | Replaces the deprecated `PackageLicenseUrl`          |
+| `PackageReadmeFile`               | README  | Publishes the rule tables to nuget.org              |
+
+`SuppressDependenciesWhenPacking` is the mechanism that keeps
+`Microsoft.CodeAnalysis.CSharp` out of the consumer graph. Do not rely on a
+`PrivateAssets` entry on that reference instead.
+
+A consumer inside this repository references the analyzer project directly:
+
+```xml
+<ProjectReference Include="src/E128.Analyzers/E128.Analyzers.csproj"
+                  OutputItemType="Analyzer"
+                  ReferenceOutputAssembly="false" />
+```
+
+`Directory.Build.targets` adds that item for every project except the analyzer
+itself. A consumer outside the repository takes a plain `PackageReference`,
+because the DLL sits under `analyzers/dotnet/cs`.
+
 ## Related
 
 - [Analyzers](../analyzers.md): rule strategy and configuration
diff --git a/lode/dotnet/analyzers/performance.md b/lode/dotnet/analyzers/performance.md
index 881ab3c..9f1ba18 100644
--- a/lode/dotnet/analyzers/performance.md
+++ b/lode/dotnet/analyzers/performance.md
@@ -1,5 +1,5 @@
 # Analyzer Performance
-*Updated: 2026-11-01T20:55:18Z*
+*Updated: 2026-11-03T03:13:32Z*
 
 Every analyzer in `src/E128.Analyzers` runs on each compilation and each edit. A
 slow analyzer multiplies its cost by the number of nodes it registers for.
@@ -108,6 +108,33 @@ argument subtrees and the returned literal set on the fact object, then let each
 detector read the stored value. A lazy property keeps a method that never needs
 the view free of the cost.
 
+## Measure an Analyzer
+
+The compiler reports per-analyzer time when the build asks for it.
+
+```bash
+dotnet build src/E128.Analyzers/E128.Analyzers.csproj -p:ReportAnalyzer=true -v:d
+```
+
+The report lands in the verbose build output. It writes no separate file. It
+holds a `Total analyzer execution time` line and a table of `Time (s) | % |
+Analyzer` rows, indented per diagnostic analyzer.
+
+Analyzers run concurrently, so the summed analyzer time can exceed the elapsed
+build time. Use the numbers to find an outlier against the other analyzers.
+Never treat one absolute number as a budget.
+
+Two conditions produce a clean-looking report with no content:
+
+- An up-to-date incremental build runs no compiler, so no report appears.
+- `-v:d` is required. A quieter build prints nothing.
+
+A structured log gives the same data in the Structured Log Viewer under
+Analyzer Summary: add `-bl:artifacts/analyzer.binlog`.
+
+To isolate one suspect, set `RunAnalyzersDuringBuild=false` and run the rule
+alone through a unit test or a single project.
+
 ## Exemptions
 
 - A syntax walk that exists only to answer one question at one node needs no
diff --git a/lode/dotnet/analyzers/pooled-buffer-rules.md b/lode/dotnet/analyzers/pooled-buffer-rules.md
index 19dac2a..d6c7380 100644
--- a/lode/dotnet/analyzers/pooled-buffer-rules.md
+++ b/lode/dotnet/analyzers/pooled-buffer-rules.md
@@ -1,5 +1,5 @@
 # Pooled Buffer and stackalloc Rules
-*Updated: 2026-11-01T20:55:29Z*
+*Updated: 2026-11-03T03:09:24Z*
 
 Reliability analyzers guard buffer memory. All report at Warning. E128109 and
 E128110 ship a code fix. E128111 ships no code fix.
diff --git a/lode/dotnet/analyzers/redundant-rules.md b/lode/dotnet/analyzers/redundant-rules.md
index 1906870..ed16162 100644
--- a/lode/dotnet/analyzers/redundant-rules.md
+++ b/lode/dotnet/analyzers/redundant-rules.md
@@ -1,5 +1,5 @@
 # Redundant E128 Rules
-*Updated: 2026-11-01T20:55:18Z*
+*Updated: 2026-11-03T03:09:24Z*
 
 An E128 rule is redundant when an installed third-party rule fires on the same code. The redundant rule gets `severity = none` in `.globalconfig` (section "E128"), even when the rule ships a code fix. The rule source stays in the project, so package consumers still receive the rule and its fix.
 
diff --git a/lode/dotnet/analyzers/summary.md b/lode/dotnet/analyzers/summary.md
new file mode 100644
index 0000000..6748b75
--- /dev/null
+++ b/lode/dotnet/analyzers/summary.md
@@ -0,0 +1,41 @@
+# Analyzers Summary
+*Updated: 2026-11-03T03:13:32Z*
+
+`src/E128.Analyzers` holds every E128 Roslyn rule. `src/E128.Analyzers.CodeFixers`
+holds the fix providers. The analyzer assembly targets `netstandard2.0` and
+holds no Workspaces reference. Each rule resolves its types once at compilation
+start, matches symbols rather than names, and reports only what its fix can
+repair.
+
+## Reading Order
+
+| Question                                         | File                                              |
+| ------------------------------------------------ | ------------------------------------------------- |
+| How do I add a rule?                             | [new-analyzer-checklist.md](new-analyzer-checklist.md) |
+| How does the rule read a user option?            | [configuration.md](configuration.md)              |
+| Which Roslyn API answers this question?          | [symbol-lookups.md](symbol-lookups.md)            |
+| How do I test the rule?                          | [testing.md](testing.md)                          |
+| How do I keep a rule fast?                       | [performance.md](performance.md)                  |
+| How do I write the fix?                          | [code-fix-patterns.md](code-fix-patterns.md)      |
+| What text may the fix write?                     | [code-fix-output.md](code-fix-output.md)          |
+| Which edit API do I use?                         | [code-fix-techniques.md](code-fix-techniques.md)  |
+| How is the package built and packed?             | [package-layout.md](package-layout.md)            |
+| Which buffer rules exist?                        | [pooled-buffer-rules.md](pooled-buffer-rules.md)  |
+| Which rules are disabled as redundant?           | [redundant-rules.md](redundant-rules.md)          |
+| What is the FIPS policy set?                     | [fips-rules.md](fips-rules.md)                    |
+
+## Shared Invariants
+
+- A rule reports a construct only after the semantic model confirms the symbol.
+- A rule reports error severity only where policy demands a hard gate. The FIPS
+  set is that case.
+- A code fix returns a changed document or offers no action. An offered fix
+  never leaves the document unchanged.
+- The fix and the analyzer read one table. The table is `internal static
+  readonly` on the analyzer.
+
+## Related
+
+- [../analyzers.md](../analyzers.md): analyzer strategy and the category map
+- [../analyzer-candidates.md](../analyzer-candidates.md): the ID map and free IDs
+- [../release-tracking.md](../release-tracking.md): Shipped and Unshipped files
diff --git a/lode/dotnet/analyzers/symbol-lookups.md b/lode/dotnet/analyzers/symbol-lookups.md
new file mode 100644
index 0000000..6c26607
--- /dev/null
+++ b/lode/dotnet/analyzers/symbol-lookups.md
@@ -0,0 +1,172 @@
+# Symbol and Semantic Lookups
+
+*Updated: 2026-11-03T03:14:50Z*
+
+Recipes for the question the rule asks once the syntax shape passed. The
+binding rules live in [new-analyzer-checklist.md](new-analyzer-checklist.md)
+§ Match Symbols, Not Names. The cost rules live in
+[performance.md](performance.md).
+
+## Reach a Member by Documentation Comment ID
+
+`GetTypeByMetadataName` reaches a type. A specific overload needs a
+documentation comment ID:
+
+```csharp
+var symbol = DocumentationCommentId.GetFirstSymbolForDeclarationId(
+    "M:System.Text.StringBuilder.Append(System.Char)",
+    compilation);
+```
+
+The ID format is the one the compiler writes into a documentation file. Take it
+from an IDE "Copy Reference" action or from the published API docs.
+
+The call returns null when no type carries the ID. Guard it like any metadata
+lookup.
+
+## Two Accessibility Questions
+
+The two questions are different, and the APIs differ.
+
+| Question                                    | Call                                                      |
+| ------------------------------------------- | --------------------------------------------------------- |
+| May this compilation call the symbol?       | `compilation.IsSymbolAccessibleWithin(symbol, compilation.Assembly)` |
+| Is the symbol visible outside its assembly? | Walk `DeclaredAccessibility` from the symbol up through `ContainingType` |
+
+The first call answers a remedy question, so use it when a code fix will write a
+call. The second answers a public API question, and it needs the walk. A symbol
+counts as visible when every level is `Public`, `Protected`, or
+`ProtectedOrInternal`.
+
+## Interface Implementation
+
+An explicit implementation needs no walk:
+
+```csharp
+if (member.ExplicitInterfaceImplementations.Length > 0)
+{
+    // Explicit implementation.
+}
+```
+
+An implicit implementation needs the interface list and the implementation
+lookup:
+
+```csharp
+foreach (var interfaceType in member.ContainingType.AllInterfaces)
+{
+    foreach (var interfaceMember in interfaceType.GetMembers(member.Name))
+    {
+        if (SymbolEqualityComparer.Default.Equals(
+                member.ContainingType.FindImplementationForInterfaceMember(interfaceMember),
+                member))
+        {
+            return true;
+        }
+    }
+}
+```
+
+## Operator and Extension Method Predicates
+
+```csharp
+methodSymbol.MethodKind is MethodKind.UserDefinedOperator or MethodKind.Conversion
+methodSymbol.IsExtensionMethod
+```
+
+`MethodKind.Conversion` covers both the implicit and the explicit conversion
+operator. Test `MethodKind.UserDefinedOperator` for an arithmetic or comparison
+operator.
+
+## Namespace and Display Name
+
+`ContainingNamespace` is null for the global namespace. A null-conditional call
+returns null, and the caller decides the default.
+
+```csharp
+var format = SymbolDisplayFormat.FullyQualifiedFormat
+    .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);
+var namespaceName = type.ContainingNamespace?.ToDisplayString(format);
+```
+
+Omit the `global::` prefix when the text names a namespace rather than a type.
+
+## Data Flow and Control Flow
+
+Both APIs return a result with a `Succeeded` flag, and both return a result with
+no content when the region does not compile.
+
+```csharp
+var dataFlow = semanticModel.AnalyzeDataFlow(node);
+if (!dataFlow.Succeeded)
+{
+    return;
+}
+```
+
+`DataFlowAnalysis` exposes `ReadInside`, `WrittenInside`, `VariablesDeclared`,
+`CapturedInside`, and `UsedLocalFunctions`. `ControlFlowAnalysis` exposes
+`EntryPoints`, `ExitPoints`, `StartPointIsReachable`, `EndPointIsReachable`, and
+`ReturnStatements`.
+
+`CapturedInside` is the cheapest answer to "does this lambda close over a
+local". Use it instead of an ancestry walk.
+
+## Language Version
+
+The syntax tree carries the parse options, and the options carry the version:
+
+```csharp
+if (node.SyntaxTree.Options is CSharpParseOptions options
+    && options.LanguageVersion < LanguageVersion.CSharp10)
+{
+    return;
+}
+```
+
+Compare against a `LanguageVersion` member. Never parse the version from a
+string, and never infer the framework version from an assembly version. An
+assembly version heuristic breaks on .NET Framework, on Mono, and on a custom
+reference set.
+
+## Scope Work to One Symbol
+
+A rule that applies to one symbol registers a scoped start action, so the
+callback runs only for that symbol. Three levels exist.
+
+| Registration                     | Scope                                  |
+| -------------------------------- | -------------------------------------- |
+| `RegisterSymbolStartAction`      | One named type or method, plus its end |
+| `RegisterOperationBlockStartAction` | One method, accessor, or field body |
+| `RegisterCompilationStartAction` | The whole compilation                  |
+
+The middle level answers "only inside a member that carries this attribute":
+
+```csharp
+start.RegisterOperationBlockStartAction(block =>
+{
+    if (!block.OwningSymbol.GetAttributes().Any(
+            attribute => SymbolEqualityComparer.Default
+                .Equals(attribute.AttributeClass, auditedAttribute)))
+    {
+        return;
+    }
+
+    block.RegisterOperationAction(
+        operation => AnalyzeInvocation(operation),
+        OperationKind.Invocation);
+});
+```
+
+The nested registration is the point. The inner action runs only for the
+attributed body, so the rule pays no cost elsewhere.
+
+`RegisterSymbolStartAction` also carries `RegisterSymbolEndAction`, which runs
+after the symbol's own callbacks. Both are available from Roslyn 3.0 onward.
+
+## Related
+
+- [New Analyzer Checklist](new-analyzer-checklist.md): gates and symbol rules
+- [Analyzer Performance](performance.md): cost of each lookup
+- [Code Fix Output](code-fix-output.md): trivia and emitted text
+- [Analyzer Testing](testing.md): driving the path in a test
diff --git a/lode/dotnet/analyzers/testing.md b/lode/dotnet/analyzers/testing.md
new file mode 100644
index 0000000..621e3f8
--- /dev/null
+++ b/lode/dotnet/analyzers/testing.md
@@ -0,0 +1,126 @@
+# Analyzer Testing
+
+*Updated: 2026-11-03T03:13:32Z*
+
+How a test drives an analyzer and a code fix. The harness types live in
+`Microsoft.CodeAnalysis.CSharp.Testing`. The repo wrapper lives in
+`tests/E128.Analyzers.Tests/CodeFixTestRunner.cs`.
+
+Run the suite with `scripts/test.sh ClassName` or `scripts/test.sh --all`.
+Never run raw `dotnet test`. See [.NET Testing](../testing.md).
+
+## Markup Forms
+
+The verifier reads the expected diagnostic from the source text. Each form
+answers a different question.
+
+| Form          | Use                                                  |
+| ------------- | ---------------------------------------------------- |
+| `[|code|]`    | An analyzer that declares exactly one descriptor      |
+| `{\|E128NNN:code\|}` | A named diagnostic, for any analyzer           |
+| `{\|#0:code\|}`     | A keyed location, paired with `.WithLocation(0)` |
+| `$$`          | A bare position, paired with `.WithLocation(line, column)` |
+
+`[|code|]` misbehaves on a multi-descriptor analyzer, and most E128 analyzers
+declare one descriptor per rule. Write the named form
+`{|E128NNN:code|}`.
+
+Two diagnostics on one line need the full closing form `|#1}`. The short form
+cannot resolve which marker closes which span.
+
+`.WithSpan(new FileLinePositionSpan(...))` checks the span and its length.
+`.WithLocation(...)` checks the start position only.
+
+## Expected Compiler Diagnostics
+
+An analyzer runs on a buffer that can fail to compile. A test source that does
+not compile needs the compiler diagnostic marked in the source, or the verifier
+reports an unexpected diagnostic:
+
+```csharp
+int x = {|CS0029:"abc"|};
+```
+
+Marking the compiler error is the test of a guard path. A rule that reads a
+symbol with no binding must stay silent, and the test proves it.
+
+## What a Code Fix Test Asserts
+
+`VerifyCodeFixAsync` runs four checks:
+
+1. The expected diagnostics are reported.
+2. The fixed result holds no diagnostic that a fix can still repair.
+3. Applying one fix at a time produces the expected output.
+4. Fix All produces the expected output, when the provider offers one.
+
+Check 4 means every code fix test exercises the fix-all provider. A provider
+that returns a broken `FixAllProvider` fails a test that never mentions fix-all.
+
+Set the fixed text equal to the input text to assert that a diagnostic is
+reported with no fix offered.
+
+Keep the analyzer and the fix in one test. A split test loses checks 3 and 4
+and duplicates the source text.
+
+## Supply a Configuration Option
+
+`TestState.AnalyzerConfigFiles` holds a global config. Add it before the run:
+
+```csharp
+test.TestState.AnalyzerConfigFiles.Add(
+    ("/.globalconfig", "is_global = true\ne128_minimum_framework_version = 100"));
+```
+
+`is_global = true` is the header that makes the file global. Without it, the
+keys do not reach `GlobalOptions`.
+
+`TestState.AdditionalFiles` takes `(filename, content)` tuples, for a rule that
+reads a structured file. See [configuration.md](configuration.md).
+
+## Roslyn Version Alignment
+
+The testing packages depend on an older Roslyn than the analyzer may use. When a
+rule uses a language feature that the pinned testing Roslyn lacks, override the
+Workspaces package in the test project to match the analyzer version.
+
+```xml
+<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces"
+                  Version="$(RoslynVersion)" />
+```
+
+The misalignment surfaces either as a parse failure in the test source or as a
+load exception. Both read like an analyzer defect.
+
+## Obsolete Package Surface
+
+The framework-suffixed packages (`.MSTest`, `.NUnit`, `.XUnit`) are obsolete.
+Use the generic verifiers. The migration changes three things:
+
+- Drop the suffix from the package name.
+- Import `Microsoft.CodeAnalysis.CSharp.Testing` instead of the framework
+  sub-namespace.
+- Add the verifier type argument:
+  `CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>`.
+
+No package declares xUnit v3 support. Treat that combination as unverified, and
+confirm the test count in the run output.
+
+## Repo Harness
+
+- Build the test directly with `CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>`
+  or `CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>`. Most test files
+  call `test.RunAsync()`, not a static verifier helper.
+- Route a code fix test through `CodeFixTestRunner.RunAsync` for a fix and
+  `CodeFixTestRunner.NoFixAsync` for a decline.
+- The shared stub types live in `tests/E128.Analyzers.Tests/TestStubs.cs`.
+- `ReferenceAssemblies` default to the version in `tests/.globalconfig` through
+  `e128_minimum_framework_version`.
+- xUnit v3 runs on the Microsoft Testing Platform. Only `scripts/test.sh`
+  works.
+
+## Related
+
+- [New Analyzer Checklist](new-analyzer-checklist.md): registration and gates
+- [Analyzer Configuration](configuration.md): the option the test supplies
+- [Code Fix Patterns](code-fix-patterns.md): the fix a test drives
+- [.NET Testing](../testing.md): runner, categories, conventions
diff --git a/lode/lode-map.md b/lode/lode-map.md
index 9e13794..777ce42 100644
--- a/lode/lode-map.md
+++ b/lode/lode-map.md
@@ -1,5 +1,5 @@
 # Lode Map
-*Updated: 2026-11-01T20:54:44Z*
+*Updated: 2026-11-03T03:13:32Z*
 
 ## Generated Docs
 
@@ -32,15 +32,19 @@
 
 ### dotnet/analyzers/
 
+- [summary.md](dotnet/analyzers/summary.md): Domain snapshot, reading order, shared invariants
 - [new-analyzer-checklist.md](dotnet/analyzers/new-analyzer-checklist.md): Pre-flight checks, netstandard2.0 pitfalls, nullable Roslyn API surprises
+- [configuration.md](dotnet/analyzers/configuration.md): Option surfaces, key naming, severity keys, MSBuild properties, additional files
+- [symbol-lookups.md](dotnet/analyzers/symbol-lookups.md): Documentation comment IDs, accessibility, interface implementation, data and control flow, scoped registration
+- [testing.md](dotnet/analyzers/testing.md): Markup forms, code fix assertions, option injection, Roslyn version alignment
 - [redundant-rules.md](dotnet/analyzers/redundant-rules.md): E128 rules disabled because third-party analyzers cover them
-- [code-fix-patterns.md](dotnet/analyzers/code-fix-patterns.md): Code fix implementation patterns, BatchFixer vs SequentialRename, AddUsing blank line behavior
-- [code-fix-output.md](dotnet/analyzers/code-fix-output.md): Rules for the source a fix writes: valid literals, formatter placement, single insert, rewrite shape guards
-- [code-fix-trivia.md](dotnet/analyzers/code-fix-trivia.md): Trivia the fix must keep: absent whitespace, replacement literals, member removal
-- [pooled-buffer-rules.md](dotnet/analyzers/pooled-buffer-rules.md): E128109 stackalloc limits, E128110 pooled buffer zeroing, E128111 use after Return
+- [code-fix-patterns.md](dotnet/analyzers/code-fix-patterns.md): Code fix structure: decision list, code-aware library, AddUsing, span widening, tables, registration
+- [code-fix-output.md](dotnet/analyzers/code-fix-output.md): Text the fix writes and keeps: literals, formatter, inserted members, trivia, rewrite shape guards
+- [code-fix-techniques.md](dotnet/analyzers/code-fix-techniques.md): Fix-all providers, DocumentEditor, speculative bind, error-count guard, generator, attribute update
 - [performance.md](dotnet/analyzers/performance.md): Analyzer hot-path rules: bind late, resolve types once, scan each scope once
-- [code-fix-techniques.md](dotnet/analyzers/code-fix-techniques.md): DocumentEditor, speculative bind, error-count guard, generator, attribute update
-- [package-layout.md](dotnet/analyzers/package-layout.md): two assemblies, per-Roslyn packaging, banned symbols, build metadata
+- [package-layout.md](dotnet/analyzers/package-layout.md): two assemblies, NuGet analyzer layout, per-Roslyn packaging, build metadata
+- [pooled-buffer-rules.md](dotnet/analyzers/pooled-buffer-rules.md): E128109 stackalloc limits, E128110 pooled buffer zeroing, E128111 use after Return
+- [fips-rules.md](dotnet/analyzers/fips-rules.md): FIPS rule map, policy values, shared symbol and constant helpers
 
 ## dotnet-reference/
 
diff --git a/lode/terminology.md b/lode/terminology.md
index ccd26ca..9a0acb4 100644
--- a/lode/terminology.md
+++ b/lode/terminology.md
@@ -1,5 +1,5 @@
 # Terminology
-*Updated: 2026-11-01T20:55:03Z*
+*Updated: 2026-11-03T01:04:17Z*
 
 - **CPM**: Central Package Management. NuGet feature where all package versions are declared in `Directory.Packages.props` rather than per-project.
 - **MTP**: Microsoft Testing Platform. The modern test execution platform used by xUnit v3, replacing the legacy vstest runner.
@@ -18,7 +18,7 @@
 - **Diagnostic ID**: The `E128xxx` number assigned to each custom analyzer rule, for example `E128064`. The `E128.Analyzers` README lists the full rule table.
 - **YAGNI**: You Aren't Gonna Need It. A principle that balances SOLID. Build an abstraction only when the code needs it now, not for a hypothetical future need.
 - **TOCTOU**: Time-Of-Check To Time-Of-Use. A race condition where a file changes between a check and a later use. Analyzer `E128056` flags this pattern.
-- **FIPS**: Federal Information Processing Standards. A hash-algorithm compliance target that analyzer `E128071` enforces. The container's stock Ubuntu Resolute OpenSSL package ships no FIPS provider.
+- **FIPS**: Federal Information Processing Standards. A cryptography compliance target. The rules E128071, E128075, and E128142 to E128151 enforce the policy. The container's stock Ubuntu Resolute OpenSSL package ships no FIPS provider.
 - **gitleaks**: An open source secret scanner. `scripts/internal/precommit.sh` runs it on the staged diff, and the CI `secrets` job runs it on the full history. See `lode/infrastructure/secret-scanning.md`.
 - **Deterministic Script**: A `scripts/*.sh` wrapper that replaces an ad hoc command with a repeatable, token-free operation. See `.claude/rules/deterministic-scripts.md` for the full routing table.
 - **Shell Lint**: The `shellcheck` pass over every tracked `.sh` file. `scripts/lint-shell.sh` owns it. See `.claude/rules/shellcheck-validation.md`.
diff --git a/scripts/internal/analyzer-release-check.sh b/scripts/internal/analyzer-release-check.sh
index 91b68ae..b7a9cba 100755
--- a/scripts/internal/analyzer-release-check.sh
+++ b/scripts/internal/analyzer-release-check.sh
@@ -35,12 +35,14 @@ while IFS= read -r id; do
     source_ids+=("$id")
 done < <(rg -o '"E128\d{3}"' "$ANALYZER_DIR/" --no-filename 2>/dev/null | tr -d '"' | sort -u)
 
-# 2. Extract IDs from Unshipped.md
+# 2. Extract IDs from the New Rules section of Unshipped.md.
+# A rule under Changed Rules is already shipped, so its old Shipped entry is
+# expected and must not count as a duplicate.
 unshipped_ids=()
 if [[ -f "$UNSHIPPED" ]]; then
     while IFS= read -r id; do
         unshipped_ids+=("$id")
-    done < <(rg -o 'E128\d{3}' "$UNSHIPPED" 2>/dev/null | sort -u)
+    done < <(awk '/^### Changed Rules/ { exit } { print }' "$UNSHIPPED" | rg -o 'E128\d{3}' | sort -u)
 fi
 
 # 3. Extract IDs from Shipped.md
diff --git a/src/E128.Analyzers.CodeFixers/Security/InsecureRandomInCryptoContextCodeFixProvider.cs b/src/E128.Analyzers.CodeFixers/Security/InsecureRandomInCryptoContextCodeFixProvider.cs
index 06b0daa..c6fce41 100644
--- a/src/E128.Analyzers.CodeFixers/Security/InsecureRandomInCryptoContextCodeFixProvider.cs
+++ b/src/E128.Analyzers.CodeFixers/Security/InsecureRandomInCryptoContextCodeFixProvider.cs
@@ -1,6 +1,7 @@
 using System;
 using System.Collections.Immutable;
 using System.Composition;
+using System.Linq;
 using System.Threading;
 using System.Threading.Tasks;
 using Microsoft.CodeAnalysis;
@@ -8,83 +9,293 @@ using Microsoft.CodeAnalysis.CodeActions;
 using Microsoft.CodeAnalysis.CodeFixes;
 using Microsoft.CodeAnalysis.CSharp;
 using Microsoft.CodeAnalysis.CSharp.Syntax;
+using Microsoft.CodeAnalysis.Editing;
+using Microsoft.CodeAnalysis.Simplification;
 
 namespace E128.Analyzers.Security;
 
+/// <summary>
+///     Replaces <c lang="csharp">Random.Shared.Next()</c>, <c lang="csharp">Next(max)</c> and
+///     <c lang="csharp">Next(min, max)</c> with the <c lang="csharp">RandomNumberGenerator.GetInt32</c> overload that has
+///     the same bounds. The fix is not offered for any other Random member, or unless the bounds are compile-time
+///     constants that describe a non-empty range, because <c lang="csharp">GetInt32</c> throws for an empty range where
+///     <c lang="csharp">Random.Next</c> returns the lower bound. Named arguments map to
+///     <c lang="csharp">GetInt32</c> positions through the parameters of the bound <c lang="csharp">Random.Next</c>
+///     overload, so <c lang="csharp">Next(maxValue: 7, minValue: 1)</c> becomes
+///     <c lang="csharp">GetInt32(1, 7)</c>. The replacement draws from a different random source on purpose, so values
+///     differ from the original. The generated name starts with <c lang="csharp">global::</c> so a local or member named
+///     <c lang="csharp">System</c> cannot capture it.
+/// </summary>
 [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InsecureRandomInCryptoContextCodeFixProvider))]
 [Shared]
 public sealed class InsecureRandomInCryptoContextCodeFixProvider : E128CodeFixProviderBase
 {
+    private const string RandomNumberGeneratorTypeName = "System.Security.Cryptography.RandomNumberGenerator";
+
     public override ImmutableArray<string> FixableDiagnosticIds =>
         [InsecureRandomInCryptoContextAnalyzer.DiagnosticId];
 
-    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
+    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
     {
         var diagnostic = context.Diagnostics[0];
-        var node = root.FindNode(diagnostic.Location.SourceSpan);
+        var shared = root.FindNode(diagnostic.Location.SourceSpan);
 
-        if (node is ObjectCreationExpressionSyntax)
+        // Random.Shared.Next(...): the diagnostic covers 'Random.Shared'.
+        if (shared is not MemberAccessExpressionSyntax
+            || shared.Parent is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Next" } next
+            || next.Expression != shared
+            || next.Parent is not InvocationExpressionSyntax invocation
+            || invocation.Expression != next)
         {
-            return Task.CompletedTask;
+            return;
         }
 
-        if (node is not MemberAccessExpressionSyntax randomSharedAccess)
+        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
+        if (model is null
+            || model.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
+            || !IsRandomNext(method)
+            || !TryGetBounds(invocation, method, model, context.CancellationToken, out var bounds))
         {
-            return Task.CompletedTask;
+            return;
         }
 
-        var match = TryGetFixableInvocation(randomSharedAccess);
-        if (match is null)
+        // Bind the replacement where it will go, so a source type that shadows RandomNumberGenerator, or a
+        // missing GetInt32 overload, means no fix rather than code that does not compile.
+        var replacement = CreateReplacement(invocation, bounds);
+        if (!BindsToGetInt32(model, invocation, replacement))
         {
-            return Task.CompletedTask;
+            return;
         }
 
-        var (outerInvocation, methodName) = match.Value;
-
         context.RegisterCodeFix(
             CodeAction.Create(
-                $"Use RandomNumberGenerator.{methodName}()",
-                ct => ReplaceWithRandomNumberGeneratorAsync(
-                    context.Document, outerInvocation, methodName, ct),
-                nameof(InsecureRandomInCryptoContextCodeFixProvider)),
+                "Use RandomNumberGenerator.GetInt32",
+                ct => ReplaceAsync(context.Document, root, invocation, replacement, ct),
+                "UseGetInt32"),
             diagnostic);
+    }
+
+    private static async Task<Document> ReplaceAsync(
+        Document document,
+        SyntaxNode root,
+        InvocationExpressionSyntax invocation,
+        InvocationExpressionSyntax replacement,
+        CancellationToken cancellationToken)
+    {
+        var changed = document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement));
+
+        // Add the using directive for the fully qualified name, then shorten the name.
+        changed = await ImportAdder.AddImportsAsync(
+            changed,
+            Simplifier.AddImportsAnnotation,
+            cancellationToken: cancellationToken).ConfigureAwait(false);
 
-        return Task.CompletedTask;
+        return await Simplifier.ReduceAsync(
+            changed,
+            Simplifier.Annotation,
+            cancellationToken: cancellationToken).ConfigureAwait(false);
     }
 
-    private static (InvocationExpressionSyntax Invocation, string MethodName)? TryGetFixableInvocation(
-        MemberAccessExpressionSyntax randomSharedAccess)
+    /// <summary>
+    ///     Returns true when the replacement binds to an accessible static <c lang="csharp">GetInt32</c> overload of the
+    ///     referenced <c lang="csharp">System.Security.Cryptography.RandomNumberGenerator</c>, with one int parameter per
+    ///     argument.
+    /// </summary>
+    private static bool BindsToGetInt32(
+        SemanticModel model,
+        InvocationExpressionSyntax invocation,
+        InvocationExpressionSyntax replacement)
     {
-        return randomSharedAccess.Parent is MemberAccessExpressionSyntax methodAccess
-               && string.Equals(methodAccess.Name.Identifier.ValueText, "Next", StringComparison.Ordinal)
-               && methodAccess.Parent is InvocationExpressionSyntax invocation
-               && invocation.ArgumentList.Arguments.Count is 1 or 2
-            ? (invocation, "GetInt32")
-            : null;
+        var expected = model.Compilation.GetTypeByMetadataName(RandomNumberGeneratorTypeName);
+        var bound = model.GetSpeculativeSymbolInfo(
+            invocation.SpanStart,
+            replacement,
+            SpeculativeBindingOption.BindAsExpression).Symbol;
+
+        return expected is not null
+               && !SymbolEqualityComparer.Default.Equals(expected.ContainingAssembly, model.Compilation.Assembly)
+               && bound is IMethodSymbol { Name: "GetInt32", IsStatic: true } getInt32
+               && SymbolEqualityComparer.Default.Equals(getInt32.ContainingType, expected)
+               && getInt32.Parameters.Length == replacement.ArgumentList.Arguments.Count
+               && getInt32.Parameters.All(parameter => parameter.Type.SpecialType == SpecialType.System_Int32)
+               && model.IsAccessible(invocation.SpanStart, getInt32);
     }
 
-    private static async Task<Document> ReplaceWithRandomNumberGeneratorAsync(
-        Document document,
-        InvocationExpressionSyntax outerInvocation,
-        string methodName,
-        CancellationToken cancellationToken)
+    private static bool IsRandomNext(IMethodSymbol method)
+    {
+        if (method.IsStatic ||
+            !string.Equals(method.Name, "Next", StringComparison.Ordinal) ||
+            !FipsSymbolHelpers.IsType(method.ContainingType, "Random", "System"))
+        {
+            return false;
+        }
+
+        foreach (var parameter in method.Parameters)
+        {
+            if (parameter.Type.SpecialType != SpecialType.System_Int32 || parameter.RefKind != RefKind.None)
+            {
+                return false;
+            }
+        }
+
+        return true;
+    }
+
+    /// <summary>
+    ///     Reads the arguments of <c lang="csharp">Next()</c>, <c lang="csharp">Next(max)</c> or
+    ///     <c lang="csharp">Next(min, max)</c> in parameter order, so a named argument lands where
+    ///     <c lang="csharp">GetInt32</c> expects it. The result is empty for <c lang="csharp">Next()</c>. It succeeds only
+    ///     when every bound is a compile-time constant after the implicit conversion to int, each parameter receives
+    ///     exactly one argument, no argument is <c lang="csharp">ref</c> or <c lang="csharp">out</c>, and the range is not
+    ///     empty. A variable bound could be zero or equal to the other bound, and <c lang="csharp">GetInt32</c> throws
+    ///     there where <c lang="csharp">Random.Next</c> returns a value. Constant bounds also mean the arguments have no
+    ///     side effects.
+    /// </summary>
+    private static bool TryGetBounds(
+        InvocationExpressionSyntax invocation,
+        IMethodSymbol method,
+        SemanticModel model,
+        CancellationToken cancellationToken,
+        out ImmutableArray<ExpressionSyntax> bounds)
     {
-        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
-        if (root is null)
+        bounds = default;
+
+        if (!TryOrderArguments(invocation, method, out var ordered))
         {
-            return document;
+            return false;
         }
 
-        var newExpression = SyntaxFactory.MemberAccessExpression(
-            SyntaxKind.SimpleMemberAccessExpression,
-            SyntaxFactory.IdentifierName("RandomNumberGenerator"),
-            SyntaxFactory.IdentifierName(methodName));
+        var values = new int[ordered.Length];
+        var builder = ImmutableArray.CreateBuilder<ExpressionSyntax>(ordered.Length);
+        for (var i = 0; i < ordered.Length; i++)
+        {
+            var expression = ordered[i];
+            if (expression is null ||
+                !TryGetConstantInt(model.GetConstantValue(expression, cancellationToken), out values[i]))
+            {
+                return false;
+            }
+
+            builder.Add(expression);
+        }
+
+        // Next(max) accepts 0 and GetInt32(0) throws, so the range must hold at least one value.
+        var isNonEmpty = values.Length switch
+        {
+            0 => true,
+            1 => values[0] > 0,
+            2 => values[1] > values[0],
+            _ => false
+        };
+
+        if (!isNonEmpty)
+        {
+            return false;
+        }
 
-        var newInvocation = SyntaxFactory.InvocationExpression(newExpression, outerInvocation.ArgumentList)
-            .WithLeadingTrivia(outerInvocation.GetLeadingTrivia())
-            .WithTrailingTrivia(outerInvocation.GetTrailingTrivia());
+        bounds = builder.MoveToImmutable();
+
+        return true;
+    }
+
+    /// <summary>
+    ///     Places each argument at its parameter position, so a named argument lands where the fix expects it.
+    ///     Returns false when an argument is <c lang="csharp">ref</c> or <c lang="csharp">out</c>, when the count
+    ///     does not match the parameter count, or when a parameter receives more than one argument.
+    /// </summary>
+    private static bool TryOrderArguments(
+        InvocationExpressionSyntax invocation,
+        IMethodSymbol method,
+        out ExpressionSyntax?[] ordered)
+    {
+        ordered = [];
+
+        var arguments = invocation.ArgumentList.Arguments;
+        if (arguments.Count != method.Parameters.Length)
+        {
+            return false;
+        }
+
+        ordered = new ExpressionSyntax?[arguments.Count];
+        for (var i = 0; i < arguments.Count; i++)
+        {
+            var argument = arguments[i];
+            if (!argument.RefKindKeyword.IsKind(SyntaxKind.None))
+            {
+                return false;
+            }
+
+            var ordinal = argument.NameColon is { } nameColon
+                ? IndexOfParameter(method, nameColon.Name.Identifier.ValueText)
+                : i;
+
+            if (ordinal < 0 || ordered[ordinal] is not null)
+            {
+                return false;
+            }
+
+            ordered[ordinal] = argument.Expression;
+        }
+
+        return true;
+    }
+
+    private static int IndexOfParameter(IMethodSymbol method, string name)
+    {
+        for (var i = 0; i < method.Parameters.Length; i++)
+        {
+            if (string.Equals(method.Parameters[i].Name, name, StringComparison.Ordinal))
+            {
+                return i;
+            }
+        }
+
+        return -1;
+    }
+
+    /// <summary>
+    ///     Reads a constant that converts implicitly to int: <c lang="csharp">int</c>, <c lang="csharp">short</c>,
+    ///     <c lang="csharp">ushort</c>, <c lang="csharp">byte</c>, <c lang="csharp">sbyte</c>, or <c lang="csharp">char</c>.
+    /// </summary>
+    private static bool TryGetConstantInt(Optional<object?> constant, out int value)
+    {
+        int? converted = constant.HasValue
+            ? constant.Value switch
+            {
+                int i => i,
+                short s => s,
+                ushort u => u,
+                byte b => b,
+                sbyte sb => sb,
+                char c => c,
+                _ => null
+            }
+            : null;
+
+        value = converted.GetValueOrDefault();
+
+        return converted.HasValue;
+    }
+
+    private static InvocationExpressionSyntax CreateReplacement(
+        InvocationExpressionSyntax invocation,
+        ImmutableArray<ExpressionSyntax> bounds)
+    {
+        var arguments = bounds.IsEmpty
+            ? SyntaxFactory.SeparatedList(new[] { SyntaxFactory.Argument(SyntaxFactory.ParseExpression("int.MaxValue")) })
+            : SyntaxFactory.SeparatedList(
+                bounds.Select(bound => SyntaxFactory.Argument(bound.WithoutTrivia())),
+                Enumerable.Repeat(
+                    SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space),
+                    bounds.Length - 1));
 
-        var newRoot = root.ReplaceNode(outerInvocation, newInvocation);
-        return document.WithSyntaxRoot(newRoot);
+        return SyntaxFactory.InvocationExpression(
+                SyntaxFactory.MemberAccessExpression(
+                    SyntaxKind.SimpleMemberAccessExpression,
+                    SyntaxFactory.ParseExpression("global::" + RandomNumberGeneratorTypeName),
+                    SyntaxFactory.IdentifierName("GetInt32")),
+                SyntaxFactory.ArgumentList(arguments))
+            .WithTriviaFrom(invocation)
+            .WithAdditionalAnnotations(Simplifier.AddImportsAnnotation, Simplifier.Annotation);
     }
 }
diff --git a/src/E128.Analyzers/AnalyzerReleases.Unshipped.md b/src/E128.Analyzers/AnalyzerReleases.Unshipped.md
index c903787..2d64ec3 100644
--- a/src/E128.Analyzers/AnalyzerReleases.Unshipped.md
+++ b/src/E128.Analyzers/AnalyzerReleases.Unshipped.md
@@ -2,3 +2,20 @@
 
 Rule ID | Category | Severity | Notes
 --------|----------|----------|-------
+E128142 | Security | Error    | Do not instantiate a Managed or CNG cryptography type
+E128143 | Security | Error    | Use an RSA key of at least 3072 bits or an approved curve
+E128144 | Security | Error    | Use an authenticated cipher mode
+E128145 | Security | Error    | Use a strong PBKDF2 configuration
+E128146 | Security | Error    | Use TLS 1.2 or later
+E128147 | Security | Error    | RNGCryptoServiceProvider is deprecated
+E128148 | Security | Error    | Do not use a cipher excluded by policy
+E128149 | Security | Error    | Use the full 16-byte AES-GCM tag
+E128150 | Security | Error    | Do not use SHA-3, SHAKE or KMAC
+E128151 | Security | Error    | Use an HMAC key at least as long as the hash output
+
+### Changed Rules
+
+Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
+--------|--------------|--------------|--------------|--------------|-------
+E128071 | Security     | Error        | Security     | Warning      | FIPS cryptography policy. A broken algorithm is a build-breaking defect.
+E128075 | Security     | Error        | Security     | Warning      | FIPS cryptography policy. A predictable RNG in a security context is a build-breaking defect.
diff --git a/src/E128.Analyzers/E128.Analyzers.csproj b/src/E128.Analyzers/E128.Analyzers.csproj
index 6cd1f7f..f2deda3 100644
--- a/src/E128.Analyzers/E128.Analyzers.csproj
+++ b/src/E128.Analyzers/E128.Analyzers.csproj
@@ -12,13 +12,13 @@
     <NoWarn>$(NoWarn);RS2007;RS0016;RS0017;NU5128</NoWarn>
     <!-- PolySharp: netstandard2.0 polyfill for nullable analysis attributes only -->
     <PolySharpIncludeGeneratedTypes>
-      System.Diagnostics.CodeAnalysis.NotNullWhenAttribute
+      System.Diagnostics.CodeAnalysis.NotNullWhenAttribute;System.Diagnostics.CodeAnalysis.MaybeNullWhenAttribute
     </PolySharpIncludeGeneratedTypes>
   </PropertyGroup>
 
   <!-- NuGet Packaging -->
   <PropertyGroup>
-    <Version>1.60.4</Version>
+    <Version>1.65.1</Version>
     <IsPackable>true</IsPackable>
     <IncludeBuildOutput>false</IncludeBuildOutput>
     <DevelopmentDependency>true</DevelopmentDependency>
diff --git a/src/E128.Analyzers/README.md b/src/E128.Analyzers/README.md
index 7798baa..2561807 100644
--- a/src/E128.Analyzers/README.md
+++ b/src/E128.Analyzers/README.md
@@ -133,9 +133,19 @@ All rules default to **Warning** severity unless noted. Every rule includes a co
 
 | Rule    | Title                                                                          | Code Fix |
 | ------- | ------------------------------------------------------------------------------ | -------- |
-| E128071 | Use a FIPS-approved hash algorithm                                             | Yes      |
+| E128071 | Use an approved cryptographic algorithm                                        | Yes      |
 | E128075 | Use `RandomNumberGenerator` instead of `Random` in crypto context              | Yes      |
 | E128091 | Use `ArgumentList` instead of a built `Arguments` string on `ProcessStartInfo` | Yes      |
+| E128142 | Do not instantiate a Managed or CNG cryptography type                          | No       |
+| E128143 | Use an RSA key of at least 3072 bits or an approved curve                      | No       |
+| E128144 | Use an authenticated cipher mode                                               | No       |
+| E128145 | Use a strong PBKDF2 configuration                                              | No       |
+| E128146 | Use TLS 1.2 or later                                                           | No       |
+| E128147 | RNGCryptoServiceProvider is deprecated                                         | No       |
+| E128148 | Do not use a cipher excluded by policy                                         | No       |
+| E128149 | Use the full 16-byte AES-GCM tag                                               | No       |
+| E128150 | Do not use SHA-3, SHAKE or KMAC                                                | No       |
+| E128151 | Use an HMAC key at least as long as the hash output                            | No       |
 
 ### Style
 
@@ -887,18 +897,22 @@ var buf = ArrayPool<char>.Shared.Rent(length);
 var buf = ArrayPool<char>.Shared.Rent(Math.Min(length, maxCapacity));
 ```
 
-### E128071 &mdash; FIPS-approved hash algorithm
+### E128071 &mdash; Approved cryptographic algorithm
 
-Flags use of non-FIPS algorithms (`MD5`, `SHA1`, `DES`, `RC2`, `TripleDES`, `HMACMD5`, `HMACSHA1`) in production code. Detects factory methods (`.Create()`, `.HashData()`) and constructors, including legacy concrete subclasses that bypass the base type name (`new MD5CryptoServiceProvider()`, `new SHA1Managed()`, etc.) via base-type-chain matching. FIPS 140-2 compliance requires SHA-256 or stronger.
+Flags algorithms that cryptography policy blocks: `MD5`, `SHA1`, `RIPEMD160`, `DES`, `RC2`, `TripleDES`, the MAC types built on them, `ChaCha20Poly1305` and `PasswordDeriveBytes`. It covers constructors, static `Create` and `HashData` members, method groups, legacy factories that select a weak algorithm by default, and any cryptography API that takes a blocked `HashAlgorithmName`.
+
+It also flags a constant blocked XML-DSig or XML-Enc URI, `CmsSigner.DigestAlgorithm` set to a blocked object identifier, `PbeEncryptionAlgorithm.TripleDes3KeyPkcs12`, `RSAEncryptionPadding.Pkcs1` on an RSA encryption call, the PKCS#1 key exchange formatters, and DSA signature generation. DSA verification stays allowed.
 
 ```csharp
 // Before (warns)
 using var md5 = MD5.Create();
 using var hmac = new HMACSHA1(key);
+var cipher = rsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);
 
 // After
 using var sha256 = SHA256.Create();
 using var hmac = new HMACSHA256(key);
+var cipher = rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
 ```
 
 ### E128072 &mdash; SHA256.Create() obsolete pattern (Info)
@@ -949,7 +963,11 @@ public readonly struct Point
 
 ### E128075 &mdash; Random in crypto context
 
-Flags `new Random()`, `Random.Shared`, and `Random.Next*` calls in methods whose names or surrounding types suggest cryptographic or security contexts (e.g., `Generate`, `Token`, `Secret`, `Password`, `Key`, `Nonce`, `Salt`). `System.Random` is not cryptographically secure. Use `RandomNumberGenerator` instead.
+Flags `System.Random`, and a class derived from it, in two cases. First, a random value flows into a variable, field, property or buffer with a security-sensitive name such as `token`, `salt`, `sessionId` or `key`. Second, a new instance or `Random.Shared` appears in a file that references a type from `System.Security.Cryptography` or `Microsoft.AspNetCore.Cryptography`. A file also counts as a cryptography file when it names a member of a cryptography type. `System.Random` is not cryptographically secure. Use `RandomNumberGenerator` instead.
+
+The check is a heuristic. It judges only the receiving name, and the file check reports every `Random` creation in a cryptography file. A sensitive name matches as a whole word, so `secretKey` reports and `secretary` does not. The name set also comes from the referenced assemblies.
+
+The code fix replaces `Random.Shared.Next` with `RandomNumberGenerator.GetInt32`. `Next()` becomes `GetInt32(int.MaxValue)`. The fix declines a variable bound, an empty range and `NextDouble`.
 
 ```csharp
 // Before (warns)
@@ -1449,6 +1467,128 @@ byte[] bytes = "abc"u8.ToArray();
 
 The rule needs C# 11 or later. The fix emits the bare literal when the target is a `ReadOnlySpan<byte>`. Every other target, including `Span<byte>`, keeps the `byte[]` shape through `ToArray()`.
 
+### E128142: Do not instantiate a Managed or CNG cryptography type
+
+Flags a direct `new` of a type whose name ends in `Managed` or `Cng`. A concrete type fixes one implementation. The abstract factory lets the platform select its cryptographic module. A `Cng` constructor that takes a key name, a `CngKey` or a `CngProvider` is not reported, because the factory cannot open a persisted key. `Rijndael` and `RijndaelManaged` are reported because AES does not allow their block sizes. Types that E128071 already blocks are skipped.
+
+```csharp
+// Before (warns)
+using var sha = new SHA256Managed();
+
+// After
+using var sha = SHA256.Create();
+```
+
+### E128143: Use an RSA key of at least 3072 bits or an approved curve
+
+Flags an RSA key below 3072 bits and an elliptic curve key below 384 bits. Approved curves are P-384 and P-521. A key size that the rule cannot read at compile time is reported, including one created by name, imported from parameters, a handle, a blob, PEM text or a certificate.
+
+```csharp
+// Before (warns)
+using var rsa = RSA.Create(2048);
+
+// After
+using var rsa = RSA.Create(3072);
+```
+
+### E128144: Use an authenticated cipher mode
+
+Flags `CipherMode.ECB`, `CipherMode.OFB` and `CipherMode.CFB`, and the `EncryptEcb`, `DecryptEcb`, `EncryptCfb` and `DecryptCfb` methods. ECB reveals plaintext structure. A comparison reads the mode and is not reported.
+
+```csharp
+// Before (warns)
+aes.Mode = CipherMode.ECB;
+
+// After
+aes.Mode = CipherMode.CBC;
+```
+
+### E128145: Use a strong PBKDF2 configuration
+
+Flags a PBKDF2 iteration count below 600,000 and an overload that defaults to 1,000 iterations. It also flags SHA-1 as the PRF and an iteration count that the rule cannot read at compile time. An `IterationCount` assignment on the same object replaces the default-count report.
+
+```csharp
+// Before (warns)
+using var kdf = new Rfc2898DeriveBytes("pw", salt, 1000, HashAlgorithmName.SHA256);
+
+// After
+using var kdf = new Rfc2898DeriveBytes("pw", salt, 600_000, HashAlgorithmName.SHA256);
+```
+
+### E128146: Use TLS 1.2 or later
+
+Flags `SslProtocols.Ssl2`, `Ssl3`, `Tls`, `Tls11` and `Default`, and the matching `SecurityProtocolType` members. It also flags a numeric cast that sets a weak protocol bit and a complement that enables every weak protocol. A comparison and a mask removal are not reported. `AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", true)` is reported.
+
+```csharp
+// Before (warns)
+var protocols = SslProtocols.Tls11;
+
+// After
+var protocols = SslProtocols.Tls12;
+```
+
+### E128147: RNGCryptoServiceProvider is deprecated
+
+Flags `new RNGCryptoServiceProvider()` where the target framework marks the type `[Obsolete]`, as on .NET 6 and later. On .NET Framework and netstandard2.0 the type is supported and the rule stays silent.
+
+```csharp
+// Before (warns)
+using var rng = new RNGCryptoServiceProvider();
+
+// After
+var bytes = RandomNumberGenerator.GetBytes(32);
+```
+
+### E128148: Do not use a cipher excluded by policy
+
+Flags a type whose name contains a whole word from the excluded list: Blowfish, RC4, Arcfour, CAST5, CAST6, IDEA and Skipjack. Third-party libraries such as BouncyCastle expose these ciphers. `IDEA` is also an ordinary word, so it needs a cipher role word at the end of the type name, a cipher base type, a cipher interface or a cryptography namespace. `IdeaEngine` reports, and `IdeaRankingAlgorithm` does not.
+
+The rule inspects a constructor, a static member in `Create`, `HashData`, `TryHashData` or `HashDataAsync`, and a method group. Types that E128071 already blocks are skipped, so one type produces one diagnostic.
+
+```csharp
+// Before (warns)
+var cipher = new BlowfishEngine();
+
+// After
+using var aes = Aes.Create();
+```
+
+### E128149: Use the full 16-byte AES-GCM tag
+
+Flags an `AesGcm` constructor whose `tagSizeInBytes` constant is below 16. It also flags an `Encrypt` or `Decrypt` call whose constant tag buffer is below 16 bytes. A tag size or buffer held in an unreassigned local or a readonly field resolves to its initializer.
+
+```csharp
+// Before (warns)
+using var aes = new AesGcm(key, 12);
+
+// After
+using var aes = new AesGcm(key, 16);
+```
+
+### E128150: Do not use SHA-3, SHAKE or KMAC
+
+Flags the `SHA3_*`, `Shake*`, `HMACSHA3_*` and `Kmac*` types, the `OaepSHA3_*` paddings, and a SHA-3 hash name stored in a variable. NIST SP 800-131A Rev. 2 accepts SHA-3. This rule is organizational policy, not a NIST statement. A read of `IsSupported` is not reported.
+
+```csharp
+// Before (warns)
+var hash = SHA3_256.HashData(data);
+
+// After
+var hash = SHA256.HashData(data);
+```
+
+### E128151: Use an HMAC key at least as long as the hash output
+
+Flags an HMAC key shorter than the hash output: 32 bytes for SHA-256, 48 for SHA-384 and 64 for SHA-512. The rule reads the constructor, the static `HashData` family, and a `Key` property assignment. A key whose length is not a compile-time constant is not reported.
+
+```csharp
+// Before (warns)
+using var hmac = new HMACSHA256(new byte[16]);
+
+// After
+using var hmac = new HMACSHA256(new byte[32]);
+```
+
 ### E128076 &mdash; Materialize QuerySelectorAll result before iterating
 
 Flags a `foreach` over an AngleSharp `QuerySelectorAll` result that does not pass through `ToList()` or `ToArray()`. The loop also reports when it reads a local variable initialized from an unmaterialized call. A call inside a lambda in the initializer is not reported. The collection is live, so a DOM change inside the loop moves elements that the loop has not visited yet.
diff --git a/src/E128.Analyzers/Security/FipsAesGcmTagSizeAnalyzer.cs b/src/E128.Analyzers/Security/FipsAesGcmTagSizeAnalyzer.cs
new file mode 100644
index 0000000..80c0c64
--- /dev/null
+++ b/src/E128.Analyzers/Security/FipsAesGcmTagSizeAnalyzer.cs
@@ -0,0 +1,89 @@
+using System;
+using System.Collections.Immutable;
+using Microsoft.CodeAnalysis;
+using Microsoft.CodeAnalysis.Diagnostics;
+using Microsoft.CodeAnalysis.Operations;
+
+namespace E128.Analyzers.Security;
+
+/// <summary>
+///     E128149: Reports an AES-GCM tag shorter than 16 bytes. The rule reads a constant <c lang="csharp">tagSizeInBytes</c>
+///     passed to the <c lang="csharp">AesGcm</c> constructor and the constant size of a tag buffer passed to
+///     <c lang="csharp">Encrypt</c> or <c lang="csharp">Decrypt</c>.
+/// </summary>
+[DiagnosticAnalyzer(LanguageNames.CSharp)]
+public sealed class FipsAesGcmTagSizeAnalyzer : DiagnosticAnalyzer
+{
+    internal const string DiagnosticId = "E128149";
+
+    private const int RequiredTagBytes = 16;
+
+    private static readonly DiagnosticDescriptor Rule = new(
+        DiagnosticId,
+        "Use the full 16-byte AES-GCM tag",
+        "AES-GCM tag size {0} bytes ({1} bits) is below the policy minimum of 16 bytes (128 bits)",
+        "Security",
+        DiagnosticSeverity.Error,
+        true,
+        "NIST SP 800-38D permits shorter tags in some cases, but policy requires the full 16-byte tag. A " +
+        "tag size or tag buffer held in an unreassigned local or a readonly field resolves to its initializer.");
+
+    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];
+
+    public override void Initialize(AnalysisContext context)
+    {
+        if (context is null)
+        {
+            throw new ArgumentNullException(nameof(context));
+        }
+
+        context.ConfigureGeneratedCodeAnalysis(
+            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
+        context.EnableConcurrentExecution();
+
+        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
+        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
+    }
+
+    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
+    {
+        var creation = (IObjectCreationOperation)context.Operation;
+        if (!FipsSymbolHelpers.DerivesFromCryptographyType(creation.Type, "AesGcm"))
+        {
+            return;
+        }
+
+        if (FipsSymbolHelpers.ArgumentNamed(creation.Arguments, "tagSizeInBytes") is { } argument &&
+            FipsSymbolHelpers.TryGetResolvedConstant<int>(argument.Value, out var tagBytes) &&
+            tagBytes < RequiredTagBytes)
+        {
+            Report(context, creation, tagBytes);
+        }
+    }
+
+    private static void AnalyzeInvocation(OperationAnalysisContext context)
+    {
+        var invocation = (IInvocationOperation)context.Operation;
+        var method = invocation.TargetMethod;
+        if ((!string.Equals(method.Name, "Encrypt", StringComparison.Ordinal) &&
+             !string.Equals(method.Name, "Decrypt", StringComparison.Ordinal)) ||
+            !FipsSymbolHelpers.DerivesFromCryptographyType(method.ContainingType, "AesGcm"))
+        {
+            return;
+        }
+
+        if (FipsSymbolHelpers.ArgumentNamed(invocation.Arguments, "tag") is { } argument &&
+            FipsSymbolHelpers.TryGetConstantByteLength(argument.Value, out var tagBytes) &&
+            tagBytes < RequiredTagBytes)
+        {
+            Report(context, invocation, tagBytes);
+        }
+    }
+
+    private static void Report(OperationAnalysisContext context, IOperation operation, int tagBytes) =>
+        context.ReportDiagnostic(Diagnostic.Create(
+            Rule,
+            operation.Syntax.GetLocation(),
+            tagBytes,
+            tagBytes * 8));
+}
diff --git a/src/E128.Analyzers/Security/FipsCipherModeAnalyzer.cs b/src/E128.Analyzers/Security/FipsCipherModeAnalyzer.cs
new file mode 100644
index 0000000..6a96584
--- /dev/null
+++ b/src/E128.Analyzers/Security/FipsCipherModeAnalyzer.cs
@@ -0,0 +1,135 @@
+using System;
+using System.Collections.Immutable;
+using System.Globalization;
+using Microsoft.CodeAnalysis;
+using Microsoft.CodeAnalysis.Diagnostics;
+using Microsoft.CodeAnalysis.Operations;
+
+namespace E128.Analyzers.Security;
+
+/// <summary>
+///     E128144: Reports the ECB, OFB and CFB cipher modes. The rule fires on a reference to a
+///     <c lang="csharp">CipherMode</c> member, on the mode-specific <c lang="csharp">SymmetricAlgorithm</c> methods,
+///     and on a constant numeric cast such as <c lang="csharp">(CipherMode)2</c>.
+/// </summary>
+[DiagnosticAnalyzer(LanguageNames.CSharp)]
+public sealed class FipsCipherModeAnalyzer : DiagnosticAnalyzer
+{
+    internal const string DiagnosticId = "E128144";
+
+    private static readonly ImmutableHashSet<string> ForbiddenModes = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "ECB",
+        "OFB",
+        "CFB");
+
+    private static readonly DiagnosticDescriptor Rule = new(
+        DiagnosticId,
+        "Use an authenticated cipher mode",
+        "Cipher mode '{0}' is not permitted; use AES-GCM, or CBC with a random IV and a MAC",
+        "Security",
+        DiagnosticSeverity.Error,
+        true,
+        "ECB reveals plaintext structure. The OFB and CFB modes are not permitted by policy. Use " +
+        "authenticated encryption, or CBC with an explicit random IV and a MAC.");
+
+    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];
+
+    public override void Initialize(AnalysisContext context)
+    {
+        if (context is null)
+        {
+            throw new ArgumentNullException(nameof(context));
+        }
+
+        context.ConfigureGeneratedCodeAnalysis(
+            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
+        context.EnableConcurrentExecution();
+
+        context.RegisterOperationAction(AnalyzeFieldReference, OperationKind.FieldReference);
+        context.RegisterOperationAction(AnalyzeNumericCast, OperationKind.Conversion);
+        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
+    }
+
+    private static void AnalyzeFieldReference(OperationAnalysisContext context)
+    {
+        var fieldReference = (IFieldReferenceOperation)context.Operation;
+        var field = fieldReference.Field;
+
+        if (ForbiddenModes.Contains(field.Name) &&
+            FipsSymbolHelpers.IsCryptographyType(field.ContainingType, "CipherMode") &&
+            !FipsSymbolHelpers.IsComparison(fieldReference))
+        {
+            context.ReportDiagnostic(Diagnostic.Create(
+                Rule,
+                fieldReference.Syntax.GetLocation(),
+                field.Name));
+        }
+    }
+
+    /// <summary>
+    ///     Reports an explicit cast of a number to <c lang="csharp">CipherMode</c> whose constant is ECB (2), OFB (3)
+    ///     or CFB (4), such as <c lang="csharp">(CipherMode)2</c>.
+    /// </summary>
+    private static void AnalyzeNumericCast(OperationAnalysisContext context)
+    {
+        var conversion = (IConversionOperation)context.Operation;
+        if (conversion.IsImplicit ||
+            !FipsSymbolHelpers.IsCryptographyType(conversion.Type, "CipherMode") ||
+            conversion.Operand.Type?.TypeKind == TypeKind.Enum ||
+            conversion.ConstantValue is not { HasValue: true, Value: { } value } ||
+            ModeOfValue(Convert.ToInt64(value, CultureInfo.InvariantCulture)) is not { } mode ||
+            FipsSymbolHelpers.IsComparison(conversion))
+        {
+            return;
+        }
+
+        context.ReportDiagnostic(Diagnostic.Create(
+            Rule,
+            conversion.Syntax.GetLocation(),
+            mode));
+    }
+
+    private static void AnalyzeInvocation(OperationAnalysisContext context)
+    {
+        var invocation = (IInvocationOperation)context.Operation;
+        var method = invocation.TargetMethod;
+
+        var mode = ModeOfMethod(method.Name);
+        if (mode is not null && FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "SymmetricAlgorithm"))
+        {
+            context.ReportDiagnostic(Diagnostic.Create(
+                Rule,
+                invocation.Syntax.GetLocation(),
+                mode));
+        }
+    }
+
+    // CipherMode values: CBC is 1, ECB is 2, OFB is 3, CFB is 4 and CTS is 5.
+    private static string? ModeOfValue(long value) => value switch
+    {
+        2 => "ECB",
+        3 => "OFB",
+        4 => "CFB",
+        _ => null,
+    };
+
+    private static string? ModeOfMethod(string name)
+    {
+        switch (name)
+        {
+            case "EncryptEcb":
+            case "DecryptEcb":
+            case "TryEncryptEcb":
+            case "TryDecryptEcb":
+                return "ECB";
+            case "EncryptCfb":
+            case "DecryptCfb":
+            case "TryEncryptCfb":
+            case "TryDecryptCfb":
+                return "CFB";
+            default:
+                return null;
+        }
+    }
+}
diff --git a/src/E128.Analyzers/Security/FipsDeprecatedRngAnalyzer.cs b/src/E128.Analyzers/Security/FipsDeprecatedRngAnalyzer.cs
new file mode 100644
index 0000000..7f5ac2e
--- /dev/null
+++ b/src/E128.Analyzers/Security/FipsDeprecatedRngAnalyzer.cs
@@ -0,0 +1,79 @@
+using System;
+using System.Collections.Immutable;
+using Microsoft.CodeAnalysis;
+using Microsoft.CodeAnalysis.Diagnostics;
+using Microsoft.CodeAnalysis.Operations;
+
+namespace E128.Analyzers.Security;
+
+/// <summary>
+///     E128147: Reports the deprecated <c lang="csharp">RNGCryptoServiceProvider</c>, and any class derived from it,
+///     where the target framework marks it <c lang="csharp">[Obsolete]</c>. On .NET Framework and netstandard2.0 it
+///     is the supported type.
+/// </summary>
+[DiagnosticAnalyzer(LanguageNames.CSharp)]
+public sealed class FipsDeprecatedRngAnalyzer : DiagnosticAnalyzer
+{
+    internal const string DiagnosticId = "E128147";
+
+    private static readonly DiagnosticDescriptor Rule = new(
+        DiagnosticId,
+        "RNGCryptoServiceProvider is deprecated",
+        "RNGCryptoServiceProvider is obsolete (SYSLIB0023); use the static RandomNumberGenerator methods, such as GetBytes or GetInt32",
+        "Security",
+        DiagnosticSeverity.Error,
+        true,
+        "RNGCryptoServiceProvider is obsolete in .NET 6 and later. Use the static RandomNumberGenerator " +
+        "methods, such as GetBytes, Fill and GetInt32.");
+
+    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];
+
+    public override void Initialize(AnalysisContext context)
+    {
+        if (context is null)
+        {
+            throw new ArgumentNullException(nameof(context));
+        }
+
+        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
+        context.EnableConcurrentExecution();
+
+        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
+    }
+
+    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
+    {
+        var creation = (IObjectCreationOperation)context.Operation;
+        if (FindCryptographyBase(creation.Type, "RNGCryptoServiceProvider") is { } rng && IsObsolete(rng))
+        {
+            context.ReportDiagnostic(Diagnostic.Create(Rule, creation.Syntax.GetLocation()));
+        }
+    }
+
+    private static ITypeSymbol? FindCryptographyBase(ITypeSymbol? type, string name)
+    {
+        for (var current = type; current is not null; current = current.BaseType)
+        {
+            if (FipsSymbolHelpers.IsCryptographyType(current, name))
+            {
+                return current;
+            }
+        }
+
+        return null;
+    }
+
+    private static bool IsObsolete(ITypeSymbol type)
+    {
+        foreach (var attribute in type.GetAttributes())
+        {
+            if (attribute.AttributeClass is { Name: "ObsoleteAttribute" } attributeClass &&
+                FipsSymbolHelpers.NamespaceEquals(attributeClass.ContainingNamespace, "System"))
+            {
+                return true;
+            }
+        }
+
+        return false;
+    }
+}
diff --git a/src/E128.Analyzers/Security/FipsDirectInstantiationAnalyzer.cs b/src/E128.Analyzers/Security/FipsDirectInstantiationAnalyzer.cs
new file mode 100644
index 0000000..29c9318
--- /dev/null
+++ b/src/E128.Analyzers/Security/FipsDirectInstantiationAnalyzer.cs
@@ -0,0 +1,172 @@
+using System;
+using System.Collections.Immutable;
+using Microsoft.CodeAnalysis;
+using Microsoft.CodeAnalysis.Diagnostics;
+using Microsoft.CodeAnalysis.Operations;
+
+namespace E128.Analyzers.Security;
+
+/// <summary>
+///     E128142: Do not instantiate a Managed or CNG cryptography type directly. Call the abstract factory so
+///     the platform selects its implementation. Rijndael is reported because AES does not allow its block sizes.
+///     A constructor that takes a key size or a curve reports the factory overload that keeps the argument.
+/// </summary>
+[DiagnosticAnalyzer(LanguageNames.CSharp)]
+public sealed class FipsDirectInstantiationAnalyzer : DiagnosticAnalyzer
+{
+    internal const string DiagnosticId = "E128142";
+
+    private static readonly DiagnosticDescriptor Rule = new(
+        DiagnosticId,
+        "Do not instantiate a Managed or CNG cryptography type",
+        "Do not instantiate '{0}' directly; call the factory '{1}' so the platform selects the implementation",
+        "Security",
+        DiagnosticSeverity.Error,
+        true,
+        "A concrete Managed or Cng type fixes one implementation. The abstract factory lets the platform " +
+        "choose its cryptographic module. A Cng constructor that takes a key name, a CngKey or a CngProvider " +
+        "is not reported, because the abstract factory cannot open a persisted key.");
+
+    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];
+
+    public override void Initialize(AnalysisContext context)
+    {
+        if (context is null)
+        {
+            throw new ArgumentNullException(nameof(context));
+        }
+
+        context.ConfigureGeneratedCodeAnalysis(
+            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
+        context.EnableConcurrentExecution();
+
+        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
+        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
+    }
+
+    /// <summary>Reports the static <c lang="csharp">Rijndael.Create()</c> factory, which returns the same non-AES type.</summary>
+    private static void AnalyzeInvocation(OperationAnalysisContext context)
+    {
+        var invocation = (IInvocationOperation)context.Operation;
+        var method = invocation.TargetMethod;
+        if (method.IsStatic &&
+            string.Equals(method.Name, "Create", StringComparison.Ordinal) &&
+            FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "Rijndael"))
+        {
+            Report(context, invocation, "Rijndael", "Aes.Create()");
+        }
+    }
+
+    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
+    {
+        var creation = (IObjectCreationOperation)context.Operation;
+        var type = creation.Type;
+        if (type is null)
+        {
+            return;
+        }
+
+        // E128071 already blocks these types, so factory advice would contradict it.
+        if (FipsSymbolHelpers.IsCryptographyType(type, FipsForbiddenTypes.All))
+        {
+            return;
+        }
+
+        var name = type.Name;
+        if (!FipsSymbolHelpers.IsInCryptographyNamespace(type))
+        {
+            // Rijndael can use a block size that AES does not allow. The name is ambiguous, so outside
+            // System.Security.Cryptography only a symmetric algorithm counts.
+            if ((string.Equals(name, "Rijndael", StringComparison.Ordinal) ||
+                 string.Equals(name, "RijndaelManaged", StringComparison.Ordinal)) &&
+                FipsSymbolHelpers.DerivesFromCryptographyType(type, "SymmetricAlgorithm"))
+            {
+                Report(context, creation, name, "Aes.Create()");
+            }
+
+            return;
+        }
+
+        if (string.Equals(name, "Rijndael", StringComparison.Ordinal) ||
+            string.Equals(name, "RijndaelManaged", StringComparison.Ordinal))
+        {
+            Report(context, creation, name, "Aes.Create()");
+            return;
+        }
+
+        if ((name.EndsWith("Managed", StringComparison.Ordinal) ||
+             (name.EndsWith("Cng", StringComparison.Ordinal) && !UsesCngKeyStorage(creation.Constructor))) &&
+            SuggestFactory(name, creation) is { } suggestion)
+        {
+            Report(context, creation, name, suggestion);
+        }
+    }
+
+    /// <summary>
+    ///     Returns true for a Cng constructor that takes a key name, <c lang="csharp">CngKey</c> or <c lang="csharp">CngProvider</c>. Those
+    ///     constructors open a persisted or hardware-backed key, which the abstract factory cannot do.
+    /// </summary>
+    private static bool UsesCngKeyStorage(IMethodSymbol? constructor)
+    {
+        if (constructor is null)
+        {
+            return false;
+        }
+
+        foreach (var parameter in constructor.Parameters)
+        {
+            if (parameter.Type.SpecialType == SpecialType.System_String ||
+                FipsSymbolHelpers.IsCryptographyType(parameter.Type, "CngKey") ||
+                FipsSymbolHelpers.IsCryptographyType(parameter.Type, "CngProvider"))
+            {
+                return true;
+            }
+        }
+
+        return false;
+    }
+
+    private static void Report(OperationAnalysisContext context, IOperation operation, string typeName, string suggestion) =>
+        context.ReportDiagnostic(Diagnostic.Create(
+            Rule,
+            operation.Syntax.GetLocation(),
+            typeName,
+            suggestion));
+
+    /// <summary>
+    ///     Best-effort factory suggestion: AesManaged becomes Aes.Create(), SHA256Cng becomes SHA256.Create(),
+    ///     <c lang="csharp">new RSACng(3072)</c> becomes <c lang="csharp">RSA.Create(3072)</c> and
+    ///     <c lang="csharp">new ECDsaCng(curve)</c> becomes <c lang="csharp">ECDsa.Create(curve)</c>.
+    ///     Returns null when the constructor takes arguments that no factory overload accepts.
+    /// </summary>
+    private static string? SuggestFactory(string typeName, IObjectCreationOperation creation)
+    {
+        var baseName = typeName;
+        if (baseName.EndsWith("Managed", StringComparison.Ordinal))
+        {
+            baseName = baseName.Substring(0, baseName.Length - "Managed".Length);
+        }
+        else if (baseName.EndsWith("Cng", StringComparison.Ordinal))
+        {
+            baseName = baseName.Substring(0, baseName.Length - "Cng".Length);
+        }
+
+        if (creation.Arguments.Length == 0)
+        {
+            return baseName.Length == 0 ? "the abstract base factory's Create()" : baseName + ".Create()";
+        }
+
+        if (creation.Arguments.Length != 1 || creation.Arguments[0].Parameter?.Type is not { } parameterType)
+        {
+            return null;
+        }
+
+        var keepsKeySize = baseName is "RSA" or "DSA" && parameterType.SpecialType == SpecialType.System_Int32;
+        var keepsCurve = baseName is "ECDsa" or "ECDiffieHellman" &&
+            FipsSymbolHelpers.IsCryptographyType(parameterType, "ECCurve");
+
+        return keepsKeySize || keepsCurve
+            ? baseName + ".Create(" + creation.Arguments[0].Value.Syntax + ")"
+            : null;
+    }
+}
diff --git a/src/E128.Analyzers/Security/FipsForbiddenAlgorithmNameAnalyzer.cs b/src/E128.Analyzers/Security/FipsForbiddenAlgorithmNameAnalyzer.cs
new file mode 100644
index 0000000..8b39c6f
--- /dev/null
+++ b/src/E128.Analyzers/Security/FipsForbiddenAlgorithmNameAnalyzer.cs
@@ -0,0 +1,88 @@
+using System;
+using System.Collections.Immutable;
+using Microsoft.CodeAnalysis;
+using Microsoft.CodeAnalysis.Diagnostics;
+using Microsoft.CodeAnalysis.Operations;
+
+namespace E128.Analyzers.Security;
+
+/// <summary>
+///     E128148: Reports ciphers identified by type name: Blowfish, RC4, Arcfour, CAST5, CAST6, IDEA and
+///     Skipjack. Third-party libraries such as BouncyCastle expose these ciphers. A type that E128071 reports is
+///     skipped, so each type gets one diagnostic. The name match can miss a cipher with an unusual name.
+/// </summary>
+[DiagnosticAnalyzer(LanguageNames.CSharp)]
+public sealed class FipsForbiddenAlgorithmNameAnalyzer : DiagnosticAnalyzer
+{
+    internal const string DiagnosticId = "E128148";
+
+    private static readonly DiagnosticDescriptor Rule = new(
+        DiagnosticId,
+        "Do not use a cipher excluded by policy",
+        "Type '{0}' is blocked by organizational policy because its name matches an excluded cipher (Blowfish, RC4, Arcfour, CAST5, CAST6, IDEA or Skipjack)",
+        "Security",
+        DiagnosticSeverity.Error,
+        true,
+        "Third-party libraries such as BouncyCastle expose these ciphers. The organizational policy blocks " +
+        "them. The rule matches the type name as a whole word, so it can miss a cipher with an unusual name.");
+
+    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];
+
+    public override void Initialize(AnalysisContext context)
+    {
+        if (context is null)
+        {
+            throw new ArgumentNullException(nameof(context));
+        }
+
+        context.ConfigureGeneratedCodeAnalysis(
+            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
+        context.EnableConcurrentExecution();
+
+        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
+        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
+        context.RegisterOperationAction(AnalyzeMethodReference, OperationKind.MethodReference);
+    }
+
+    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
+    {
+        var creation = (IObjectCreationOperation)context.Operation;
+
+        // E128071 covers the policy's named algorithms. This rule adds the name-only cipher matches.
+        InspectType(context, creation.Type, creation.Syntax);
+    }
+
+    /// <summary>
+    ///     Reports a static factory or one-shot helper of a cipher type, such as
+    ///     <c lang="csharp">BlowfishEngine.Create()</c> or <c lang="csharp">BlowfishEngine.HashData(...)</c>.
+    /// </summary>
+    private static void AnalyzeInvocation(OperationAnalysisContext context)
+    {
+        var invocation = (IInvocationOperation)context.Operation;
+        var method = invocation.TargetMethod;
+        if (method.IsStatic && FipsTypeClassifier.OneShotMembers.Contains(method.Name))
+        {
+            InspectType(context, method.ContainingType, invocation.Syntax);
+        }
+    }
+
+    /// <summary>Reports a method group that names a static factory or one-shot helper of a cipher type.</summary>
+    private static void AnalyzeMethodReference(OperationAnalysisContext context)
+    {
+        var reference = (IMethodReferenceOperation)context.Operation;
+        if (reference.Method.IsStatic && FipsTypeClassifier.OneShotMembers.Contains(reference.Method.Name))
+        {
+            InspectType(context, reference.Method.ContainingType, reference.Syntax);
+        }
+    }
+
+    private static void InspectType(OperationAnalysisContext context, ITypeSymbol? type, SyntaxNode syntax)
+    {
+        if (type is null || FipsTypeClassifier.GetVerdict(context.Compilation, type) != TypeVerdict.ForbiddenName)
+        {
+            return;
+        }
+
+        context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.GetLocation(), FipsSymbolHelpers.FullName(type)));
+    }
+}
diff --git a/src/E128.Analyzers/Security/FipsForbiddenTypes.cs b/src/E128.Analyzers/Security/FipsForbiddenTypes.cs
new file mode 100644
index 0000000..84b431d
--- /dev/null
+++ b/src/E128.Analyzers/Security/FipsForbiddenTypes.cs
@@ -0,0 +1,50 @@
+using System;
+using System.Collections.Immutable;
+
+namespace E128.Analyzers.Security;
+
+/// <summary>
+///     The System.Security.Cryptography type names that E128071 reports. E128071, E128148 and
+///     <see cref="FipsTypeClassifier" /> read this set, so no analyzer depends on another analyzer's members.
+/// </summary>
+internal static class FipsForbiddenTypes
+{
+    /// <summary>Types whose use the policy blocks outright.</summary>
+    public static readonly ImmutableHashSet<string> Algorithms = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "MD5",
+        "MD5CryptoServiceProvider",
+        "MD5Managed",
+        "MD5Cng",
+        "SHA1",
+        "SHA1CryptoServiceProvider",
+        "SHA1Managed",
+        "SHA1Cng",
+        "RIPEMD160",
+        "RIPEMD160Managed",
+        "DES",
+        "DESCryptoServiceProvider",
+        "RC2",
+        "RC2CryptoServiceProvider",
+        "TripleDES",
+        "TripleDESCryptoServiceProvider",
+        "TripleDESCng",
+        "HMACMD5",
+        "HMACSHA1",
+        "HMACRIPEMD160",
+        "MACTripleDES");
+
+    /// <summary>
+    ///     Types that organizational policy excludes without a claim that every use is prohibited by NIST.
+    ///     <c lang="csharp">ChaCha20Poly1305</c> is not in the approved algorithm list.
+    ///     <c lang="csharp">PasswordDeriveBytes</c> implements the obsolete PBKDF1 and defaults to SHA-1.
+    /// </summary>
+    public static readonly ImmutableHashSet<string> Excluded = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "ChaCha20Poly1305",
+        "PasswordDeriveBytes");
+
+    /// <summary>Every type E128071 reports.</summary>
+    public static readonly ImmutableHashSet<string> All =
+        Algorithms.Union(Excluded).WithComparer(StringComparer.Ordinal);
+}
diff --git a/src/E128.Analyzers/Security/FipsHmacKeyLengthAnalyzer.cs b/src/E128.Analyzers/Security/FipsHmacKeyLengthAnalyzer.cs
new file mode 100644
index 0000000..9b41221
--- /dev/null
+++ b/src/E128.Analyzers/Security/FipsHmacKeyLengthAnalyzer.cs
@@ -0,0 +1,216 @@
+using System;
+using System.Collections.Generic;
+using System.Collections.Immutable;
+using Microsoft.CodeAnalysis;
+using Microsoft.CodeAnalysis.Diagnostics;
+using Microsoft.CodeAnalysis.Operations;
+
+namespace E128.Analyzers.Security;
+
+/// <summary>
+///     E128151: Reports an HMAC key whose compile-time length is shorter than the hash output. The policy
+///     minimum is stricter than the minimum in NIST guidance.
+/// </summary>
+[DiagnosticAnalyzer(LanguageNames.CSharp)]
+public sealed class FipsHmacKeyLengthAnalyzer : DiagnosticAnalyzer
+{
+    internal const string DiagnosticId = "E128151";
+
+    private const string HmacPrefix = "HMAC";
+
+    private static readonly ImmutableHashSet<string> StaticHmacMembers = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "HashData",
+        "TryHashData",
+        "HashDataAsync");
+
+    private static readonly char[] NameSeparators = ['-', '_'];
+
+    /// <summary>
+    ///     The hash output length in bytes for each covered hash, keyed by the name without separators. A hash name
+    ///     (<c lang="csharp">SHA-256</c>, <c lang="csharp">SHA3_256</c>) and an HMAC type name without its prefix
+    ///     (<c lang="csharp">HMACSHA256</c>) share it.
+    /// </summary>
+    private static readonly Dictionary<string, int> HashOutputBytes = new(StringComparer.OrdinalIgnoreCase)
+    {
+        ["SHA256"] = 32,
+        ["SHA3256"] = 32,
+        ["SHA384"] = 48,
+        ["SHA3384"] = 48,
+        ["SHA512"] = 64,
+        ["SHA3512"] = 64,
+    };
+
+    private static readonly DiagnosticDescriptor Rule = new(
+        DiagnosticId,
+        "Use an HMAC key at least as long as the hash output",
+        "{0} key is {1} bytes; policy requires at least {2} bytes (the hash output length)",
+        "Security",
+        DiagnosticSeverity.Error,
+        true,
+        "Policy requires an HMAC key at least as long as the hash output: 32 bytes for SHA-256, 48 for " +
+        "SHA-384 and 64 for SHA-512. The rule reports only keys whose length it can determine at compile time.");
+
+    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];
+
+    public override void Initialize(AnalysisContext context)
+    {
+        if (context is null)
+        {
+            throw new ArgumentNullException(nameof(context));
+        }
+
+        context.ConfigureGeneratedCodeAnalysis(
+            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
+        context.EnableConcurrentExecution();
+
+        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
+        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
+        context.RegisterOperationAction(
+            AnalyzeAssignment,
+            OperationKind.SimpleAssignment,
+            OperationKind.CoalesceAssignment,
+            OperationKind.DeconstructionAssignment);
+    }
+
+    /// <summary>Returns the hash output length in bytes for an HMAC type, or 0 when the type is not covered.</summary>
+    private static int MinimumKeyBytes(ITypeSymbol? type) =>
+        FipsSymbolHelpers.IsInCryptographyNamespace(type) &&
+        type is INamedTypeSymbol { ContainingType: null } named &&
+        named.Name.StartsWith(HmacPrefix, StringComparison.Ordinal)
+            ? MinimumKeyBytes(named.Name.Substring(HmacPrefix.Length))
+            : 0;
+
+    /// <summary>Returns the hash output length in bytes for a hash algorithm name, or 0 when it is not covered.</summary>
+    private static int MinimumKeyBytes(string hashName)
+    {
+        var normalized = hashName.IndexOfAny(NameSeparators) < 0
+            ? hashName
+            : hashName.Replace("-", string.Empty).Replace("_", string.Empty);
+
+        return HashOutputBytes.TryGetValue(normalized, out var bytes) ? bytes : 0;
+    }
+
+    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
+    {
+        var creation = (IObjectCreationOperation)context.Operation;
+        var minimum = MinimumKeyBytes(creation.Type);
+        if (minimum != 0 && creation.Type is { } type)
+        {
+            CheckKey(context, type.Name, minimum, creation.Arguments, creation.Syntax);
+        }
+    }
+
+    private static void AnalyzeInvocation(OperationAnalysisContext context)
+    {
+        var invocation = (IInvocationOperation)context.Operation;
+        var method = invocation.TargetMethod;
+        if (!method.IsStatic)
+        {
+            return;
+        }
+
+        var syntax = invocation.Syntax;
+
+        if (StaticHmacMembers.Contains(method.Name))
+        {
+            if (method.ContainingType is { } owner)
+            {
+                var minimum = MinimumKeyBytes(owner);
+                if (minimum != 0)
+                {
+                    CheckKey(context, owner.Name, minimum, invocation.Arguments, syntax);
+                }
+            }
+
+            return;
+        }
+
+        // IncrementalHash.CreateHMAC(HashAlgorithmName, key), CryptographicOperations.HmacData and similar.
+        if (method.Name.IndexOf("HMAC", StringComparison.OrdinalIgnoreCase) < 0 ||
+            !FipsSymbolHelpers.IsInCryptographyNamespace(method.ContainingType))
+        {
+            return;
+        }
+
+        foreach (var argument in FipsSymbolHelpers.HashNameArguments(invocation.Arguments))
+        {
+            if (!FipsSymbolHelpers.TryGetHashAlgorithmName(argument.Value, out var hashName))
+            {
+                continue;
+            }
+
+            var minimum = MinimumKeyBytes(hashName);
+            if (minimum != 0)
+            {
+                CheckKey(context, "HMAC-" + hashName, minimum, invocation.Arguments, syntax);
+            }
+
+            return;
+        }
+    }
+
+    private static void AnalyzeAssignment(OperationAnalysisContext context)
+    {
+        if (!FipsSymbolHelpers.MayAssignMember(context.Operation, "Key"))
+        {
+            return;
+        }
+
+        FipsSymbolHelpers.ForEachAssignment(
+            context.Operation,
+            context,
+            static (state, target, value, syntax) => CheckKeyAssignment(state, target, value, syntax));
+    }
+
+    private static void CheckKeyAssignment(OperationAnalysisContext context, IOperation target, IOperation value, SyntaxNode syntax)
+    {
+        if (!FipsSymbolHelpers.IsCandidateAssignmentTarget(target, "Key") ||
+            target is not IPropertyReferenceOperation property)
+        {
+            return;
+        }
+
+        var instanceType = property.Instance?.Type;
+        var minimum = MinimumKeyBytes(instanceType);
+
+        // A base-typed local (HMAC h = new HMACSHA256()) takes the concrete type from its initializer.
+        if (minimum == 0 &&
+            FipsSymbolHelpers.DerivesFromCryptographyType(instanceType, "HMAC") &&
+            FipsSymbolHelpers.Unwrap(FipsSymbolHelpers.ResolveInitializer(property.Instance)) is IObjectCreationOperation created)
+        {
+            instanceType = created.Type;
+            minimum = MinimumKeyBytes(instanceType);
+        }
+
+        if (minimum != 0 &&
+            instanceType is { } type &&
+            FipsSymbolHelpers.TryGetConstantByteLength(value, out var keyBytes) &&
+            keyBytes < minimum)
+        {
+            Report(context, syntax, type.Name, keyBytes, minimum);
+        }
+    }
+
+    private static void CheckKey(
+        OperationAnalysisContext context,
+        string label,
+        int minimum,
+        ImmutableArray<IArgumentOperation> arguments,
+        SyntaxNode syntax)
+    {
+        var argument = FipsSymbolHelpers.ArgumentNamed(arguments, "key");
+        if (argument is null)
+        {
+            return;
+        }
+
+        if (FipsSymbolHelpers.TryGetConstantByteLength(argument.Value, out var keyBytes) && keyBytes < minimum)
+        {
+            Report(context, syntax, label, keyBytes, minimum);
+        }
+    }
+
+    private static void Report(OperationAnalysisContext context, SyntaxNode syntax, string label, int keyBytes, int minimum) =>
+        context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.GetLocation(), label, keyBytes, minimum));
+}
diff --git a/src/E128.Analyzers/Security/FipsPbkdfIterationAnalyzer.cs b/src/E128.Analyzers/Security/FipsPbkdfIterationAnalyzer.cs
new file mode 100644
index 0000000..aa979f2
--- /dev/null
+++ b/src/E128.Analyzers/Security/FipsPbkdfIterationAnalyzer.cs
@@ -0,0 +1,318 @@
+using System;
+using System.Collections.Immutable;
+using System.Globalization;
+using Microsoft.CodeAnalysis;
+using Microsoft.CodeAnalysis.Diagnostics;
+using Microsoft.CodeAnalysis.Operations;
+
+namespace E128.Analyzers.Security;
+
+/// <summary>
+///     E128145: Reports PBKDF2 calls below the organizational minimum: fewer than 600,000 iterations, an
+///     overload that defaults to 1,000 iterations, or SHA-1 as the PRF.
+/// </summary>
+[DiagnosticAnalyzer(LanguageNames.CSharp)]
+public sealed class FipsPbkdfIterationAnalyzer : DiagnosticAnalyzer
+{
+    internal const string DiagnosticId = "E128145";
+
+    private const int MinIterations = 600_000;
+
+    private static readonly string MinIterationsText = MinIterations.ToString(CultureInfo.InvariantCulture);
+    private const string KeyDerivationNamespace = "Microsoft.AspNetCore.Cryptography.KeyDerivation";
+
+    private static readonly ImmutableHashSet<string> WeakHashNames = ImmutableHashSet.Create(
+        StringComparer.OrdinalIgnoreCase,
+        "SHA1",
+        "SHA-1",
+        "MD5",
+        "SHA",
+        "RIPEMD",
+        "RIPEMD160",
+        "RIPEMD-160");
+
+    private static readonly DiagnosticDescriptor Rule = new(
+        DiagnosticId,
+        "Use a strong PBKDF2 configuration",
+        "{0}",
+        "Security",
+        DiagnosticSeverity.Error,
+        true,
+        "PBKDF2 must use at least 600,000 iterations, and policy prefers a SHA-2 PRF. A value that is not a " +
+        "compile-time constant is reported as unverifiable. An IterationCount assignment in an object " +
+        "initializer or as the first statement that uses the object replaces the default-count report.");
+
+    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];
+
+    public override void Initialize(AnalysisContext context)
+    {
+        if (context is null)
+        {
+            throw new ArgumentNullException(nameof(context));
+        }
+
+        // Key derivation settings are reported in generated code too, because a generated PBKDF2 call is a real
+        // policy defect. FIPS0005 parity requires this opt-in.
+        context.ConfigureGeneratedCodeAnalysis(
+            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
+        context.EnableConcurrentExecution();
+
+        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
+        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
+        context.RegisterOperationAction(
+            AnalyzeAssignment,
+            OperationKind.SimpleAssignment,
+            OperationKind.CoalesceAssignment,
+            OperationKind.CompoundAssignment,
+            OperationKind.DeconstructionAssignment);
+    }
+
+    private static void AnalyzeAssignment(OperationAnalysisContext context)
+    {
+        if (!FipsSymbolHelpers.MayAssignMember(context.Operation, "IterationCount"))
+        {
+            return;
+        }
+
+        FipsSymbolHelpers.ForEachAssignment(
+            context.Operation,
+            context,
+            static (state, target, value, syntax) => CheckIterationCountAssignment(state, target, value, syntax));
+    }
+
+    private static void CheckIterationCountAssignment(
+        OperationAnalysisContext context,
+        IOperation target,
+        IOperation value,
+        SyntaxNode syntax)
+    {
+        if (FipsSymbolHelpers.IsCandidateAssignmentTarget(target, "IterationCount") &&
+            target is IPropertyReferenceOperation property &&
+            FipsSymbolHelpers.IsCryptographyType(property.Property.ContainingType, "Rfc2898DeriveBytes"))
+        {
+            CheckIterations(context, value, syntax.GetLocation());
+        }
+    }
+
+    /// <summary>
+    ///     Returns true when the object initializer, or the first statement that uses the local receiving the new
+    ///     object, assigns <c lang="csharp">IterationCount</c>. That assignment reports for itself.
+    /// </summary>
+    private static bool IsIterationCountAssigned(IObjectCreationOperation creation)
+    {
+        if (FipsSymbolHelpers.InitializerAssigns(creation, "IterationCount"))
+        {
+            return true;
+        }
+
+        var use = FipsSymbolHelpers.FindFirstLocalUse(creation, out var block);
+
+        return use is not null && FipsSymbolHelpers.IsUnconditionalPropertyAssignment(use, "IterationCount", block);
+    }
+
+    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
+    {
+        var creation = (IObjectCreationOperation)context.Operation;
+        if (FipsSymbolHelpers.IsCryptographyType(creation.Type, "PbeParameters"))
+        {
+            CheckPbeParameters(context, creation);
+            return;
+        }
+
+        if (!FipsSymbolHelpers.DerivesFromCryptographyType(creation.Type, "Rfc2898DeriveBytes"))
+        {
+            return;
+        }
+
+        var location = creation.Syntax.GetLocation();
+        var iterations = FipsSymbolHelpers.ArgumentNamed(creation.Arguments, "iterations");
+        var hash = FipsSymbolHelpers.ArgumentNamed(creation.Arguments, "hashAlgorithm");
+
+        // A subclass constructor picks its own parameters, so only a named iterations or hashAlgorithm argument
+        // is checked. The overload defaults below apply to Rfc2898DeriveBytes itself.
+        if (!FipsSymbolHelpers.IsCryptographyType(creation.Type, "Rfc2898DeriveBytes"))
+        {
+            if (iterations is not null)
+            {
+                CheckIterations(context, iterations.Value, location);
+            }
+
+            if (hash is not null)
+            {
+                CheckHashAlgorithm(context, hash.Value, location);
+            }
+
+            return;
+        }
+
+        CheckRfc2898DeriveBytesDefaults(context, creation, iterations, hash);
+    }
+
+    /// <summary>
+    ///     Checks the argument defaults of <c lang="csharp">new Rfc2898DeriveBytes(...)</c> itself. The overloads
+    ///     differ, so an absent iteration count and an absent hash are each checked on their own.
+    /// </summary>
+    private static void CheckRfc2898DeriveBytesDefaults(
+        OperationAnalysisContext context,
+        IObjectCreationOperation creation,
+        IArgumentOperation? iterations,
+        IArgumentOperation? hash)
+    {
+        var location = creation.Syntax.GetLocation();
+
+        if (iterations is null)
+        {
+            if (!IsIterationCountAssigned(creation))
+            {
+                Report(
+                    context,
+                    location,
+                    $"Rfc2898DeriveBytes overload uses the default of 1000 iterations; use an explicit count of at least {MinIterationsText}");
+            }
+        }
+        else
+        {
+            CheckIterations(context, iterations.Value, location);
+        }
+
+        if (hash is null)
+        {
+            Report(
+                context,
+                location,
+                "Rfc2898DeriveBytes overload uses SHA1 as the PRF by default; pass HashAlgorithmName.SHA256 or stronger");
+        }
+        else
+        {
+            CheckHashAlgorithm(context, hash.Value, location);
+        }
+    }
+
+    /// <summary>
+    ///     Checks the PRF hash and the iteration count of <c lang="csharp">new PbeParameters(algorithm, hash, iterations)</c>,
+    ///     the password-based encryption settings of a PKCS#8 or PKCS#12 export.
+    /// </summary>
+    private static void CheckPbeParameters(OperationAnalysisContext context, IObjectCreationOperation creation)
+    {
+        var location = creation.Syntax.GetLocation();
+
+        if (FipsSymbolHelpers.ArgumentNamed(creation.Arguments, "hashAlgorithm") is { } hash)
+        {
+            CheckHashAlgorithm(context, hash.Value, location);
+        }
+
+        if (FipsSymbolHelpers.ArgumentNamed(creation.Arguments, "iterationCount") is { } iterations)
+        {
+            CheckIterations(context, iterations.Value, location);
+        }
+    }
+
+    private static void AnalyzeInvocation(OperationAnalysisContext context)
+    {
+        var invocation = (IInvocationOperation)context.Operation;
+        var method = invocation.TargetMethod;
+        if (!method.IsStatic || !string.Equals(method.Name, "Pbkdf2", StringComparison.Ordinal))
+        {
+            return;
+        }
+
+        var location = invocation.Syntax.GetLocation();
+
+        if (FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "Rfc2898DeriveBytes"))
+        {
+            var iterations = FipsSymbolHelpers.ArgumentNamed(invocation.Arguments, "iterations");
+            if (iterations is not null)
+            {
+                CheckIterations(context, iterations.Value, location);
+            }
+
+            var hash = FipsSymbolHelpers.ArgumentNamed(invocation.Arguments, "hashAlgorithm");
+            if (hash is not null)
+            {
+                CheckHashAlgorithm(context, hash.Value, location);
+            }
+        }
+        else if (FipsSymbolHelpers.IsType(method.ContainingType, "KeyDerivation", KeyDerivationNamespace))
+        {
+            var iterations = FipsSymbolHelpers.ArgumentNamed(invocation.Arguments, "iterationCount");
+            if (iterations is not null)
+            {
+                CheckIterations(context, iterations.Value, location);
+            }
+
+            var prf = FipsSymbolHelpers.ArgumentNamed(invocation.Arguments, "prf");
+            if (prf is not null)
+            {
+                CheckPrf(context, prf.Value, location);
+            }
+        }
+    }
+
+    private static void CheckIterations(OperationAnalysisContext context, IOperation value, Location location)
+    {
+        if (FipsSymbolHelpers.TryGetResolvedConstant<int>(value, out var iterations))
+        {
+            if (iterations < MinIterations)
+            {
+                Report(context, location, $"PBKDF2 iteration count {iterations.ToString(CultureInfo.InvariantCulture)} is below the minimum of {MinIterationsText}");
+            }
+        }
+        else
+        {
+            Report(
+                context,
+                location,
+                $"PBKDF2 iteration count cannot be verified at compile time; ensure it is at least {MinIterationsText}");
+        }
+    }
+
+    private static void CheckHashAlgorithm(OperationAnalysisContext context, IOperation value, Location location)
+    {
+        if (!FipsSymbolHelpers.TryGetHashAlgorithmName(value, out var name))
+        {
+            Report(
+                context,
+                location,
+                "PBKDF2 hash algorithm cannot be verified at compile time; ensure it is SHA-256 or stronger");
+        }
+        else if (WeakHashNames.Contains(name))
+        {
+            Report(context, location, $"PBKDF2 uses {name} as the PRF; use SHA-256 or stronger");
+        }
+    }
+
+    private static void CheckPrf(OperationAnalysisContext context, IOperation value, Location location)
+    {
+        var operation = FipsSymbolHelpers.Unwrap(value);
+
+        // KeyDerivationPrf.HMACSHA1 is 0, HMACSHA256 is 1 and HMACSHA512 is 2.
+        if (operation is IFieldReferenceOperation { Field: { } field } &&
+            FipsSymbolHelpers.IsType(field.ContainingType, "KeyDerivationPrf", KeyDerivationNamespace))
+        {
+            if (string.Equals(field.Name, "HMACSHA1", StringComparison.Ordinal))
+            {
+                Report(context, location, $"PBKDF2 uses {field.Name} as the PRF; use SHA-256 or stronger");
+            }
+
+            return;
+        }
+
+        if (operation is not null && operation.ConstantValue.HasValue)
+        {
+            if (operation.ConstantValue.Value is int and 0)
+            {
+                Report(context, location, "PBKDF2 uses HMACSHA1 as the PRF; use SHA-256 or stronger");
+            }
+
+            return;
+        }
+
+        Report(
+            context,
+            location,
+            "PBKDF2 PRF cannot be verified at compile time; ensure it is HMACSHA256 or stronger");
+    }
+
+    private static void Report(OperationAnalysisContext context, Location location, string message) =>
+        context.ReportDiagnostic(Diagnostic.Create(Rule, location, message));
+}
diff --git a/src/E128.Analyzers/Security/FipsRsaKeySizeAnalyzer.cs b/src/E128.Analyzers/Security/FipsRsaKeySizeAnalyzer.cs
new file mode 100644
index 0000000..e02656c
--- /dev/null
+++ b/src/E128.Analyzers/Security/FipsRsaKeySizeAnalyzer.cs
@@ -0,0 +1,589 @@
+using System;
+using System.Collections.Immutable;
+using System.Diagnostics.CodeAnalysis;
+using System.Globalization;
+using Microsoft.CodeAnalysis;
+using Microsoft.CodeAnalysis.Diagnostics;
+using Microsoft.CodeAnalysis.Operations;
+
+namespace E128.Analyzers.Security;
+
+/// <summary>
+///     E128143: Reports RSA keys under 3072 bits and elliptic curve keys under 384 bits. These limits are
+///     stricter organizational policy than NIST guidance. An RSA key size that cannot be verified at compile
+///     time is also reported.
+/// </summary>
+[DiagnosticAnalyzer(LanguageNames.CSharp)]
+public sealed class FipsRsaKeySizeAnalyzer : DiagnosticAnalyzer
+{
+    internal const string DiagnosticId = "E128143";
+
+    private const int MinRsaBits = 3072;
+    private const int MinEllipticCurveBits = 384;
+
+    private static readonly string MinRsaBitsText = MinRsaBits.ToString(CultureInfo.InvariantCulture);
+
+    private static readonly string NamedDefaultKeySizeMessage =
+        $"RSA created by name uses the platform default key size, which cannot be verified; create the key with an explicit key size of at least {MinRsaBitsText} bits";
+
+    /// <summary>Curve names and object identifiers for P-384 and P-521. The comparison ignores case.</summary>
+    private static readonly ImmutableHashSet<string> AllowedCurves = ImmutableHashSet.Create(
+        StringComparer.OrdinalIgnoreCase,
+        "nistP384",
+        "nistP521",
+        "secp384r1",
+        "secp521r1",
+        "P-384",
+        "P-521",
+        "ECDSA_P384",
+        "ECDSA_P521",
+        "ECDH_P384",
+        "ECDH_P521",
+        "1.3.132.0.34",
+        "1.3.132.0.35");
+
+    private static readonly ImmutableHashSet<string> ImportMethods = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "ImportParameters",
+        "ImportSubjectPublicKeyInfo",
+        "ImportRSAPublicKey",
+        "ImportRSAPrivateKey",
+        "ImportPkcs8PrivateKey",
+        "ImportEncryptedPkcs8PrivateKey",
+        "ImportFromPem",
+        "ImportFromEncryptedPem",
+        "ImportCspBlob",
+        "FromXmlString");
+
+    private static readonly ImmutableHashSet<string> ImportedKeyTypes = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "RSAParameters",
+        "CngKey",
+        "SafeEvpPKeyHandle");
+
+    private static readonly ImmutableHashSet<string> EcImportMethods = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "ImportSubjectPublicKeyInfo",
+        "ImportECPrivateKey",
+        "ImportPkcs8PrivateKey",
+        "ImportEncryptedPkcs8PrivateKey",
+        "ImportFromPem",
+        "ImportFromEncryptedPem");
+
+    private static readonly ImmutableHashSet<string> CertificateKeyMethods = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "GetRSAPublicKey",
+        "GetRSAPrivateKey",
+        "GetECDsaPublicKey",
+        "GetECDsaPrivateKey",
+        "GetECDiffieHellmanPublicKey",
+        "GetECDiffieHellmanPrivateKey");
+
+    private static readonly DiagnosticDescriptor Rule = new(
+        DiagnosticId,
+        "Use an RSA key of at least 3072 bits or an approved curve",
+        "{0}",
+        "Security",
+        DiagnosticSeverity.Error,
+        true,
+        "Policy requires RSA keys of at least 3072 bits and elliptic curves of at least 384 bits (P-384 or " +
+        "P-521). An RSA key size that cannot be determined at compile time is reported, including a key " +
+        "imported from parameters, a handle, a blob, PEM text or a certificate.");
+
+    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];
+
+    public override void Initialize(AnalysisContext context)
+    {
+        if (context is null)
+        {
+            throw new ArgumentNullException(nameof(context));
+        }
+
+        // Cryptographic key creation is reported in generated code too, because a generated key setup is a real
+        // policy defect. FIPS0003 parity requires this opt-in.
+        context.ConfigureGeneratedCodeAnalysis(
+            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
+        context.EnableConcurrentExecution();
+
+        context.RegisterCompilationStartAction(start =>
+        {
+            // .NET Framework's mscorlib defines object. There RSA.Create() returns RSACryptoServiceProvider.
+            var isFramework =
+                string.Equals(
+                    start.Compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly?.Name,
+                    "mscorlib",
+                    StringComparison.Ordinal);
+
+            start.RegisterOperationAction(operation => AnalyzeInvocation(operation, isFramework), OperationKind.Invocation);
+            start.RegisterOperationAction(operation => AnalyzeObjectCreation(operation, isFramework), OperationKind.ObjectCreation);
+            start.RegisterOperationAction(AnalyzeMethodReference, OperationKind.MethodReference);
+            start.RegisterOperationAction(
+                AnalyzeAssignment,
+                OperationKind.SimpleAssignment,
+                OperationKind.CoalesceAssignment,
+                OperationKind.CompoundAssignment,
+                OperationKind.DeconstructionAssignment);
+        });
+    }
+
+    private static Family GetFamily(ITypeSymbol? type)
+    {
+        var isRsa = FipsSymbolHelpers.DerivesFromCryptographyType(type, "RSA");
+        var isEllipticCurve = FipsSymbolHelpers.DerivesFromCryptographyType(type, "ECDsa") ||
+            FipsSymbolHelpers.DerivesFromCryptographyType(type, "ECDiffieHellman");
+
+        return isRsa ? Family.Rsa : isEllipticCurve ? Family.EllipticCurve : Family.None;
+    }
+
+    /// <summary>
+    ///     Reports a certificate key, whose size or curve comes from the certificate and cannot be verified at
+    ///     compile time. Returns true when the call is one.
+    /// </summary>
+    private static bool ReportCertificateKey(OperationAnalysisContext context, IInvocationOperation invocation)
+    {
+        var method = invocation.TargetMethod;
+        if (!CertificateKeyMethods.Contains(method.Name) ||
+            !FipsSymbolHelpers.IsInCryptographyNamespaceTree(method.ContainingType))
+        {
+            return false;
+        }
+
+        var message = GetFamily(method.ReturnType) switch
+        {
+            Family.Rsa => ImportedKeySizeMessage(method.ReturnType.Name),
+            Family.EllipticCurve => ImportedCurveMessage(method.ReturnType.Name),
+            Family.None => null,
+            _ => null,
+        };
+
+        if (message is null)
+        {
+            return false;
+        }
+
+        Report(context, invocation.Syntax, message);
+
+        return true;
+    }
+
+    /// <summary>
+    ///     Checks the static factory calls <c lang="csharp">RSA.Create</c>, <c lang="csharp">ECDsa.Create</c> and
+    ///     <c lang="csharp">ECDiffieHellman.Create</c>. Returns true when the call is one of them.
+    /// </summary>
+    private static bool AnalyzeStaticCreate(OperationAnalysisContext context, IInvocationOperation invocation, bool isFramework)
+    {
+        var method = invocation.TargetMethod;
+        if (!method.IsStatic ||
+            !string.Equals(method.Name, "Create", StringComparison.Ordinal) ||
+            !FipsSymbolHelpers.IsInCryptographyNamespace(method.ContainingType))
+        {
+            return false;
+        }
+
+        var type = method.ContainingType;
+        if (FipsSymbolHelpers.IsCryptographyType(type, "RSA"))
+        {
+            CheckRsaCreation(context, invocation, invocation.Arguments, type.Name, invocation.Syntax, isFramework);
+        }
+        else if (FipsSymbolHelpers.IsCryptographyType(type, "ECDsa") ||
+                 FipsSymbolHelpers.IsCryptographyType(type, "ECDiffieHellman"))
+        {
+            CheckCurveArgument(context, invocation.Arguments, invocation.Syntax);
+        }
+
+        return true;
+    }
+
+    private static void AnalyzeInvocation(OperationAnalysisContext context, bool isFramework)
+    {
+        var invocation = (IInvocationOperation)context.Operation;
+        var method = invocation.TargetMethod;
+        var syntax = invocation.Syntax;
+
+        if (ReportCertificateKey(context, invocation))
+        {
+            return;
+        }
+
+        if (IsRsaCreatedByName(invocation))
+        {
+            // The returned object is not an RSA type here, so a later KeySize assignment is not tracked.
+            if (!IsDefaultKeyResolved(invocation, allowKeySize: false))
+            {
+                Report(context, syntax, NamedDefaultKeySizeMessage);
+            }
+
+            return;
+        }
+
+        if (AnalyzeStaticCreate(context, invocation, isFramework))
+        {
+            return;
+        }
+
+        if (method.IsStatic)
+        {
+            return;
+        }
+
+        if (invocation.Instance?.Type is not { } instanceType)
+        {
+            return;
+        }
+
+        var family = GetFamily(instanceType);
+
+        // ECDsa.GenerateKey(ECCurve) and ECDiffieHellman.GenerateKey(ECCurve).
+        if (family == Family.EllipticCurve && string.Equals(method.Name, "GenerateKey", StringComparison.Ordinal))
+        {
+            CheckCurveArgument(context, invocation.Arguments, syntax);
+        }
+        else if (family == Family.EllipticCurve && EcImportMethods.Contains(method.Name))
+        {
+            Report(context, syntax, ImportedCurveMessage(instanceType.Name));
+        }
+        else if (family == Family.Rsa && ImportMethods.Contains(method.Name))
+        {
+            Report(context, syntax, ImportedKeySizeMessage(instanceType.Name));
+        }
+    }
+
+    /// <summary>
+    ///     Reports an <c lang="csharp">RSA.Create</c> method group, such as <c lang="csharp">Func&lt;int, RSA&gt; create = RSA.Create</c>. The
+    ///     key size arrives when the delegate runs, so it cannot be verified here.
+    /// </summary>
+    private static void AnalyzeMethodReference(OperationAnalysisContext context)
+    {
+        var reference = (IMethodReferenceOperation)context.Operation;
+        var method = reference.Method;
+        if (method.IsStatic &&
+            string.Equals(method.Name, "Create", StringComparison.Ordinal) &&
+            FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "RSA"))
+        {
+            Report(
+                context,
+                reference.Syntax,
+                $"RSA key size cannot be verified at compile time because the {method.ContainingType.Name}.Create method group is invoked later; ensure it is at least {MinRsaBitsText} bits");
+        }
+    }
+
+    private static void AnalyzeObjectCreation(OperationAnalysisContext context, bool isFramework)
+    {
+        var creation = (IObjectCreationOperation)context.Operation;
+        if (creation.Type is not { } createdType)
+        {
+            return;
+        }
+
+        var family = GetFamily(createdType);
+        if (family == Family.None)
+        {
+            return;
+        }
+
+        var syntax = creation.Syntax;
+        if (family == Family.EllipticCurve && CheckCurveArgument(context, creation.Arguments, syntax))
+        {
+            return;
+        }
+
+        if (family == Family.Rsa)
+        {
+            CheckRsaCreation(context, creation, creation.Arguments, createdType.Name, syntax, isFramework);
+        }
+        else if (TryGetKeySizeArgument(creation.Arguments, out var keySize))
+        {
+            CheckKeySize(context, family, createdType.Name, keySize, syntax);
+        }
+    }
+
+    private static void AnalyzeAssignment(OperationAnalysisContext context)
+    {
+        if (!FipsSymbolHelpers.MayAssignMember(context.Operation, "KeySize") &&
+            !FipsSymbolHelpers.MayAssignMember(context.Operation, "Curve"))
+        {
+            return;
+        }
+
+        FipsSymbolHelpers.ForEachAssignment(
+            context.Operation,
+            context,
+            static (state, target, value, syntax) => CheckAssignment(state, target, value, syntax));
+    }
+
+    private static void CheckAssignment(OperationAnalysisContext context, IOperation target, IOperation value, SyntaxNode syntax)
+    {
+        // ECParameters.Curve is a field, so a parameters struct can carry a weak curve.
+        if (target is IFieldReferenceOperation { Field.Name: "Curve" } curveField &&
+            FipsSymbolHelpers.IsCryptographyType(curveField.Field.ContainingType, "ECParameters"))
+        {
+            CheckCurveValue(context, value, syntax);
+
+            return;
+        }
+
+        if (target is not IPropertyReferenceOperation { Property.Name: "KeySize" } property ||
+            !FipsSymbolHelpers.IsInCryptographyNamespace(property.Property.ContainingType))
+        {
+            return;
+        }
+
+        if (property.Instance?.Type is not { } instanceType)
+        {
+            return;
+        }
+
+        var family = GetFamily(instanceType);
+        if (family == Family.None)
+        {
+            return;
+        }
+
+        CheckKeySize(context, family, instanceType.Name, value, syntax);
+    }
+
+    /// <summary>The first int parameter is the key size, such as <c lang="csharp">RSACryptoServiceProvider(int, CspParameters)</c>.</summary>
+    private static bool TryGetKeySizeArgument(
+        ImmutableArray<IArgumentOperation> arguments,
+        [NotNullWhen(true)] out IOperation? keySize)
+    {
+        foreach (var argument in arguments)
+        {
+            if (argument.Parameter?.Type.SpecialType == SpecialType.System_Int32)
+            {
+                keySize = argument.Value;
+                return true;
+            }
+        }
+
+        keySize = null;
+        return false;
+    }
+
+    /// <summary>
+    ///     Checks an RSA factory call or constructor: a key size argument is read, an imported key is reported as
+    ///     unverifiable, and any other overload creates a key of the platform default size.
+    /// </summary>
+    private static void CheckRsaCreation(
+        OperationAnalysisContext context,
+        IOperation creation,
+        ImmutableArray<IArgumentOperation> arguments,
+        string typeName,
+        SyntaxNode syntax,
+        bool isFramework)
+    {
+        if (TryGetKeySizeArgument(arguments, out var keySize))
+        {
+            CheckKeySize(context, Family.Rsa, typeName, keySize, syntax);
+            return;
+        }
+
+        foreach (var argument in arguments)
+        {
+            var type = argument.Parameter?.Type;
+            if (type is null)
+            {
+                continue;
+            }
+
+            if (type.SpecialType == SpecialType.System_IntPtr ||
+                (FipsSymbolHelpers.IsInCryptographyNamespace(type) && ImportedKeyTypes.Contains(type.Name)))
+            {
+                Report(context, syntax, ImportedKeySizeMessage(typeName));
+                return;
+            }
+        }
+
+        // Assigning KeySize does not reliably resize an RSACryptoServiceProvider key, so only an import, which
+        // reports for itself, replaces its default report. On .NET Framework RSA.Create() returns that type.
+        var isCryptoServiceProvider = FipsSymbolHelpers.DerivesFromCryptographyType(creation.Type, "RSACryptoServiceProvider");
+        var isFrameworkFactory = isFramework && creation is IInvocationOperation;
+        if (IsDefaultKeyResolved(creation, allowKeySize: !isCryptoServiceProvider && !isFrameworkFactory))
+        {
+            return;
+        }
+
+        var message = isCryptoServiceProvider
+            ? $"{typeName} created without an explicit key size uses the platform default, which cannot be verified, and assigning KeySize afterward is not a reliable way to set it; use the constructor that takes a key size of at least {MinRsaBitsText} bits"
+            : isFrameworkFactory
+                ? $"{typeName} created without an explicit key size uses the platform default, which cannot be verified, and on .NET Framework assigning KeySize afterward is not a reliable way to set it; use an overload that takes a key size of at least {MinRsaBitsText} bits"
+                : $"{typeName} created without an explicit key size uses the platform default, which cannot be verified; use an explicit key size of at least {MinRsaBitsText} bits or set KeySize before use";
+        Report(context, syntax, message);
+    }
+
+    /// <summary>
+    ///     Returns true for <c lang="csharp">CryptoConfig.CreateFromName</c>, <c lang="csharp">AsymmetricAlgorithm.Create</c> and
+    ///     <c lang="csharp">RSA.Create</c> with a constant name that creates an RSA key.
+    /// </summary>
+    private static bool IsRsaCreatedByName(IInvocationOperation invocation)
+    {
+        var method = invocation.TargetMethod;
+        if (!method.IsStatic ||
+            !((string.Equals(method.Name, "CreateFromName", StringComparison.Ordinal) &&
+               FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "CryptoConfig")) ||
+              (string.Equals(method.Name, "Create", StringComparison.Ordinal) &&
+               (FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "AsymmetricAlgorithm") ||
+                FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "RSA")))) ||
+            method.Parameters.Length == 0 ||
+            method.Parameters[0].Type.SpecialType != SpecialType.System_String ||
+            !FipsSymbolHelpers.TryGetConstant<string>(FipsSymbolHelpers.ArgumentAt(invocation.Arguments, 0)?.Value, out var name))
+        {
+            return false;
+        }
+
+        name = name.Trim();
+
+        return string.Equals(name, "RSA", StringComparison.OrdinalIgnoreCase) ||
+            string.Equals(name, FipsSymbolHelpers.Cryptography + ".RSA", StringComparison.OrdinalIgnoreCase) ||
+            string.Equals(name, FipsSymbolHelpers.Cryptography + ".AsymmetricAlgorithm", StringComparison.OrdinalIgnoreCase) ||
+            string.Equals(name, FipsSymbolHelpers.Cryptography + ".RSACryptoServiceProvider", StringComparison.OrdinalIgnoreCase);
+    }
+
+    private static string ImportedKeySizeMessage(string typeName) =>
+        $"{typeName} key size cannot be verified because the key is imported; policy requires at least {MinRsaBitsText} bits";
+
+    private static string ImportedCurveMessage(string typeName) =>
+        $"{typeName} curve cannot be verified because the key is imported; policy requires P-384 or P-521";
+
+    /// <summary>
+    ///     Returns true when the object initializer, or the first statement that uses the local receiving the new
+    ///     key, sets <c lang="csharp">KeySize</c> or imports a key. Each of those reports for itself.
+    /// </summary>
+    private static bool IsDefaultKeyResolved(IOperation creation, bool allowKeySize)
+    {
+        if (allowKeySize && creation is IObjectCreationOperation objectCreation &&
+            FipsSymbolHelpers.InitializerAssigns(objectCreation, "KeySize"))
+        {
+            return true;
+        }
+
+        var use = FipsSymbolHelpers.FindFirstLocalUse(creation, out var block);
+
+        // An upcast local (AsymmetricAlgorithm) does not reach the RSA KeySize or import checks, so it stays unresolved.
+        return use is not null &&
+            GetFamily(use.Type) == Family.Rsa &&
+            ((allowKeySize && FipsSymbolHelpers.IsUnconditionalPropertyAssignment(use, "KeySize", block)) ||
+             (use.Parent is IInvocationOperation invocation &&
+              invocation.Instance == use &&
+              ImportMethods.Contains(invocation.TargetMethod.Name) &&
+              invocation.Parent is IExpressionStatementOperation { Parent: { } parent } &&
+              parent == block));
+    }
+
+    private static void CheckKeySize(
+        OperationAnalysisContext context,
+        Family family,
+        string typeName,
+        IOperation value,
+        SyntaxNode syntax)
+    {
+        var minimum = family == Family.EllipticCurve ? MinEllipticCurveBits : MinRsaBits;
+
+        if (FipsSymbolHelpers.TryGetResolvedConstant<int>(value, out var bits))
+        {
+            if (bits < minimum)
+            {
+                Report(
+                    context,
+                    syntax,
+                    $"{typeName} key size {bits.ToString(CultureInfo.InvariantCulture)} is below the policy minimum of {minimum.ToString(CultureInfo.InvariantCulture)} bits");
+            }
+        }
+        else
+        {
+            Report(context, syntax, $"{typeName} key size cannot be verified at compile time; ensure it is at least {minimum.ToString(CultureInfo.InvariantCulture)} bits");
+        }
+    }
+
+    /// <summary>
+    ///     Checks the first <c lang="csharp">ECCurve</c> argument. Returns true when the call has one, so a constructor does not
+    ///     also look for a key size.
+    /// </summary>
+    private static bool CheckCurveArgument(
+        OperationAnalysisContext context,
+        ImmutableArray<IArgumentOperation> arguments,
+        SyntaxNode syntax)
+    {
+        foreach (var argument in arguments)
+        {
+            if (!FipsSymbolHelpers.IsCryptographyType(argument.Parameter?.Type, "ECCurve"))
+            {
+                continue;
+            }
+
+            CheckCurveValue(context, argument.Value, syntax);
+
+            return true;
+        }
+
+        return false;
+    }
+
+    private static void CheckCurveValue(OperationAnalysisContext context, IOperation value, SyntaxNode syntax)
+    {
+        if (TryGetCurveName(value, out var curve) && !AllowedCurves.Contains(curve))
+        {
+            Report(context, syntax, $"Curve '{curve}' is not P-384 or P-521; use nistP384 or nistP521");
+        }
+    }
+
+    private static bool TryGetCurveName(IOperation value, out string curve, int depth = 0)
+    {
+        curve = string.Empty;
+        var operation = FipsSymbolHelpers.Unwrap(value);
+
+        // A curve held in a readonly field or an unreassigned local resolves to its initializer.
+        if (operation is IFieldReferenceOperation or ILocalReferenceOperation)
+        {
+            return depth < 4 &&
+                FipsSymbolHelpers.ResolveInitializer(operation) is { } resolved &&
+                TryGetCurveName(resolved, out curve, depth + 1);
+        }
+
+        // ECCurve.NamedCurves.nistP256
+        if (operation is IPropertyReferenceOperation { Property: { IsStatic: true } property } &&
+            property.ContainingType is { Name: "NamedCurves", ContainingType: { } outer } &&
+            FipsSymbolHelpers.IsCryptographyType(outer, "ECCurve"))
+        {
+            curve = property.Name;
+            return true;
+        }
+
+        // ECCurve.CreateFromFriendlyName("nistP256") and ECCurve.CreateFromValue("1.2.840.10045.3.1.7")
+        if (operation is IInvocationOperation { TargetMethod: { IsStatic: true } method } invocation &&
+            (string.Equals(method.Name, "CreateFromFriendlyName", StringComparison.Ordinal) ||
+             string.Equals(method.Name, "CreateFromValue", StringComparison.Ordinal)) &&
+            FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "ECCurve") &&
+            invocation.Arguments.Length == 1 &&
+            FipsSymbolHelpers.TryGetConstant<string>(invocation.Arguments[0].Value, out var text))
+        {
+            curve = text;
+            return true;
+        }
+
+        // ECCurve.CreateFromOid(new Oid("1.2.840.10045.3.1.7"))
+        if (operation is IInvocationOperation { TargetMethod: { IsStatic: true, Name: "CreateFromOid" } oidMethod } oidInvocation &&
+            FipsSymbolHelpers.IsCryptographyType(oidMethod.ContainingType, "ECCurve") &&
+            oidInvocation.Arguments.Length == 1 &&
+            FipsSymbolHelpers.Unwrap(oidInvocation.Arguments[0].Value) is IObjectCreationOperation oid &&
+            FipsSymbolHelpers.IsCryptographyType(oid.Type, "Oid") &&
+            FipsSymbolHelpers.ArgumentAt(oid.Arguments, 0)?.Value is { } oidValue &&
+            FipsSymbolHelpers.TryGetConstant<string>(oidValue, out var oidText))
+        {
+            curve = oidText;
+            return true;
+        }
+
+        return false;
+    }
+
+    private enum Family
+    {
+        None = 0,
+        Rsa = 1,
+        EllipticCurve = 2,
+    }
+
+    private static void Report(OperationAnalysisContext context, SyntaxNode syntax, string message) =>
+        context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.GetLocation(), message));
+}
diff --git a/src/E128.Analyzers/Security/FipsSha3Analyzer.cs b/src/E128.Analyzers/Security/FipsSha3Analyzer.cs
new file mode 100644
index 0000000..8581da5
--- /dev/null
+++ b/src/E128.Analyzers/Security/FipsSha3Analyzer.cs
@@ -0,0 +1,164 @@
+using System;
+using System.Collections.Immutable;
+using Microsoft.CodeAnalysis;
+using Microsoft.CodeAnalysis.Diagnostics;
+using Microsoft.CodeAnalysis.Operations;
+
+namespace E128.Analyzers.Security;
+
+/// <summary>
+///     E128150: Reports SHA-3, SHAKE, KMAC and HMAC-SHA-3 usage. NIST SP 800-131A Rev. 2 lists SHA3-224
+///     through SHA3-512 as acceptable, and FIPS 202 specifies SHAKE. This rule is organizational policy.
+/// </summary>
+[DiagnosticAnalyzer(LanguageNames.CSharp)]
+public sealed class FipsSha3Analyzer : DiagnosticAnalyzer
+{
+    internal const string DiagnosticId = "E128150";
+
+    private static readonly ImmutableHashSet<string> Sha3Types = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "SHA3_256",
+        "SHA3_384",
+        "SHA3_512",
+        "Shake128",
+        "Shake256",
+        "HMACSHA3_256",
+        "HMACSHA3_384",
+        "HMACSHA3_512",
+        "Kmac128",
+        "Kmac256",
+        "KmacXof128",
+        "KmacXof256");
+
+    private static readonly ImmutableHashSet<string> Sha3AlgorithmNames = ImmutableHashSet.Create(
+        StringComparer.OrdinalIgnoreCase,
+        "SHA3_256",
+        "SHA3_384",
+        "SHA3_512",
+        "SHA3-256",
+        "SHA3-384",
+        "SHA3-512");
+
+    private static readonly DiagnosticDescriptor Rule = new(
+        DiagnosticId,
+        "Do not use SHA-3, SHAKE or KMAC",
+        "'{0}' is blocked by policy; use SHA-256, SHA-384 or SHA-512",
+        "Security",
+        DiagnosticSeverity.Error,
+        true,
+        "This organization's policy blocks SHA-3 and SHAKE. The rule is organizational policy, not a " +
+        "statement that NIST forbids these algorithms. Reading IsSupported is not reported.");
+
+    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];
+
+    public override void Initialize(AnalysisContext context)
+    {
+        if (context is null)
+        {
+            throw new ArgumentNullException(nameof(context));
+        }
+
+        context.ConfigureGeneratedCodeAnalysis(
+            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
+        context.EnableConcurrentExecution();
+
+        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
+        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
+        context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
+        context.RegisterOperationAction(AnalyzeMethodReference, OperationKind.MethodReference);
+        context.RegisterOperationAction(
+            AnalyzeHashNameStore,
+            OperationKind.SimpleAssignment,
+            OperationKind.CoalesceAssignment,
+            OperationKind.DeconstructionAssignment,
+            OperationKind.FieldInitializer,
+            OperationKind.PropertyInitializer);
+    }
+
+    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
+    {
+        var creation = (IObjectCreationOperation)context.Operation;
+        var syntax = creation.Syntax;
+
+        if (!ReportType(context, creation.Type, syntax))
+        {
+            ReportHashNameArguments(context, creation.Constructor, creation.Arguments, syntax);
+        }
+    }
+
+    private static void AnalyzePropertyReference(OperationAnalysisContext context)
+    {
+        var reference = (IPropertyReferenceOperation)context.Operation;
+        var property = reference.Property;
+
+        // RSAEncryptionPadding.OaepSHA3_256 and its siblings.
+        if (property.IsStatic &&
+            property.Name.StartsWith("OaepSHA3_", StringComparison.Ordinal) &&
+            FipsSymbolHelpers.IsCryptographyType(property.ContainingType, "RSAEncryptionPadding"))
+        {
+            context.ReportDiagnostic(Diagnostic.Create(
+                Rule,
+                reference.Syntax.GetLocation(),
+                property.Name.Substring("Oaep".Length)));
+        }
+    }
+
+    private static void AnalyzeMethodReference(OperationAnalysisContext context)
+    {
+        var reference = (IMethodReferenceOperation)context.Operation;
+        if (reference.Method.IsStatic)
+        {
+            ReportType(context, reference.Method.ContainingType, reference.Syntax);
+        }
+    }
+
+    private static void AnalyzeInvocation(OperationAnalysisContext context)
+    {
+        var invocation = (IInvocationOperation)context.Operation;
+        var method = invocation.TargetMethod;
+        var syntax = invocation.Syntax;
+
+        // SHA3_256.HashData(...), Shake256.HashData(...) and similar static members.
+        if (method.IsStatic && ReportType(context, method.ContainingType, syntax))
+        {
+            return;
+        }
+
+        ReportHashNameArguments(context, method, invocation.Arguments, syntax);
+    }
+
+    private static void ReportHashNameArguments(
+        OperationAnalysisContext context,
+        IMethodSymbol? method,
+        ImmutableArray<IArgumentOperation> arguments,
+        SyntaxNode syntax) =>
+        FipsSymbolHelpers.ReportHashNameArguments(
+            method,
+            arguments,
+            syntax,
+            context,
+            static (_, name) => Sha3AlgorithmNames.Contains(name),
+            static (state, node, name) => Report(state, node, name));
+
+    private static void AnalyzeHashNameStore(OperationAnalysisContext context) =>
+        FipsSymbolHelpers.ReportHashNameStores(
+            context.Operation,
+            context,
+            static (_, name) => Sha3AlgorithmNames.Contains(name),
+            static (state, node, name) => Report(state, node, name));
+
+    private static void Report(OperationAnalysisContext context, SyntaxNode syntax, string name) =>
+        context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.GetLocation(), name));
+
+    private static bool ReportType(OperationAnalysisContext context, ITypeSymbol? type, SyntaxNode syntax)
+    {
+        if (type is null || !FipsSymbolHelpers.IsCryptographyType(type, Sha3Types))
+        {
+            return false;
+        }
+
+        context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.GetLocation(), type.Name));
+
+        return true;
+    }
+}
diff --git a/src/E128.Analyzers/Security/FipsSymbolHelpers.cs b/src/E128.Analyzers/Security/FipsSymbolHelpers.cs
new file mode 100644
index 0000000..67806bb
--- /dev/null
+++ b/src/E128.Analyzers/Security/FipsSymbolHelpers.cs
@@ -0,0 +1,1293 @@
+using System;
+using System.Collections.Concurrent;
+using System.Collections.Generic;
+using System.Collections.Immutable;
+using System.Diagnostics.CodeAnalysis;
+using System.Runtime.CompilerServices;
+using System.Text;
+using Microsoft.CodeAnalysis;
+using Microsoft.CodeAnalysis.CSharp.Syntax;
+using Microsoft.CodeAnalysis.Operations;
+
+namespace E128.Analyzers.Security;
+
+/// <summary>
+///     Shared symbol and constant helpers for the FIPS cryptography rules. Type matching uses symbols
+///     (namespace and name), not source text, so a <c lang="csharp">using</c> alias or <c lang="csharp">using static</c> cannot hide a type.
+/// </summary>
+internal static class FipsSymbolHelpers
+{
+    public const string Cryptography = "System.Security.Cryptography";
+
+    private const int MaxResolveDepth = 4;
+
+    private const string HashAlgorithmNameType = "HashAlgorithmName";
+
+    private static readonly SymbolDisplayFormat FullyQualifiedNoGlobal =
+        SymbolDisplayFormat.FullyQualifiedFormat
+            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);
+
+    // Collection and dictionary probes read a hash name to guard against it, not to select it.
+    private static readonly ImmutableHashSet<string> HashNameProbeMembers = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "Equals", "Contains", "ContainsKey", "IndexOf", "Remove", "TryGetValue");
+
+    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<IFieldSymbol, bool>> AssignedInConstructorCache = new();
+
+    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<ILocalSymbol, bool>> NeverReassignedCache = new();
+
+    /// <summary>
+    ///     Returns the fully qualified type name without <c lang="csharp">global::</c>. Call this only when reporting,
+    ///     because formatting allocates.
+    /// </summary>
+    public static string? FullName(ITypeSymbol? type) =>
+        type?.ToDisplayString(FullyQualifiedNoGlobal);
+
+    /// <summary>Returns true when the namespace chain equals <paramref name="dotted" />, such as <c lang="csharp">System.Net</c>.</summary>
+    public static bool NamespaceEquals(INamespaceSymbol? ns, string dotted)
+    {
+        var end = dotted.Length;
+        for (; ns is not null && !ns.IsGlobalNamespace; ns = ns.ContainingNamespace)
+        {
+            if (end < 0)
+            {
+                return false;
+            }
+
+            var dot = end > 0 ? dotted.LastIndexOf('.', end - 1) : -1;
+            var start = dot + 1;
+            var length = end - start;
+            if (ns.Name.Length != length || string.CompareOrdinal(ns.Name, 0, dotted, start, length) != 0)
+            {
+                return false;
+            }
+
+            end = dot;
+        }
+
+        return end < 0;
+    }
+
+    /// <summary>Returns true when <paramref name="type" /> is a top-level type with the given name and namespace.</summary>
+    public static bool IsType(ITypeSymbol? type, string name, string dottedNamespace) =>
+        type is INamedTypeSymbol { ContainingType: null } named &&
+        string.Equals(named.Name, name, StringComparison.Ordinal) &&
+        NamespaceEquals(named.ContainingNamespace, dottedNamespace);
+
+    public static bool IsCryptographyType(ITypeSymbol? type, string name) =>
+        IsType(type, name, Cryptography);
+
+    /// <summary>Returns true when <paramref name="type" /> is a top-level cryptography type whose name is in the set.</summary>
+    public static bool IsCryptographyType(ITypeSymbol? type, ImmutableHashSet<string> names) =>
+        type is INamedTypeSymbol { ContainingType: null } named &&
+        names.Contains(named.Name) &&
+        NamespaceEquals(named.ContainingNamespace, Cryptography);
+
+    public static bool IsInCryptographyNamespace(ITypeSymbol? type) =>
+        type is INamedTypeSymbol named && NamespaceEquals(named.ContainingNamespace, Cryptography);
+
+    /// <summary>
+    ///     Returns true when the type sits in System.Security.Cryptography or one of its child namespaces,
+    ///     such as <c lang="csharp">System.Security.Cryptography.X509Certificates</c>.
+    /// </summary>
+    public static bool IsInCryptographyNamespaceTree(ITypeSymbol? type)
+    {
+        if (type is not INamedTypeSymbol named)
+        {
+            return false;
+        }
+
+        for (var ns = named.ContainingNamespace; ns is not null && !ns.IsGlobalNamespace; ns = ns.ContainingNamespace)
+        {
+            if (NamespaceEquals(ns, Cryptography))
+            {
+                return true;
+            }
+        }
+
+        return false;
+    }
+
+    /// <summary>Returns true when the type, or one of its base types, is the named cryptography type.</summary>
+    public static bool DerivesFromCryptographyType(ITypeSymbol? type, string name)
+    {
+        for (var current = type; current is not null; current = current.BaseType)
+        {
+            if (IsCryptographyType(current, name))
+            {
+                return true;
+            }
+        }
+
+        return false;
+    }
+
+    /// <summary>
+    ///     Returns the argument bound to the parameter at <paramref name="ordinal" />. The lookup uses the
+    ///     parameter, not the array position, because named arguments can reorder the source.
+    /// </summary>
+    public static IArgumentOperation? ArgumentAt(ImmutableArray<IArgumentOperation> arguments, int ordinal)
+    {
+        foreach (var argument in arguments)
+        {
+            if (argument.Parameter?.Ordinal == ordinal)
+            {
+                return argument;
+            }
+        }
+
+        return null;
+    }
+
+    /// <summary>Returns the argument bound to the parameter with the given name, or null.</summary>
+    public static IArgumentOperation? ArgumentNamed(ImmutableArray<IArgumentOperation> arguments, string parameterName)
+    {
+        foreach (var argument in arguments)
+        {
+            if (argument.Parameter is { } parameter && string.Equals(parameter.Name, parameterName, StringComparison.Ordinal))
+            {
+                return argument;
+            }
+        }
+
+        return null;
+    }
+
+    /// <summary>
+    ///     Returns true when the type sits in the <c lang="csharp">System</c> or <c lang="csharp">Microsoft</c> namespace tree,
+    ///     such as <c lang="csharp">System.Collections.Generic.List&lt;T&gt;</c>. A type in any other namespace belongs to
+    ///     application or third-party code.
+    /// </summary>
+    public static bool IsBclType(ITypeSymbol? type)
+    {
+        var ns = type?.ContainingNamespace;
+        if (ns is null || ns.IsGlobalNamespace)
+        {
+            return false;
+        }
+
+        while (ns.ContainingNamespace is { IsGlobalNamespace: false } parent)
+        {
+            ns = parent;
+        }
+
+        return ns.Name is "System" or "Microsoft";
+    }
+
+    /// <summary>
+    ///     Reports each argument of <paramref name="method" /> that carries a hash name <paramref name="isForbidden" />
+    ///     accepts. The arguments are found by parameter type, so named and reordered arguments resolve correctly.
+    ///     A probe such as <c lang="csharp">Equals</c> or <c lang="csharp">Contains</c> on a framework type is skipped.
+    ///     Each name reports once per call.
+    /// </summary>
+    public static void ReportHashNameArguments(
+        IMethodSymbol? method,
+        ImmutableArray<IArgumentOperation> arguments,
+        SyntaxNode syntax,
+        Func<string, bool> isForbidden,
+        Action<SyntaxNode, string> report) =>
+        ReportHashNameArguments(
+            method,
+            arguments,
+            syntax,
+            (isForbidden, report),
+            static (state, name) => state.isForbidden(name),
+            static (state, node, name) => state.report(node, name));
+
+    /// <summary>
+    ///     Like <see cref="ReportHashNameArguments" /> but passes <paramref name="state" /> to both delegates, so a
+    ///     caller can use static lambdas and avoid a closure allocation on every call.
+    /// </summary>
+    /// <typeparam name="TState">The type of the state passed to both delegates.</typeparam>
+    public static void ReportHashNameArguments<TState>(
+        IMethodSymbol? method,
+        ImmutableArray<IArgumentOperation> arguments,
+        SyntaxNode syntax,
+        TState state,
+        Func<TState, string, bool> isForbidden,
+        Action<TState, SyntaxNode, string> report)
+    {
+        var hasHashParameter = false;
+        foreach (var argument in arguments)
+        {
+            if (IsCryptographyType(argument.Parameter?.Type, HashAlgorithmNameType))
+            {
+                hasHashParameter = true;
+                break;
+            }
+        }
+
+        if (!hasHashParameter)
+        {
+            return;
+        }
+
+        if (method is not null && HashNameProbeMembers.Contains(method.Name) && IsBclType(method.ContainingType))
+        {
+            return;
+        }
+
+        var seen = default(NameSet);
+        foreach (var argument in arguments)
+        {
+            if (IsCryptographyType(argument.Parameter?.Type, HashAlgorithmNameType))
+            {
+                ReportNames(argument.Value, syntax, ref seen, state, isForbidden, report);
+            }
+        }
+    }
+
+    /// <summary>
+    ///     Reports a hash name stored where <see cref="ResolveInitializer" /> cannot follow it: an assignment to any
+    ///     <c lang="csharp">HashAlgorithmName</c> target, and the initializer of a mutable field or a property. A <c lang="csharp">readonly</c>
+    ///     field with an initializer resolves at its use, so its initializer is skipped.
+    /// </summary>
+    public static void ReportHashNameStores(
+        IOperation operation,
+        Func<string, bool> isForbidden,
+        Action<SyntaxNode, string> report) =>
+        ReportHashNameStores(
+            operation,
+            (isForbidden, report),
+            static (state, name) => state.isForbidden(name),
+            static (state, node, name) => state.report(node, name));
+
+    /// <summary>
+    ///     Like <see cref="ReportHashNameStores" /> but passes <paramref name="state" /> to both delegates, so a
+    ///     caller can use static lambdas and avoid a closure allocation on every operation.
+    /// </summary>
+    /// <typeparam name="TState">The type of the state passed to both delegates.</typeparam>
+    public static void ReportHashNameStores<TState>(
+        IOperation operation,
+        TState state,
+        Func<TState, string, bool> isForbidden,
+        Action<TState, SyntaxNode, string> report)
+    {
+        var seen = default(NameSet);
+        switch (operation)
+        {
+            case IFieldInitializerOperation { InitializedFields.Length: 1 } field
+                when !field.InitializedFields[0].IsReadOnly && IsCryptographyType(field.InitializedFields[0].Type, HashAlgorithmNameType):
+                ReportNames(field.Value, field.Value.Syntax, ref seen, state, isForbidden, report);
+                break;
+
+            case IPropertyInitializerOperation { InitializedProperties.Length: 1 } property
+                when IsCryptographyType(property.InitializedProperties[0].Type, HashAlgorithmNameType):
+                ReportNames(property.Value, property.Value.Syntax, ref seen, state, isForbidden, report);
+                break;
+
+            case IFieldInitializerOperation or IPropertyInitializerOperation:
+                break;
+
+            default:
+                ForEachAssignment(
+                    operation,
+                    (state, isForbidden, report),
+                    static (context, target, value, syntax) =>
+                    {
+                        if (!IsCryptographyType(target.Type, HashAlgorithmNameType))
+                        {
+                            return;
+                        }
+
+                        var assigned = default(NameSet);
+                        ReportNames(value, syntax, ref assigned, context.state, context.isForbidden, context.report);
+                    });
+
+                break;
+        }
+    }
+
+    private static void ReportNames<TState>(
+        IOperation value,
+        SyntaxNode syntax,
+        ref NameSet seen,
+        TState state,
+        Func<TState, string, bool> isForbidden,
+        Action<TState, SyntaxNode, string> report)
+    {
+        foreach (var name in HashAlgorithmNames(value))
+        {
+            if (isForbidden(state, name) && seen.Add(name))
+            {
+                report(state, syntax, name);
+            }
+        }
+    }
+
+    /// <summary>
+    ///     Reads every hash name an expression can produce: one name, or both arms of a conditional, a <c lang="csharp">??</c> or
+    ///     a switch expression, including arms held in an unreassigned local or a <c lang="csharp">readonly</c> field.
+    /// </summary>
+    public static IEnumerable<string> HashAlgorithmNames(IOperation? operation, int depth = 0)
+    {
+        operation = Unwrap(operation);
+        if (operation is null || depth > MaxResolveDepth)
+        {
+            yield break;
+        }
+
+        if (operation is IConditionalOperation or ICoalesceOperation or ISwitchExpressionOperation)
+        {
+            foreach (var name in BranchHashAlgorithmNames(operation, depth))
+            {
+                yield return name;
+            }
+
+            yield break;
+        }
+
+        if (TryGetDirectHashAlgorithmName(operation, out var single))
+        {
+            yield return single;
+            yield break;
+        }
+
+        if (ResolveInitializer(operation) is { } resolved)
+        {
+            foreach (var name in HashAlgorithmNames(resolved, depth + 1))
+            {
+                yield return name;
+            }
+        }
+    }
+
+    /// <summary>Reads the names from both arms of a conditional, a <c lang="csharp">??</c> or a switch expression.</summary>
+    private static IEnumerable<string> BranchHashAlgorithmNames(IOperation operation, int depth)
+    {
+        if (operation is IConditionalOperation conditional)
+        {
+            foreach (var name in HashAlgorithmNames(conditional.WhenTrue, depth + 1))
+            {
+                yield return name;
+            }
+
+            foreach (var name in HashAlgorithmNames(conditional.WhenFalse, depth + 1))
+            {
+                yield return name;
+            }
+
+            yield break;
+        }
+
+        if (operation is ICoalesceOperation coalesce)
+        {
+            foreach (var name in HashAlgorithmNames(coalesce.Value, depth + 1))
+            {
+                yield return name;
+            }
+
+            foreach (var name in HashAlgorithmNames(coalesce.WhenNull, depth + 1))
+            {
+                yield return name;
+            }
+
+            yield break;
+        }
+
+        if (operation is ISwitchExpressionOperation switchExpression)
+        {
+            foreach (var arm in switchExpression.Arms)
+            {
+                foreach (var name in HashAlgorithmNames(arm.Value, depth + 1))
+                {
+                    yield return name;
+                }
+            }
+        }
+    }
+
+    /// <summary>Tracks the names already reported for one call site or store, so a repeated name reports once.</summary>
+    private struct NameSet
+    {
+        private string? _first;
+        private HashSet<string>? _others;
+
+        public bool Add(string name)
+        {
+            if (_first is null)
+            {
+                _first = name;
+                return true;
+            }
+
+            if (string.Equals(_first, name, StringComparison.Ordinal))
+            {
+                return false;
+            }
+
+            _others ??= new HashSet<string>(StringComparer.Ordinal);
+
+            return _others.Add(name);
+        }
+    }
+
+    /// <summary>
+    ///     Returns the arguments whose parameter type is <c lang="csharp">System.Security.Cryptography.HashAlgorithmName</c>.
+    ///     Allocates only when at least one argument matches.
+    /// </summary>
+    public static ImmutableArray<IArgumentOperation> HashNameArguments(ImmutableArray<IArgumentOperation> arguments)
+    {
+        var count = 0;
+        foreach (var argument in arguments)
+        {
+            if (IsCryptographyType(argument.Parameter?.Type, HashAlgorithmNameType))
+            {
+                count++;
+            }
+        }
+
+        if (count == 0)
+        {
+            return [];
+        }
+
+        var result = ImmutableArray.CreateBuilder<IArgumentOperation>(count);
+        foreach (var argument in arguments)
+        {
+            if (IsCryptographyType(argument.Parameter?.Type, HashAlgorithmNameType))
+            {
+                result.Add(argument);
+            }
+        }
+
+        return result.MoveToImmutable();
+    }
+
+    /// <summary>
+    ///     Returns the first reference to the local that receives <paramref name="creation" /> (through a
+    ///     declaration or a plain assignment) in the rest of the same block, or null when the value does not
+    ///     land in a local or the local is never used again. <paramref name="block" /> is that block.
+    /// </summary>
+    public static ILocalReferenceOperation? FindFirstLocalUse(IOperation creation, out IBlockOperation? block)
+    {
+        block = null;
+        var local = GetReceivingLocal(creation);
+        if (local is null)
+        {
+            return null;
+        }
+
+        // The block that holds the statement, or the body of a 'using (var x = ...)' statement whose resource
+        // is the creation, so the first statement of that body counts as the first use.
+        IBlockOperation? parentBlock = null;
+        for (var statement = creation; statement.Parent is not null; statement = statement.Parent)
+        {
+            if (statement.Parent is IUsingOperation { Body: IBlockOperation body } usingStatement && usingStatement.Resources == statement)
+            {
+                parentBlock = body;
+                break;
+            }
+
+            if (statement.Parent is IBlockOperation enclosing)
+            {
+                parentBlock = enclosing;
+                break;
+            }
+        }
+
+        if (parentBlock is null)
+        {
+            return null;
+        }
+
+        block = parentBlock;
+        var end = creation.Syntax.Span.End;
+        ILocalReferenceOperation? first = null;
+        foreach (var descendant in parentBlock.Descendants())
+        {
+            if (descendant is ILocalReferenceOperation reference &&
+                SymbolEqualityComparer.Default.Equals(reference.Local, local) &&
+                reference.Syntax.SpanStart >= end &&
+                (first is null || reference.Syntax.SpanStart < first.Syntax.SpanStart))
+            {
+                first = reference;
+            }
+        }
+
+        return first;
+    }
+
+    /// <summary>
+    ///     Returns true when <paramref name="use" /> is the receiver of <c lang="csharp">use.Property = value</c> written as its
+    ///     own statement directly in <paramref name="block" />, so the assignment runs before any later code.
+    /// </summary>
+    public static bool IsUnconditionalPropertyAssignment(ILocalReferenceOperation use, string propertyName, IBlockOperation? block) =>
+        block is not null &&
+        use.Parent is IPropertyReferenceOperation property &&
+        string.Equals(property.Property.Name, propertyName, StringComparison.Ordinal) &&
+        property.Instance == use &&
+        property.Parent is ISimpleAssignmentOperation assignment &&
+        assignment.Target == property &&
+        assignment.Parent is IExpressionStatementOperation statement &&
+        statement.Parent == block;
+
+    /// <summary>
+    ///     Returns true when <paramref name="target" /> is a property or field reference named
+    ///     <paramref name="memberName" />. Callers use it as a cheap pre-check before any type lookup.
+    /// </summary>
+    public static bool IsCandidateAssignmentTarget(IOperation? target, string memberName) => target switch
+    {
+        IPropertyReferenceOperation property => string.Equals(property.Property.Name, memberName, StringComparison.Ordinal),
+        IFieldReferenceOperation field => string.Equals(field.Field.Name, memberName, StringComparison.Ordinal),
+        _ => false,
+    };
+
+    /// <summary>
+    ///     Returns false when <paramref name="assignment" /> is a simple or compound assignment whose target cannot be
+    ///     the member named <paramref name="memberName" />. A tuple deconstruction returns true, because any element
+    ///     can match. Call it before <see cref="ForEachAssignment{TState}" /> to skip an unrelated assignment.
+    /// </summary>
+    public static bool MayAssignMember(IOperation assignment, string memberName) =>
+        assignment is not IAssignmentOperation single ||
+        assignment is IDeconstructionAssignmentOperation ||
+        IsCandidateAssignmentTarget(single.Target, memberName);
+
+    /// <summary>Returns true when the object initializer of <paramref name="creation" /> assigns the property.</summary>
+    public static bool InitializerAssigns(IObjectCreationOperation creation, string propertyName)
+    {
+        if (creation.Initializer is null)
+        {
+            return false;
+        }
+
+        foreach (var initializer in creation.Initializer.Initializers)
+        {
+            if (initializer is ISimpleAssignmentOperation { Target: IPropertyReferenceOperation property } &&
+                string.Equals(property.Property.Name, propertyName, StringComparison.Ordinal))
+            {
+                return true;
+            }
+        }
+
+        return false;
+    }
+
+    /// <summary>
+    ///     Calls <paramref name="check" /> with each target an assignment writes, the value it receives, and the
+    ///     syntax to report. It covers simple, compound, <c lang="csharp">??=</c> and tuple deconstruction assignments.
+    /// </summary>
+    public static void ForEachAssignment(IOperation operation, Action<IOperation, IOperation, SyntaxNode> check) =>
+        ForEachAssignment(operation, check, static (callback, target, value, syntax) => callback(target, value, syntax));
+
+    /// <summary>
+    ///     Like <see cref="ForEachAssignment(IOperation, Action{IOperation, IOperation, SyntaxNode})" /> but passes
+    ///     <paramref name="state" /> to <paramref name="check" />, so a caller can use a static lambda and avoid a
+    ///     closure allocation on every operation.
+    /// </summary>
+    /// <typeparam name="TState">The type of the state passed to the check delegate.</typeparam>
+    public static void ForEachAssignment<TState>(IOperation operation, TState state, Action<TState, IOperation, IOperation, SyntaxNode> check)
+    {
+        if (operation is IDeconstructionAssignmentOperation deconstruction)
+        {
+            if (TryGetDeconstructedPairs(deconstruction, out var targets, out var values))
+            {
+                for (var i = 0; i < targets.Length; i++)
+                {
+                    check(state, targets[i], values[i], values[i].Syntax);
+                }
+            }
+
+            return;
+        }
+
+        if (operation is ICompoundAssignmentOperation compound)
+        {
+            check(state, compound.Target, compound, compound.Syntax);
+
+            return;
+        }
+
+        if (operation is IAssignmentOperation assignment)
+        {
+            check(state, assignment.Target, assignment.Value, assignment.Syntax);
+        }
+    }
+
+    /// <summary>
+    ///     Returns true when <paramref name="operation" />, after any conversions, is an operand of <c lang="csharp">==</c> or
+    ///     <c lang="csharp">!=</c>, a constant pattern, a <c lang="csharp">case</c> label, or the argument of <c lang="csharp">Equals</c> or <c lang="csharp">HasFlag</c>.
+    ///     A comparison reads a value and does not select it.
+    /// </summary>
+    public static bool IsComparison(IOperation operation)
+    {
+        var parent = operation.Parent;
+        while (parent is IConversionOperation)
+        {
+            parent = parent.Parent;
+        }
+
+        return parent is IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals }
+            or IConstantPatternOperation
+            or ISingleValueCaseClauseOperation
+            or IArgumentOperation { Parent: IInvocationOperation { TargetMethod.Name: "Equals" or "HasFlag" } };
+    }
+
+    /// <summary>Removes conversion wrappers to reach the value an expression produces.</summary>
+    public static IOperation? Unwrap(IOperation? operation)
+    {
+        while (operation is IConversionOperation conversion)
+        {
+            operation = conversion.Operand;
+        }
+
+        return operation;
+    }
+
+    /// <summary>
+    ///     Reads a compile-time constant of type <typeparamref name="T" /> (<see cref="int" /> or
+    ///     <see cref="string" />), including <c lang="csharp">const</c> fields, folded expressions and string concatenations.
+    /// </summary>
+    /// <typeparam name="T">The constant type.</typeparam>
+    public static bool TryGetConstant<T>(IOperation? operation, [MaybeNullWhen(false)] out T value)
+    {
+        if (operation is { ConstantValue: { HasValue: true, Value: T constant } })
+        {
+            value = constant;
+            return true;
+        }
+
+        value = default;
+
+        return false;
+    }
+
+    /// <summary>
+    ///     Reads a compile-time constant held in an unreassigned local or a readonly field that no constructor
+    ///     writes. A parameter or a reassigned value is not resolved.
+    /// </summary>
+    /// <typeparam name="T">The constant type.</typeparam>
+    public static bool TryGetResolvedConstant<T>(IOperation? operation, [MaybeNullWhen(false)] out T value, int depth = 0)
+    {
+        operation = Unwrap(operation);
+
+        return TryGetConstant(operation, out value) ||
+            (depth < MaxResolveDepth &&
+             ResolveInitializer(operation) is { } resolved &&
+             TryGetResolvedConstant(resolved, out value, depth + 1));
+    }
+
+    /// <summary>
+    ///     Returns the initializer of the field or local that <paramref name="operation" /> reads, or null. Only a
+    ///     <c lang="csharp">readonly</c> field that no constructor of its type assigns, or a local that nothing assigns after its
+    ///     declaration, resolves, so the initializer is the value every read sees. A declaration in a syntax tree
+    ///     outside the compilation (a field from a referenced project) does not resolve.
+    /// </summary>
+    public static IOperation? ResolveInitializer(IOperation? operation)
+    {
+        operation = Unwrap(operation);
+
+        if (operation?.SemanticModel is not { } model)
+        {
+            return null;
+        }
+
+        SyntaxReference? declaration = null;
+        if (operation is IFieldReferenceOperation { Field.IsReadOnly: true } field)
+        {
+            if (field.Field.DeclaringSyntaxReferences.Length == 1 &&
+                model.Compilation.ContainsSyntaxTree(field.Field.DeclaringSyntaxReferences[0].SyntaxTree) &&
+                !IsAssignedInConstructor(field.Field, model.Compilation))
+            {
+                declaration = field.Field.DeclaringSyntaxReferences[0];
+            }
+        }
+        else if (operation is ILocalReferenceOperation local &&
+                 IsNeverReassigned(local) &&
+                 local.Local.DeclaringSyntaxReferences.Length == 1)
+        {
+            declaration = local.Local.DeclaringSyntaxReferences[0];
+        }
+
+        var syntax = declaration?.GetSyntax();
+
+        return syntax is VariableDeclaratorSyntax { Initializer.Value: { } value } &&
+            model.Compilation.ContainsSyntaxTree(syntax.SyntaxTree)
+                ? model.Compilation.GetSemanticModel(syntax.SyntaxTree).GetOperation(value)
+                : null;
+    }
+
+    /// <summary>
+    ///     Reads the algorithm name from a <c lang="csharp">HashAlgorithmName.X</c> property or a
+    ///     <c lang="csharp">new HashAlgorithmName("X")</c> expression with a constant name, a <c lang="csharp">HashAlgorithmName.FromOid</c> call
+    ///     with a known constant object identifier, either written inline or held in a <c lang="csharp">readonly</c> field or an
+    ///     unreassigned local.
+    /// </summary>
+    public static bool TryGetHashAlgorithmName(IOperation? operation, out string name, int depth = 0)
+    {
+        name = string.Empty;
+        operation = Unwrap(operation);
+
+        return TryGetDirectHashAlgorithmName(operation, out name) ||
+            (depth < MaxResolveDepth &&
+             ResolveInitializer(operation) is { } resolved &&
+             TryGetHashAlgorithmName(resolved, out name, depth + 1));
+    }
+
+    private static bool TryGetDirectHashAlgorithmName(IOperation? operation, out string name)
+    {
+        name = string.Empty;
+        operation = Unwrap(operation);
+
+        if (operation is IPropertyReferenceOperation { Property: { IsStatic: true } property } &&
+            IsCryptographyType(property.ContainingType, HashAlgorithmNameType))
+        {
+            name = property.Name;
+            return true;
+        }
+
+        if (operation is IObjectCreationOperation creation &&
+            IsCryptographyType(creation.Type, HashAlgorithmNameType) &&
+            creation.Arguments.Length == 1 &&
+            TryGetConstant<string>(creation.Arguments[0].Value, out var text))
+        {
+            name = text;
+            return true;
+        }
+
+        if (operation is IInvocationOperation { TargetMethod: { IsStatic: true, Name: "FromOid" } fromOid } oidCall &&
+            IsCryptographyType(fromOid.ContainingType, HashAlgorithmNameType) &&
+            TryGetConstant<string>(ArgumentAt(oidCall.Arguments, 0)?.Value, out var oid) &&
+            HashNameFromOid(oid) is { } oidName)
+        {
+            name = oidName;
+            return true;
+        }
+
+        return false;
+    }
+
+    /// <summary>
+    ///     Reads the byte length of a buffer when it is a fixed-size array (<c lang="csharp">new byte[N]</c>,
+    ///     <c lang="csharp">new byte[] { ... }</c>), a collection expression, a <c lang="csharp">stackalloc</c>, or an encoded
+    ///     string with a constant argument, either written inline or held in a <c lang="csharp">readonly</c> field or an
+    ///     unreassigned local.
+    /// </summary>
+    public static bool TryGetConstantByteLength(IOperation? operation, out int length, int depth = 0)
+    {
+        length = 0;
+        operation = Unwrap(operation);
+
+        if (operation is IInvocationOperation invocation && TryGetKnownByteLength(invocation, out length, depth))
+        {
+            return true;
+        }
+
+        switch (operation)
+        {
+            case null:
+                return false;
+
+            case IArrayCreationOperation { DimensionSizes.Length: 1 } array:
+                if (TryGetConstant<int>(array.DimensionSizes[0], out var size))
+                {
+                    length = size;
+                    return true;
+                }
+
+                if (array.Initializer is not null)
+                {
+                    length = array.Initializer.ElementValues.Length;
+                    return true;
+                }
+
+                return false;
+
+            case ICollectionExpressionOperation collection:
+                foreach (var element in collection.Elements)
+                {
+                    if (element is ISpreadOperation)
+                    {
+                        return false;
+                    }
+                }
+
+                length = collection.Elements.Length;
+                return true;
+
+            case IImplicitIndexerReferenceOperation { Argument: { } rangeArgument } indexer
+                when Unwrap(rangeArgument) is IRangeOperation range:
+                return TryGetRangeLength(indexer.Instance, range.LeftOperand, range.RightOperand, depth, out length);
+
+            case IArrayElementReferenceOperation { Indices.Length: 1 } element
+                when Unwrap(element.Indices[0]) is IRangeOperation arrayRange:
+                return TryGetRangeLength(element.ArrayReference, arrayRange.LeftOperand, arrayRange.RightOperand, depth, out length);
+
+            case IFieldReferenceOperation or ILocalReferenceOperation:
+                return depth < MaxResolveDepth &&
+                    ResolveInitializer(operation) is { } resolved &&
+                    TryGetConstantByteLength(resolved, out length, depth + 1);
+
+            default:
+                // Roslyn does not expose a public operation interface for stackalloc, so read the syntax.
+                return TryGetStackAllocLength(operation, out length);
+        }
+    }
+
+    private static bool TryGetKnownByteLength(IInvocationOperation invocation, out int length, int depth)
+    {
+        length = 0;
+        if (depth >= MaxResolveDepth)
+        {
+            return false;
+        }
+
+        if (TryGetSliceLength(invocation, out length, depth))
+        {
+            return true;
+        }
+
+        if (string.Equals(invocation.TargetMethod.Name, "GetBytes", StringComparison.Ordinal) &&
+            NamespaceEquals(invocation.TargetMethod.ContainingNamespace, "System.Text") &&
+            Unwrap(ResolveInitializer(invocation.Instance) ?? invocation.Instance) is IPropertyReferenceOperation encodingProperty &&
+            encodingProperty.Property.IsStatic &&
+            IsType(encodingProperty.Property.ContainingType, "Encoding", "System.Text") &&
+            TryGetResolvedConstant<string>(ArgumentAt(invocation.Arguments, 0)?.Value, out var text, depth + 1) &&
+            GetEncoding(encodingProperty.Property.Name) is { } encoding)
+        {
+            length = encoding.GetByteCount(text);
+            return true;
+        }
+
+        return invocation.TargetMethod.IsStatic &&
+            IsType(invocation.TargetMethod.ContainingType, "Convert", "System") &&
+            invocation.Arguments.Length == 1 &&
+            TryGetResolvedConstant<string>(invocation.Arguments[0].Value, out var encoded, depth + 1) &&
+            invocation.TargetMethod.Name switch
+            {
+                "FromBase64String" => TryGetBase64ByteLength(encoded, out length),
+                "FromHexString" => TryGetHexByteLength(encoded, out length),
+                _ => false,
+            };
+    }
+
+    /// <summary>
+    ///     Reads the length of a slice: <c lang="csharp">AsSpan</c> or <c lang="csharp">AsMemory</c> with a start, a
+    ///     length or a range, an instance <c lang="csharp">Slice</c>, or
+    ///     <c lang="csharp">RuntimeHelpers.GetSubArray</c>. A constant length argument decides the result even when
+    ///     the source length is unknown.
+    /// </summary>
+    private static bool TryGetSliceLength(IInvocationOperation invocation, out int length, int depth)
+    {
+        length = 0;
+        var method = invocation.TargetMethod;
+        var isSubArray = string.Equals(method.Name, "GetSubArray", StringComparison.Ordinal) &&
+            IsType(method.ContainingType, "RuntimeHelpers", "System.Runtime.CompilerServices");
+
+        if (!isSubArray &&
+            ((!string.Equals(method.Name, "Slice", StringComparison.Ordinal) &&
+              !string.Equals(method.Name, "AsSpan", StringComparison.Ordinal) &&
+              !string.Equals(method.Name, "AsMemory", StringComparison.Ordinal)) ||
+             !NamespaceEquals(method.ContainingNamespace, "System")))
+        {
+            return false;
+        }
+
+        // An extension method receives the source as its first argument, so the extra arguments follow it.
+        var source = invocation.Instance ?? ArgumentAt(invocation.Arguments, 0)?.Value;
+        var extraArguments = invocation.Instance is null ? invocation.Arguments.Length - 1 : invocation.Arguments.Length;
+
+        if (ArgumentNamed(invocation.Arguments, "range") is { } rangeArgument)
+        {
+            return Unwrap(rangeArgument.Value) is IRangeOperation range &&
+                TryGetRangeLength(source, range.LeftOperand, range.RightOperand, depth, out length);
+        }
+
+        if (ArgumentNamed(invocation.Arguments, nameof(length)) is { } lengthArgument)
+        {
+            if (TryGetResolvedConstant<int>(lengthArgument.Value, out var count, depth + 1) && count >= 0)
+            {
+                length = count;
+
+                return true;
+            }
+
+            return false;
+        }
+
+        if (extraArguments == 0)
+        {
+            return !isSubArray && TryGetConstantByteLength(source, out length, depth + 1);
+        }
+
+        var startArgument = ArgumentNamed(invocation.Arguments, "start") ?? ArgumentNamed(invocation.Arguments, "startIndex");
+        if (startArgument is null ||
+            !TryGetIndexValue(startArgument.Value, source, depth, out var start) ||
+            !TryGetConstantByteLength(source, out var total, depth + 1) ||
+            total < start)
+        {
+            return false;
+        }
+
+        length = total - start;
+
+        return true;
+    }
+
+    /// <summary>
+    ///     Reads the length of <c lang="csharp">source[left..right]</c>. An omitted left bound is 0. An omitted right
+    ///     bound, or an index from the end (<c lang="csharp">^4</c>), needs a source of known length.
+    /// </summary>
+    private static bool TryGetRangeLength(IOperation? source, IOperation? left, IOperation? right, int depth, out int length)
+    {
+        length = 0;
+        if (depth >= MaxResolveDepth)
+        {
+            return false;
+        }
+
+        var start = 0;
+        if (left is not null && !TryGetIndexValue(left, source, depth, out start))
+        {
+            return false;
+        }
+
+        int end;
+        if (right is null)
+        {
+            if (!TryGetConstantByteLength(source, out end, depth + 1))
+            {
+                return false;
+            }
+        }
+        else if (!TryGetIndexValue(right, source, depth, out end))
+        {
+            return false;
+        }
+
+        if (end < start)
+        {
+            return false;
+        }
+
+        length = end - start;
+
+        return true;
+    }
+
+    /// <summary>
+    ///     Reads a constant index, or an index from the end (<c lang="csharp">^4</c>) when the length of
+    ///     <paramref name="source" /> is known.
+    /// </summary>
+    private static bool TryGetIndexValue(IOperation index, IOperation? source, int depth, out int value)
+    {
+        value = 0;
+        var operation = Unwrap(index);
+        if (operation is IUnaryOperation { OperatorKind: UnaryOperatorKind.Hat } hat)
+        {
+            if (!TryGetResolvedConstant<int>(hat.Operand, out var fromEnd, depth + 1) ||
+                !TryGetConstantByteLength(source, out var total, depth + 1) ||
+                total < fromEnd)
+            {
+                return false;
+            }
+
+            value = total - fromEnd;
+
+            return true;
+        }
+
+        return TryGetResolvedConstant(operation, out value, depth + 1) && value >= 0;
+    }
+
+    private static bool TryGetBase64ByteLength(string value, out int length)
+    {
+        try
+        {
+            length = Convert.FromBase64String(value).Length;
+            return true;
+        }
+        catch (FormatException)
+        {
+            length = 0;
+            return false;
+        }
+    }
+
+    private static bool TryGetHexByteLength(string value, out int length)
+    {
+        length = 0;
+        if (value.Length % 2 != 0)
+        {
+            return false;
+        }
+
+        foreach (var character in value)
+        {
+            if (!Uri.IsHexDigit(character))
+            {
+                return false;
+            }
+        }
+
+        length = value.Length >> 1;
+        return true;
+    }
+
+    private static Encoding? GetEncoding(string propertyName) => propertyName switch
+    {
+        "UTF8" => Encoding.UTF8,
+        "ASCII" => Encoding.ASCII,
+        "Unicode" => Encoding.Unicode,
+        "Latin1" => Encoding.GetEncoding("iso-8859-1"),
+        "UTF32" => Encoding.UTF32,
+        _ => null,
+    };
+
+    /// <summary>
+    ///     Returns true when <paramref name="word" /> appears in <paramref name="name" /> as a whole word:
+    ///     bounded by the start or end, an underscore, a digit, or a camelCase change.
+    /// </summary>
+    public static bool ContainsWord(string name, string word)
+    {
+        var camelCase = false;
+        foreach (var character in name)
+        {
+            if (char.IsLower(character))
+            {
+                camelCase = true;
+                break;
+            }
+        }
+
+        var index = 0;
+        while ((index = name.IndexOf(word, index, StringComparison.OrdinalIgnoreCase)) >= 0)
+        {
+            var afterPosition = index + word.Length;
+            var before = index == 0 ? '\0' : name[index - 1];
+            var after = afterPosition >= name.Length ? '\0' : name[afterPosition];
+
+            var boundaryBefore = before == '\0' || before == '_' || char.IsDigit(before) || (camelCase && char.IsUpper(name[index]));
+            var boundaryAfter = after == '\0' || after == '_' || char.IsDigit(after) || (camelCase && char.IsUpper(after));
+
+            if (boundaryBefore && boundaryAfter)
+            {
+                return true;
+            }
+
+            index = afterPosition;
+        }
+
+        return false;
+    }
+
+    private static ILocalSymbol? GetReceivingLocal(IOperation creation)
+    {
+        var child = creation;
+        var current = creation.Parent;
+        while (current is IConversionOperation)
+        {
+            child = current;
+            current = current.Parent;
+        }
+
+        switch (current)
+        {
+            case IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declarator }:
+                return declarator.Symbol;
+            case ISimpleAssignmentOperation { Target: ILocalReferenceOperation target } assignment when assignment.Value == child:
+                return target.Local;
+            default:
+                return null;
+        }
+    }
+
+    private static bool TryGetDeconstructedPairs(
+        IDeconstructionAssignmentOperation assignment,
+        out ImmutableArray<IOperation> targets,
+        out ImmutableArray<IOperation> values)
+    {
+        if (assignment.Target is ITupleOperation target &&
+            Unwrap(assignment.Value) is ITupleOperation value &&
+            target.Elements.Length == value.Elements.Length)
+        {
+            targets = target.Elements;
+            values = value.Elements;
+
+            return true;
+        }
+
+        targets = values = default;
+
+        return false;
+    }
+
+    /// <summary>
+    ///     Returns true when a constructor or static constructor of the field's type writes the field. A
+    ///     <c lang="csharp">readonly</c> field then holds its initializer only until the constructor runs.
+    /// </summary>
+    private static bool IsAssignedInConstructor(IFieldSymbol field, Compilation compilation)
+    {
+        var cache = AssignedInConstructorCache.GetValue(
+            compilation,
+            static _ => new ConcurrentDictionary<IFieldSymbol, bool>(SymbolEqualityComparer.Default));
+
+        if (cache.TryGetValue(field, out var assigned))
+        {
+            return assigned;
+        }
+
+        assigned = ComputeIsAssignedInConstructor(field, compilation);
+        cache.TryAdd(field, assigned);
+
+        return assigned;
+    }
+
+    private static bool ComputeIsAssignedInConstructor(IFieldSymbol field, Compilation compilation)
+    {
+        foreach (var constructor in field.ContainingType.Constructors)
+        {
+            if (HasFieldWrite(constructor, field, compilation))
+            {
+                return true;
+            }
+        }
+
+        foreach (var member in field.ContainingType.GetMembers())
+        {
+            if (member is IPropertySymbol { SetMethod.IsInitOnly: true, SetMethod: { } setter } &&
+                HasFieldWrite(setter, field, compilation))
+            {
+                return true;
+            }
+        }
+
+        return false;
+    }
+
+    private static bool HasFieldWrite(IMethodSymbol method, IFieldSymbol field, Compilation compilation)
+    {
+        foreach (var reference in method.DeclaringSyntaxReferences)
+        {
+            var syntax = reference.GetSyntax();
+            if (!compilation.ContainsSyntaxTree(syntax.SyntaxTree) ||
+                compilation.GetSemanticModel(syntax.SyntaxTree).GetOperation(syntax) is not { } body)
+            {
+                continue;
+            }
+
+            foreach (var descendant in body.Descendants())
+            {
+                if (descendant is IFieldReferenceOperation other &&
+                    SymbolEqualityComparer.Default.Equals(other.Field, field) &&
+                    IsWrite(other))
+                {
+                    return true;
+                }
+            }
+        }
+
+        return false;
+    }
+
+    /// <summary>
+    ///     Returns true when no statement in the enclosing body assigns the local, passes it by <c lang="csharp">ref</c> or
+    ///     <c lang="csharp">out</c>, or increments it.
+    /// </summary>
+    private static bool IsNeverReassigned(ILocalReferenceOperation reference)
+    {
+        if (reference.SemanticModel is not { } model)
+        {
+            return ComputeIsNeverReassigned(reference);
+        }
+
+        var cache = NeverReassignedCache.GetValue(
+            model.Compilation,
+            static _ => new ConcurrentDictionary<ILocalSymbol, bool>(SymbolEqualityComparer.Default));
+
+        if (cache.TryGetValue(reference.Local, out var neverReassigned))
+        {
+            return neverReassigned;
+        }
+
+        neverReassigned = ComputeIsNeverReassigned(reference);
+        cache.TryAdd(reference.Local, neverReassigned);
+
+        return neverReassigned;
+    }
+
+    private static bool ComputeIsNeverReassigned(ILocalReferenceOperation reference)
+    {
+        var root = (IOperation)reference;
+        while (root.Parent is not null)
+        {
+            root = root.Parent;
+        }
+
+        foreach (var descendant in root.Descendants())
+        {
+            if (descendant is ILocalReferenceOperation other &&
+                SymbolEqualityComparer.Default.Equals(other.Local, reference.Local) &&
+                IsWrite(other))
+            {
+                return false;
+            }
+        }
+
+        return true;
+    }
+
+    private static bool IsWrite(IOperation reference) => reference.Parent switch
+    {
+        IAssignmentOperation assignment => assignment.Target == reference,
+        IIncrementOrDecrementOperation => true,
+        IArgumentOperation argument => argument.Parameter?.RefKind is RefKind.Ref or RefKind.Out,
+        ITupleOperation { Parent: IDeconstructionAssignmentOperation deconstruction } tuple => deconstruction.Target == tuple,
+        _ => false,
+    };
+
+    private static string? HashNameFromOid(string oid) => oid.Trim() switch
+    {
+        "1.3.14.3.2.26" => "SHA1",
+        "1.2.840.113549.2.5" => "MD5",
+        "1.3.36.3.2.1" => "RIPEMD160",
+        "2.16.840.1.101.3.4.2.8" => "SHA3_256",
+        "2.16.840.1.101.3.4.2.9" => "SHA3_384",
+        "2.16.840.1.101.3.4.2.10" => "SHA3_512",
+        _ => null,
+    };
+
+    private static bool TryGetStackAllocLength(IOperation operation, out int length)
+    {
+        length = 0;
+        if (operation.Syntax is ImplicitStackAllocArrayCreationExpressionSyntax implicitStackAlloc)
+        {
+            length = implicitStackAlloc.Initializer.Expressions.Count;
+            return true;
+        }
+
+        if (operation.Syntax is not StackAllocArrayCreationExpressionSyntax stackAlloc ||
+            stackAlloc.Type is not ArrayTypeSyntax { RankSpecifiers.Count: 1 } arrayType ||
+            arrayType.RankSpecifiers[0].Sizes.Count != 1)
+        {
+            return false;
+        }
+
+        var sizeExpression = arrayType.RankSpecifiers[0].Sizes[0];
+        if (sizeExpression is OmittedArraySizeExpressionSyntax)
+        {
+            if (stackAlloc.Initializer is null)
+            {
+                return false;
+            }
+
+            length = stackAlloc.Initializer.Expressions.Count;
+            return true;
+        }
+
+        var model = operation.SemanticModel;
+        if (model is null)
+        {
+            return false;
+        }
+
+        var constant = model.GetConstantValue(sizeExpression);
+        if (constant.HasValue && constant.Value is int count)
+        {
+            length = count;
+            return true;
+        }
+
+        return false;
+    }
+}
diff --git a/src/E128.Analyzers/Security/FipsTlsVersionAnalyzer.cs b/src/E128.Analyzers/Security/FipsTlsVersionAnalyzer.cs
new file mode 100644
index 0000000..f4f4b0f
--- /dev/null
+++ b/src/E128.Analyzers/Security/FipsTlsVersionAnalyzer.cs
@@ -0,0 +1,242 @@
+using System;
+using System.Collections.Immutable;
+using System.Globalization;
+using Microsoft.CodeAnalysis;
+using Microsoft.CodeAnalysis.Diagnostics;
+using Microsoft.CodeAnalysis.Operations;
+
+namespace E128.Analyzers.Security;
+
+/// <summary>
+///     E128146: Reports TLS configuration that allows protocols below 1.2: the SSL and TLS 1.0 and 1.1 members
+///     of <c lang="csharp">SslProtocols</c> and <c lang="csharp">SecurityProtocolType</c>, and the
+///     <c lang="csharp">DontEnableSchUseStrongCrypto</c> and <c lang="csharp">DontEnableSystemDefaultTlsVersions</c>
+///     AppContext switches.
+/// </summary>
+[DiagnosticAnalyzer(LanguageNames.CSharp)]
+public sealed class FipsTlsVersionAnalyzer : DiagnosticAnalyzer
+{
+    internal const string DiagnosticId = "E128146";
+
+    private const string StrongCryptoSwitch = "Switch.System.Net.DontEnableSchUseStrongCrypto";
+
+    private const string SystemDefaultSwitch = "Switch.System.Net.DontEnableSystemDefaultTlsVersions";
+
+    // Bits of SslProtocols and SecurityProtocolType below TLS 1.2: Ssl2 (12), Ssl3 (48), Tls (192) and Tls11 (768).
+    private const int WeakProtocolBits = 12 | 48 | 192 | 768;
+
+    // Members that enable a protocol below TLS 1.2. Tls12, Tls13, None and SystemDefault are allowed.
+    private static readonly ImmutableHashSet<string> ForbiddenMembers = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "Ssl2",
+        "Ssl3",
+        "Tls",
+        "Tls11",
+        "Default");
+
+    private static readonly DiagnosticDescriptor Rule = new(
+        DiagnosticId,
+        "Use TLS 1.2 or later",
+        "{0}",
+        "Security",
+        DiagnosticSeverity.Error,
+        true,
+        "SSL 2.0, SSL 3.0, TLS 1.0 and TLS 1.1 are not permitted. Use TLS 1.2 or 1.3, or SslProtocols.None " +
+        "to defer to the operating system configuration. The rule skips comparisons and mask removal.");
+
+    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];
+
+    public override void Initialize(AnalysisContext context)
+    {
+        if (context is null)
+        {
+            throw new ArgumentNullException(nameof(context));
+        }
+
+        context.ConfigureGeneratedCodeAnalysis(
+            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
+        context.EnableConcurrentExecution();
+
+        context.RegisterOperationAction(AnalyzeFieldReference, OperationKind.FieldReference);
+        context.RegisterOperationAction(AnalyzeNumericCast, OperationKind.Conversion);
+        context.RegisterOperationAction(AnalyzeNegation, OperationKind.Unary);
+        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
+    }
+
+    private static void AnalyzeFieldReference(OperationAnalysisContext context)
+    {
+        var fieldReference = (IFieldReferenceOperation)context.Operation;
+        var field = fieldReference.Field;
+
+        if (!ForbiddenMembers.Contains(field.Name))
+        {
+            return;
+        }
+
+        if (IsProtocolType(field.ContainingType) && !IsRead(fieldReference))
+        {
+            Report(
+                context,
+                fieldReference,
+                $"'{field.Name}' enables a TLS/SSL protocol below 1.2, which is not permitted; use Tls12 or Tls13");
+        }
+    }
+
+    /// <summary>
+    ///     Returns true when the protocol value is read, not selected: a comparison, a bit test, or a mask that
+    ///     removes the protocol.
+    /// </summary>
+    private static bool IsRead(IOperation operand) =>
+        FipsSymbolHelpers.IsComparison(operand) || IsMaskTest(operand) || IsMaskRemoval(operand);
+
+    /// <summary>
+    ///     Returns true for <c lang="csharp">x &amp; SslProtocols.Tls11</c> when the result feeds a comparison, an
+    ///     <c lang="csharp">is</c> pattern or <c lang="csharp">HasFlag</c>, such as
+    ///     <c lang="csharp">(x &amp; SslProtocols.Tls11) != 0</c>. The mask reads a bit and enables nothing.
+    /// </summary>
+    private static bool IsMaskTest(IOperation operand)
+    {
+        var parent = operand.Parent;
+        while (parent is IConversionOperation)
+        {
+            parent = parent.Parent;
+        }
+
+        if (parent is not IBinaryOperation { OperatorKind: BinaryOperatorKind.And } mask)
+        {
+            return false;
+        }
+
+        var outer = mask.Parent;
+        while (outer is IConversionOperation)
+        {
+            outer = outer.Parent;
+        }
+
+        return outer is IIsPatternOperation || FipsSymbolHelpers.IsComparison(mask);
+    }
+
+    /// <summary>
+    ///     Returns true for <c lang="csharp">~SslProtocols.Tls11</c> or <c lang="csharp">~(SslProtocols.Tls | SslProtocols.Tls11)</c> used as an
+    ///     <c lang="csharp">&amp;</c> or <c lang="csharp">&amp;=</c> operand, which removes the protocols from a mask instead of enabling them.
+    /// </summary>
+    private static bool IsMaskRemoval(IOperation operand)
+    {
+        var parent = operand.Parent;
+        while (parent is IConversionOperation or IBinaryOperation { OperatorKind: BinaryOperatorKind.Or })
+        {
+            parent = parent.Parent;
+        }
+
+        if (parent is not IUnaryOperation { OperatorKind: UnaryOperatorKind.BitwiseNegation } negation)
+        {
+            return false;
+        }
+
+        var outer = negation.Parent;
+        while (outer is IConversionOperation)
+        {
+            outer = outer.Parent;
+        }
+
+        return outer is IBinaryOperation { OperatorKind: BinaryOperatorKind.And } or ICompoundAssignmentOperation
+        {
+            OperatorKind: BinaryOperatorKind.And,
+        };
+    }
+
+    private static bool IsProtocolType(ITypeSymbol? type) =>
+        FipsSymbolHelpers.IsType(type, "SslProtocols", "System.Security.Authentication") ||
+        FipsSymbolHelpers.IsType(type, "SecurityProtocolType", "System.Net");
+
+    private static bool HasWeakBits(IOperation operation) =>
+        operation.ConstantValue is { HasValue: true, Value: { } value } &&
+        Convert.ToInt64(value, CultureInfo.InvariantCulture) is var bits &&
+        (bits & WeakProtocolBits) != 0;
+
+    /// <summary>Reports a numeric cast such as <c lang="csharp">(SslProtocols)192</c> that includes a weak protocol bit.</summary>
+    private static void AnalyzeNumericCast(OperationAnalysisContext context)
+    {
+        var conversion = (IConversionOperation)context.Operation;
+        if (conversion.IsImplicit ||
+            !IsProtocolType(conversion.Type) ||
+            conversion.Operand.Type?.TypeKind == TypeKind.Enum ||
+            !HasWeakBits(conversion) ||
+            IsRead(conversion))
+        {
+            return;
+        }
+
+        Report(
+            context,
+            conversion,
+            "A numeric value enables a TLS/SSL protocol below 1.2, which is not permitted; use Tls12 or Tls13");
+    }
+
+    /// <summary>
+    ///     Reports <c lang="csharp">~SslProtocols.None</c> and similar complements of an allowed member, which set every weak
+    ///     protocol bit. A complement used as an <c lang="csharp">&amp;</c> mask is not reported.
+    /// </summary>
+    private static void AnalyzeNegation(OperationAnalysisContext context)
+    {
+        var negation = (IUnaryOperation)context.Operation;
+        if (negation.OperatorKind != UnaryOperatorKind.BitwiseNegation ||
+            !IsProtocolType(negation.Type) ||
+            FipsSymbolHelpers.Unwrap(negation.Operand) is not IFieldReferenceOperation operand ||
+            ForbiddenMembers.Contains(operand.Field.Name) ||
+            !HasWeakBits(negation) ||
+            IsMaskRemoval(operand))
+        {
+            return;
+        }
+
+        Report(
+            context,
+            negation,
+            "The complement of a protocol enables every protocol below 1.2, which is not permitted; use Tls12 or Tls13");
+    }
+
+    private static void AnalyzeInvocation(OperationAnalysisContext context)
+    {
+        var invocation = (IInvocationOperation)context.Operation;
+        var method = invocation.TargetMethod;
+        if (!string.Equals(method.Name, "SetSwitch", StringComparison.Ordinal) ||
+            !FipsSymbolHelpers.IsType(method.ContainingType, "AppContext", "System"))
+        {
+            return;
+        }
+
+        // Resolve by parameter, because named arguments can reorder the call.
+        var switchName = FipsSymbolHelpers.ArgumentAt(invocation.Arguments, 0);
+        var isEnabled = FipsSymbolHelpers.ArgumentAt(invocation.Arguments, 1);
+        if (switchName is null || isEnabled is null)
+        {
+            return;
+        }
+
+        if (!FipsSymbolHelpers.TryGetResolvedConstant<string>(switchName.Value, out var name) ||
+            !FipsSymbolHelpers.TryGetResolvedConstant<bool>(isEnabled.Value, out var enabled) ||
+            !enabled)
+        {
+            return;
+        }
+
+        string? message = null;
+        if (string.Equals(name, StrongCryptoSwitch, StringComparison.Ordinal))
+        {
+            message = "Setting 'DontEnableSchUseStrongCrypto' to true disables strong crypto and re-enables weak TLS; remove this switch";
+        }
+        else if (string.Equals(name, SystemDefaultSwitch, StringComparison.Ordinal))
+        {
+            message = "Setting 'DontEnableSystemDefaultTlsVersions' to true stops the operating system from choosing the protocol and re-enables TLS 1.0 and 1.1; remove this switch";
+        }
+
+        if (message is not null)
+        {
+            Report(context, invocation, message);
+        }
+    }
+
+    private static void Report(OperationAnalysisContext context, IOperation operation, string message) =>
+        context.ReportDiagnostic(Diagnostic.Create(Rule, operation.Syntax.GetLocation(), message));
+}
diff --git a/src/E128.Analyzers/Security/FipsTypeClassifier.cs b/src/E128.Analyzers/Security/FipsTypeClassifier.cs
new file mode 100644
index 0000000..1e54c59
--- /dev/null
+++ b/src/E128.Analyzers/Security/FipsTypeClassifier.cs
@@ -0,0 +1,149 @@
+using System;
+using System.Collections.Immutable;
+using Microsoft.CodeAnalysis;
+
+namespace E128.Analyzers.Security;
+
+/// <summary>
+///     Classifies a cryptography type for E128071 and E128148. One type gets one verdict, so the two rules never
+///     report the same type. The verdict is cached per compilation.
+/// </summary>
+internal static class FipsTypeClassifier
+{
+    /// <summary>
+    ///     Static members that produce an instance without <see langword="new" />, such as
+    ///     <c lang="csharp">MD5.Create()</c> and the .NET 5 and later one-shot helpers
+    ///     (<c lang="csharp">MD5.HashData</c>). Both type rules inspect the declaring type of these members.
+    /// </summary>
+    public static readonly ImmutableHashSet<string> OneShotMembers = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "Create",
+        "HashData",
+        "TryHashData",
+        "HashDataAsync");
+
+    /// <summary>
+    ///     Fragments that name a cipher on their own. IDEA is also an ordinary word, so it needs a cryptographic
+    ///     context before it counts (see <see cref="HasCipherContext" />).
+    /// </summary>
+    private static readonly ImmutableArray<string> ForbiddenNameFragments = ImmutableArray.Create(
+        "Blowfish",
+        "RC4",
+        "Arcfour",
+        "CAST5",
+        "CAST6",
+        "IDEA",
+        "Skipjack");
+
+    /// <summary>
+    ///     Words that mark a type as a cipher implementation when they directly follow an ambiguous fragment and end
+    ///     the type name, as in <c lang="csharp">IdeaEngine</c>. <c lang="csharp">IdeaRankingAlgorithm</c> and
+    ///     <c lang="csharp">IdeaManagedService</c> do not match.
+    /// </summary>
+    private static readonly ImmutableArray<string> CipherRoleWords = ImmutableArray.Create(
+        "Engine",
+        "Cipher",
+        "Cng",
+        "Managed",
+        "CryptoServiceProvider",
+        "Algorithm",
+        "Transform",
+        "Encryptor",
+        "Decryptor",
+        "Cbc",
+        "Ecb");
+
+    /// <summary>Returns the cached verdict for <paramref name="type" /> in <paramref name="compilation" />.</summary>
+    public static TypeVerdict GetVerdict(Compilation compilation, ITypeSymbol type)
+    {
+        var cache = TypeVerdictCache.For(compilation);
+
+        if (!cache.TryGetValue(type, out var verdict))
+        {
+            verdict = Classify(type);
+            cache.TryAdd(type, verdict);
+        }
+
+        return verdict;
+    }
+
+    private static TypeVerdict Classify(ITypeSymbol type)
+    {
+        // A framework type outside System.Security.Cryptography is never a forbidden algorithm, so skip the
+        // string scans.
+        if (FipsSymbolHelpers.IsBclType(type) && !FipsSymbolHelpers.IsInCryptographyNamespaceTree(type))
+        {
+            return TypeVerdict.Allowed;
+        }
+
+        // The type itself, then each base type: a type that derives from a forbidden type inherits the algorithm.
+        for (var current = type; current is not null; current = current.BaseType)
+        {
+            if (FipsSymbolHelpers.IsCryptographyType(current, FipsForbiddenTypes.All))
+            {
+                return TypeVerdict.ForbiddenType;
+            }
+        }
+
+        foreach (var fragment in ForbiddenNameFragments)
+        {
+            if (FipsSymbolHelpers.ContainsWord(type.Name, fragment) &&
+                (!string.Equals(fragment, "IDEA", StringComparison.Ordinal) || HasCipherContext(type, fragment)))
+            {
+                return TypeVerdict.ForbiddenName;
+            }
+        }
+
+        return TypeVerdict.Allowed;
+    }
+
+    /// <summary>
+    ///     Decides whether a type whose name contains an ambiguous fragment (such as <c lang="csharp">Idea</c>) is a
+    ///     cipher. A cipher has a role word directly after the fragment that ends the name
+    ///     (<c lang="csharp">IdeaEngine</c>), derives from a cipher or symmetric algorithm base, implements a cipher
+    ///     interface, or is named exactly like the cipher inside a cryptographic namespace.
+    /// </summary>
+    private static bool HasCipherContext(ITypeSymbol type, string fragment)
+    {
+        foreach (var role in CipherRoleWords)
+        {
+            if (type.Name.EndsWith(fragment + role, StringComparison.OrdinalIgnoreCase))
+            {
+                return true;
+            }
+        }
+
+        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
+        {
+            if (baseType.Name.IndexOf("Cipher", StringComparison.OrdinalIgnoreCase) >= 0 ||
+                FipsSymbolHelpers.IsCryptographyType(baseType, "SymmetricAlgorithm"))
+            {
+                return true;
+            }
+        }
+
+        foreach (var contract in type.AllInterfaces)
+        {
+            if (contract.Name.IndexOf("Cipher", StringComparison.OrdinalIgnoreCase) >= 0)
+            {
+                return true;
+            }
+        }
+
+        if (!string.Equals(type.Name, fragment, StringComparison.OrdinalIgnoreCase))
+        {
+            return false;
+        }
+
+        for (var ns = type.ContainingNamespace; ns is not null && !ns.IsGlobalNamespace; ns = ns.ContainingNamespace)
+        {
+            if (ns.Name.IndexOf("crypt", StringComparison.OrdinalIgnoreCase) >= 0 ||
+                ns.Name.IndexOf("cipher", StringComparison.OrdinalIgnoreCase) >= 0)
+            {
+                return true;
+            }
+        }
+
+        return false;
+    }
+}
diff --git a/src/E128.Analyzers/Security/FipsUnapprovedHashAnalyzer.cs b/src/E128.Analyzers/Security/FipsUnapprovedHashAnalyzer.cs
index d5db949..63a48e8 100644
--- a/src/E128.Analyzers/Security/FipsUnapprovedHashAnalyzer.cs
+++ b/src/E128.Analyzers/Security/FipsUnapprovedHashAnalyzer.cs
@@ -1,37 +1,129 @@
 using System;
 using System.Collections.Immutable;
 using Microsoft.CodeAnalysis;
-using Microsoft.CodeAnalysis.CSharp;
-using Microsoft.CodeAnalysis.CSharp.Syntax;
 using Microsoft.CodeAnalysis.Diagnostics;
+using Microsoft.CodeAnalysis.Operations;
 
 namespace E128.Analyzers.Security;
 
+/// <summary>
+///     E128071: Reports algorithms and members that organizational cryptography policy blocks: MD5, SHA-1,
+///     RIPEMD-160, DES, RC2, Triple DES, the MAC types built on them, ChaCha20Poly1305, PasswordDeriveBytes and
+///     DSA signature generation. The rule covers the types, their static Create and HashData members, legacy
+///     factories that select a weak algorithm by default, factories that take a constant algorithm name, and
+///     any cryptography API that takes a forbidden <c lang="csharp">HashAlgorithmName</c>. Verification methods are skipped,
+///     so code can still check an existing signature.
+/// </summary>
 [DiagnosticAnalyzer(LanguageNames.CSharp)]
 public sealed class FipsUnapprovedHashAnalyzer : DiagnosticAnalyzer
 {
     internal const string DiagnosticId = "E128071";
 
+    private static readonly ImmutableHashSet<string> ForbiddenAlgorithmTypes = FipsForbiddenTypes.Algorithms;
+
+    /// <summary>
+    ///     Algorithm names that a factory, a hash-name argument or an XML-DSig and XML-Enc property resolves to a
+    ///     forbidden algorithm. The comparison ignores case, and a leading <c lang="csharp">System.Security.Cryptography.</c>
+    ///     is removed first.
+    /// </summary>
+    private static readonly ImmutableHashSet<string> ForbiddenAlgorithmNames =
+        ForbiddenAlgorithmTypes.Union(new[]
+        {
+            "SHA",
+            "SHA-1",
+            "RIPEMD",
+            "RIPEMD-160",
+            "3DES",
+            "Triple DES",
+            "HashAlgorithm",
+            "HMAC",
+            "KeyedHashAlgorithm",
+            "http://www.w3.org/2000/09/xmldsig#sha1",
+            "http://www.w3.org/2001/04/xmldsig-more#md5",
+            "http://www.w3.org/2001/04/xmldsig-more#ripemd160",
+            "http://www.w3.org/2000/09/xmldsig#hmac-sha1",
+            "http://www.w3.org/2001/04/xmldsig-more#hmac-md5",
+            "http://www.w3.org/2001/04/xmldsig-more#hmac-ripemd160",
+            "http://www.w3.org/2000/09/xmldsig#rsa-sha1",
+            "http://www.w3.org/2000/09/xmldsig#dsa-sha1",
+            "http://www.w3.org/2001/04/xmlenc#des-cbc",
+            "http://www.w3.org/2001/04/xmlenc#tripledes-cbc",
+            "http://www.w3.org/2001/04/xmlenc#kw-tripledes",
+            "http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p",
+            "http://www.w3.org/2001/04/xmlenc#rsa-1_5",
+        }).WithComparer(StringComparer.OrdinalIgnoreCase);
+
+    private const string XmlDsigNamespace = "System.Security.Cryptography.Xml";
+
+    private const string PkcsNamespace = "System.Security.Cryptography.Pkcs";
+
+    private const string Pkcs1Encryption = "RSA PKCS#1 v1.5 encryption (use RSAEncryptionPadding.OaepSHA256)";
+
+    private static readonly ImmutableHashSet<string> Pbkdf2Types = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "Rfc2898DeriveBytes",
+        "PbeParameters");
+
+    private static readonly ImmutableHashSet<string> OaepKeyExchangeTypes = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "RSAOAEPKeyExchangeFormatter",
+        "RSAOAEPKeyExchangeDeformatter");
+
+    private static readonly ImmutableHashSet<string> Pkcs1KeyExchangeTypes = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "RSAPKCS1KeyExchangeFormatter",
+        "RSAPKCS1KeyExchangeDeformatter");
+
+    private static readonly ImmutableHashSet<string> RsaEncryptionMembers = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "Encrypt",
+        "Decrypt",
+        "TryEncrypt",
+        "TryDecrypt");
+
+    private static readonly ImmutableHashSet<string> NamedFactoryMethods = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "Create",
+        "CreateFromName");
+
+    private static readonly ImmutableHashSet<string> DsaSigningMembers = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "CreateSignature",
+        "TryCreateSignature",
+        "SignData",
+        "TrySignData",
+        "SignHash");
+
+    // Legacy signing members that take the hash as a string, an object or a Type.
+    private static readonly ImmutableHashSet<string> LegacyHashNameMembers = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "SignData",
+        "SignHash",
+        "SetHashAlgorithm");
+
+    // Object identifiers for SHA-1, MD5 and RIPEMD-160.
+    private static readonly ImmutableHashSet<string> ForbiddenHashOids = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "1.3.14.3.2.26",
+        "1.2.840.113549.2.5",
+        "1.3.36.3.2.1");
+
+    private static readonly ImmutableHashSet<string> LegacyDefaultFactoryTypes = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "HashAlgorithm",
+        "HMAC",
+        "KeyedHashAlgorithm");
+
     private static readonly DiagnosticDescriptor Rule = new(
         DiagnosticId,
-        "Use a FIPS-approved hash algorithm",
-        "'{0}' is not FIPS 140-2 approved — use a SHA256/SHA384/SHA512-based equivalent",
+        "Use an approved cryptographic algorithm",
+        "'{0}' is blocked by the cryptography policy; use SHA-256, SHA-384 or SHA-512 for hashing and AES for encryption",
         "Security",
-        DiagnosticSeverity.Warning,
-        true);
-
-    private static readonly ImmutableHashSet<string> UnapprovedTypes = ImmutableHashSet.Create(
-        StringComparer.Ordinal,
-        "MD5",
-        "SHA1",
-        "RIPEMD160",
-        "DES",
-        "RC2",
-        "TripleDES",
-        "HMACMD5",
-        "HMACSHA1",
-        "HMACRIPEMD160",
-        "MACTripleDES");
+        DiagnosticSeverity.Error,
+        true,
+        "The policy blocks MD5, SHA-1, RIPEMD-160, DES, RC2, Triple DES, the MAC types built on them, " +
+        "ChaCha20Poly1305 and PasswordDeriveBytes. DSA signature generation is reported and DSA verification " +
+        "is allowed, because NIST FIPS 186-5 permits DSA only to verify existing signatures.");
 
     public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];
 
@@ -42,94 +134,443 @@ public sealed class FipsUnapprovedHashAnalyzer : DiagnosticAnalyzer
             throw new ArgumentNullException(nameof(context));
         }
 
-        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
+        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
         context.EnableConcurrentExecution();
 
-        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
-        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
-        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ImplicitObjectCreationExpression);
+        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
+        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
+        context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
+        context.RegisterOperationAction(AnalyzeMethodReference, OperationKind.MethodReference);
+        context.RegisterOperationAction(AnalyzeFieldReference, OperationKind.FieldReference);
+        context.RegisterOperationAction(
+            AnalyzeHashNameStore,
+            OperationKind.SimpleAssignment,
+            OperationKind.CoalesceAssignment,
+            OperationKind.DeconstructionAssignment,
+            OperationKind.FieldInitializer,
+            OperationKind.PropertyInitializer);
+        context.RegisterOperationAction(
+            AnalyzeAssignment,
+            OperationKind.SimpleAssignment,
+            OperationKind.CoalesceAssignment,
+            OperationKind.DeconstructionAssignment);
     }
 
-    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
+    /// <summary>
+    ///     Reports a constant forbidden XML-DSig or XML-Enc URI assigned to <c lang="csharp">SignedInfo.SignatureMethod</c>,
+    ///     <c lang="csharp">Reference.DigestMethod</c> or <c lang="csharp">EncryptionMethod.KeyAlgorithm</c>,
+    ///     and a forbidden hash object identifier assigned to <c lang="csharp">CmsSigner.DigestAlgorithm</c>.
+    /// </summary>
+    /// <remarks>
+    ///     The target member name decides whether the assignment can report. A deconstruction
+    ///     assignment always enters the walk, because its targets appear only after the split.
+    /// </remarks>
+    private static void AnalyzeAssignment(OperationAnalysisContext context)
     {
-        var invocation = (InvocationExpressionSyntax)context.Node;
+        var mayTargetAlgorithm = context.Operation switch
+        {
+            ISimpleAssignmentOperation assignment => IsAlgorithmPropertyTarget(assignment.Target),
+            ICoalesceAssignmentOperation assignment => IsAlgorithmPropertyTarget(assignment.Target),
+            IDeconstructionAssignmentOperation => true,
+            _ => false,
+        };
+
+        if (mayTargetAlgorithm)
+        {
+            FipsSymbolHelpers.ForEachAssignment(
+                context.Operation,
+                (target, value, syntax) => CheckXmlUriAssignment(context, target, value, syntax));
+        }
+    }
+
+    private static bool IsAlgorithmPropertyTarget(IOperation target) =>
+        FipsSymbolHelpers.IsCandidateAssignmentTarget(target, "SignatureMethod") ||
+        FipsSymbolHelpers.IsCandidateAssignmentTarget(target, "DigestMethod") ||
+        FipsSymbolHelpers.IsCandidateAssignmentTarget(target, "KeyAlgorithm") ||
+        FipsSymbolHelpers.IsCandidateAssignmentTarget(target, "DigestAlgorithm");
+
+    private static void CheckXmlUriAssignment(
+        OperationAnalysisContext context,
+        IOperation target,
+        IOperation value,
+        SyntaxNode syntax)
+    {
+        if (target is not IPropertyReferenceOperation { Property: { } property })
+        {
+            return;
+        }
+
+        if (string.Equals(property.Name, "DigestAlgorithm", StringComparison.Ordinal) &&
+            FipsSymbolHelpers.IsType(property.ContainingType, "CmsSigner", PkcsNamespace))
+        {
+            // CmsSigner.DigestAlgorithm = new Oid("1.3.14.3.2.26")
+            if (FipsSymbolHelpers.Unwrap(value) is IObjectCreationOperation oid &&
+                FipsSymbolHelpers.IsCryptographyType(oid.Type, "Oid") &&
+                FipsSymbolHelpers.TryGetConstant<string>(FipsSymbolHelpers.ArgumentAt(oid.Arguments, 0)?.Value, out var oidValue) &&
+                ForbiddenHashOids.Contains(oidValue.Trim()))
+            {
+                ReportForbidden(context, syntax, oidValue);
+            }
+
+            return;
+        }
 
-        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
+        if (!((string.Equals(property.Name, "SignatureMethod", StringComparison.Ordinal) &&
+               FipsSymbolHelpers.IsType(property.ContainingType, "SignedInfo", XmlDsigNamespace)) ||
+              (string.Equals(property.Name, "DigestMethod", StringComparison.Ordinal) &&
+               FipsSymbolHelpers.IsType(property.ContainingType, "Reference", XmlDsigNamespace)) ||
+              (string.Equals(property.Name, "KeyAlgorithm", StringComparison.Ordinal) &&
+               FipsSymbolHelpers.IsType(property.ContainingType, "EncryptionMethod", XmlDsigNamespace))))
         {
             return;
         }
 
-        var methodName = memberAccess.Name.Identifier.ValueText;
-        if (!string.Equals(methodName, "Create", StringComparison.Ordinal)
-            && !string.Equals(methodName, "HashData", StringComparison.Ordinal))
+        if (FipsSymbolHelpers.TryGetConstant<string>(value, out var uri) && IsForbiddenAlgorithmName(uri))
         {
+            ReportForbidden(context, syntax, uri);
+        }
+    }
+
+    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
+    {
+        var creation = (IObjectCreationOperation)context.Operation;
+        var syntax = creation.Syntax;
+
+        if (FipsSymbolHelpers.IsCryptographyType(creation.Type, "DSASignatureFormatter"))
+        {
+            ReportForbidden(context, syntax, "DSA signature generation (DSASignatureFormatter)");
             return;
         }
 
-        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken);
-        if (symbolInfo.Symbol is not IMethodSymbol method)
+        // RSAOAEPKeyExchangeFormatter and its deformatter always use OAEP with SHA-1.
+        if (FipsSymbolHelpers.IsCryptographyType(creation.Type, OaepKeyExchangeTypes))
         {
+            ReportForbidden(context, syntax, "SHA1 (RSA OAEP padding)");
             return;
         }
 
-        var typeName = method.ContainingType?.Name;
-        if (typeName is null || !UnapprovedTypes.Contains(typeName))
+        // RSAPKCS1KeyExchangeFormatter and its deformatter always use PKCS#1 v1.5 padding.
+        if (FipsSymbolHelpers.IsCryptographyType(creation.Type, Pkcs1KeyExchangeTypes))
         {
+            ReportForbidden(context, syntax, Pkcs1Encryption);
             return;
         }
 
-        var containingNamespace = method.ContainingType?.ContainingNamespace?.ToDisplayString();
-        if (!string.Equals(containingNamespace, "System.Security.Cryptography", StringComparison.Ordinal))
+        // new EncryptionMethod("http://www.w3.org/2001/04/xmlenc#tripledes-cbc")
+        if (FipsSymbolHelpers.IsType(creation.Type, "EncryptionMethod", XmlDsigNamespace) &&
+            FipsSymbolHelpers.TryGetConstant<string>(FipsSymbolHelpers.ArgumentAt(creation.Arguments, 0)?.Value, out var uri) &&
+            IsForbiddenAlgorithmName(uri))
         {
+            ReportForbidden(context, syntax, uri);
             return;
         }
 
-        context.ReportDiagnostic(Diagnostic.Create(
-            Rule,
-            invocation.GetLocation(),
-            $"{typeName}.{methodName}()"));
+        // PBKDF2 and PBE hash arguments belong to E128145.
+        if (!FipsSymbolHelpers.IsCryptographyType(creation.Type, Pbkdf2Types))
+        {
+            ReportHashNameArguments(context, creation.Constructor, creation.Arguments, syntax);
+        }
+
+        InspectType(context, creation.Type, syntax);
+    }
+
+    /// <summary>
+    ///     Reports <c lang="csharp">PbeEncryptionAlgorithm.TripleDes3KeyPkcs12</c>, the Triple DES password-based encryption of
+    ///     a PKCS#8 or PKCS#12 export. A comparison is not a selection.
+    /// </summary>
+    private static void AnalyzeFieldReference(OperationAnalysisContext context)
+    {
+        var reference = (IFieldReferenceOperation)context.Operation;
+        if (string.Equals(reference.Field.Name, "TripleDes3KeyPkcs12", StringComparison.Ordinal) &&
+            FipsSymbolHelpers.IsCryptographyType(reference.Field.ContainingType, "PbeEncryptionAlgorithm") &&
+            !FipsSymbolHelpers.IsComparison(reference))
+        {
+            ReportForbidden(context, reference.Syntax, "TripleDES (PbeEncryptionAlgorithm.TripleDes3KeyPkcs12)");
+        }
     }
 
-    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
+    /// <summary>
+    ///     Reports <c lang="csharp">RSAEncryptionPadding.OaepSHA1</c> everywhere, and
+    ///     <c lang="csharp">RSAEncryptionPadding.Pkcs1</c> when an RSA <c lang="csharp">Encrypt</c>, <c lang="csharp">Decrypt</c>,
+    ///     <c lang="csharp">TryEncrypt</c> or <c lang="csharp">TryDecrypt</c> call receives it as an argument.
+    ///     <c lang="csharp">RSASignaturePadding.Pkcs1</c> is a different type and stays allowed.
+    /// </summary>
+    private static void AnalyzePropertyReference(OperationAnalysisContext context)
     {
-        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
+        var reference = (IPropertyReferenceOperation)context.Operation;
+        var property = reference.Property;
 
-        var symbolInfo = context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken);
-        if (symbolInfo.Symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor } constructor)
+        if (!property.IsStatic || !FipsSymbolHelpers.IsCryptographyType(property.ContainingType, "RSAEncryptionPadding"))
         {
             return;
         }
 
-        var constructedTypeName = constructor.ContainingType?.Name;
-        if (constructedTypeName is null || !DerivesFromUnapprovedAlgorithm(constructor.ContainingType))
+        if (string.Equals(property.Name, "OaepSHA1", StringComparison.Ordinal))
+        {
+            ReportForbidden(context, reference.Syntax, "SHA1 (RSA OAEP padding)");
+        }
+        else if (string.Equals(property.Name, "Pkcs1", StringComparison.Ordinal) &&
+                 reference.Parent is IArgumentOperation { Parent: IInvocationOperation invocation } &&
+                 RsaEncryptionMembers.Contains(invocation.TargetMethod.Name) &&
+                 FipsSymbolHelpers.DerivesFromCryptographyType(invocation.TargetMethod.ContainingType, "RSA"))
+        {
+            ReportForbidden(context, reference.Syntax, Pkcs1Encryption);
+        }
+    }
+
+    /// <summary>
+    ///     Reports a method group that names a static factory or one-shot helper of a forbidden type, such as
+    ///     <c lang="csharp">Func&lt;MD5&gt; create = MD5.Create</c>.
+    /// </summary>
+    private static void AnalyzeMethodReference(OperationAnalysisContext context)
+    {
+        var reference = (IMethodReferenceOperation)context.Operation;
+        if (reference.Method.IsStatic && FipsTypeClassifier.OneShotMembers.Contains(reference.Method.Name))
+        {
+            InspectType(context, reference.Method.ContainingType, reference.Syntax);
+        }
+    }
+
+    private static void AnalyzeInvocation(OperationAnalysisContext context)
+    {
+        var invocation = (IInvocationOperation)context.Operation;
+        var method = invocation.TargetMethod;
+
+        // One diagnostic per call: a SHA-1 hash name on the same call would repeat it.
+        if (ReportDsaOrOaep(context, invocation) ||
+            ReportLegacyHashName(context, invocation))
         {
             return;
         }
 
-        context.ReportDiagnostic(Diagnostic.Create(
-            Rule,
-            creation.GetLocation(),
-            $"new {constructedTypeName}()"));
+        // Verification of an existing signature stays allowed, and PBKDF2 belongs to E128145.
+        if (!IsExemptFromHashNameCheck(method))
+        {
+            ReportHashNameArguments(context, method, invocation.Arguments, invocation.Syntax);
+        }
+
+        if (method.IsStatic)
+        {
+            AnalyzeStaticInvocation(context, invocation);
+        }
+    }
+
+    private static bool ReportDsaOrOaep(OperationAnalysisContext context, IInvocationOperation invocation)
+    {
+        var method = invocation.TargetMethod;
+        var syntax = invocation.Syntax;
+
+        if (DsaSigningMembers.Contains(method.Name) &&
+            FipsSymbolHelpers.DerivesFromCryptographyType(method.ContainingType, "DSA"))
+        {
+            ReportForbidden(context, syntax, $"DSA signature generation ({method.Name})");
+
+            return true;
+        }
+
+        // RSACryptoServiceProvider.Encrypt(data, fOAEP) and Decrypt: true selects OAEP with SHA-1, false selects
+        // PKCS#1 v1.5, and a flag that is not constant can select either.
+        if ((string.Equals(method.Name, "Encrypt", StringComparison.Ordinal) ||
+             string.Equals(method.Name, "Decrypt", StringComparison.Ordinal)) &&
+            FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "RSACryptoServiceProvider") &&
+            FipsSymbolHelpers.ArgumentNamed(invocation.Arguments, "fOAEP") is { } oaepFlag)
+        {
+            ReportForbidden(
+                context,
+                syntax,
+                oaepFlag.Value.ConstantValue switch
+                {
+                    { HasValue: true, Value: true } => "SHA1 (RSA OAEP padding)",
+                    { HasValue: true } => Pkcs1Encryption,
+                    _ => "SHA1 (RSA OAEP padding) or RSA PKCS#1 v1.5 encryption, when fOAEP is not constant (use RSAEncryptionPadding.OaepSHA256)",
+                });
+
+            return true;
+        }
+
+        return false;
+    }
+
+    private static bool ReportLegacyHashName(OperationAnalysisContext context, IInvocationOperation invocation)
+    {
+        var method = invocation.TargetMethod;
+        if (method.IsStatic ||
+            !LegacyHashNameMembers.Contains(method.Name) ||
+            !FipsSymbolHelpers.IsInCryptographyNamespace(method.ContainingType) ||
+            method.ContainingType.Name.IndexOf("Deformatter", StringComparison.Ordinal) >= 0 ||
+            FipsSymbolHelpers.IsCryptographyType(method.ContainingType, "DSASignatureFormatter") ||
+            !TryGetLegacyHashName(invocation.Arguments, out var legacyHash))
+        {
+            return false;
+        }
+
+        ReportForbidden(context, invocation.Syntax, legacyHash);
+
+        return true;
     }
 
     /// <summary>
-    ///     Walks the base-type chain so legacy concrete subclasses (e.g. <c lang="csharp">MD5CryptoServiceProvider</c>,
-    ///     <c lang="csharp">SHA1Managed</c>) are caught even though their own type name isn't in <see cref="UnapprovedTypes" />.
+    ///     Returns true for the methods whose hash-name arguments the rule does not check. Verification of an
+    ///     existing signature stays allowed, but only for a framework method: <c lang="csharp">RSA.VerifyData</c>
+    ///     qualifies and an application's <c lang="csharp">VerifyAndSign</c> does not. The PBKDF2 hash arguments of
+    ///     <c lang="csharp">Rfc2898DeriveBytes.Pbkdf2</c> and <c lang="csharp">KeyDerivation.Pbkdf2</c> belong to
+    ///     E128145, so a method that merely shares the name <c lang="csharp">Pbkdf2</c> is checked.
     /// </summary>
-    private static bool DerivesFromUnapprovedAlgorithm(ITypeSymbol? type)
+    private static bool IsExemptFromHashNameCheck(IMethodSymbol method)
+    {
+        var owner = method.ContainingType;
+
+        return string.Equals(method.Name, "Pbkdf2", StringComparison.Ordinal)
+            ? FipsSymbolHelpers.IsCryptographyType(owner, "Rfc2898DeriveBytes") ||
+                FipsSymbolHelpers.IsType(owner, "KeyDerivation", "Microsoft.AspNetCore.Cryptography.KeyDerivation")
+            : FipsSymbolHelpers.IsBclType(owner) &&
+                (method.Name.StartsWith("Verify", StringComparison.Ordinal) ||
+                 method.Name.StartsWith("TryVerify", StringComparison.Ordinal));
+    }
+
+    private static void AnalyzeStaticInvocation(OperationAnalysisContext context, IInvocationOperation invocation)
     {
-        for (var current = type; current is not null; current = current.BaseType)
+        var method = invocation.TargetMethod;
+        var syntax = invocation.Syntax;
+
+        // Static members called on a forbidden type without 'new', such as MD5.Create() and MD5.HashData().
+        if (FipsTypeClassifier.OneShotMembers.Contains(method.Name) && InspectType(context, method.ContainingType, syntax))
         {
-            if (UnapprovedTypes.Contains(current.Name)
-                && string.Equals(
-                    current.ContainingNamespace?.ToDisplayString(),
-                    "System.Security.Cryptography",
-                    StringComparison.Ordinal))
+            return;
+        }
+
+        if (!FipsSymbolHelpers.IsInCryptographyNamespace(method.ContainingType))
+        {
+            return;
+        }
+
+        // HashAlgorithm.Create() and friends select SHA-1 or HMAC-SHA1 by default.
+        if (string.Equals(method.Name, "Create", StringComparison.Ordinal) &&
+            method.Parameters.Length == 0 &&
+            LegacyDefaultFactoryTypes.Contains(method.ContainingType.Name))
+        {
+            var text = string.Equals(method.ContainingType.Name, "HashAlgorithm", StringComparison.Ordinal)
+                ? "HashAlgorithm.Create() (legacy factory that selects SHA-1 by default)"
+                : method.ContainingType.Name + ".Create() (legacy factory that selects HMAC-SHA1 by default)";
+            ReportForbidden(context, syntax, text);
+
+            return;
+        }
+
+        // Factories that resolve an algorithm from a name, such as HashAlgorithm.Create("MD5").
+        if (NamedFactoryMethods.Contains(method.Name) &&
+            method.Parameters.Length > 0 &&
+            method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
+            FipsSymbolHelpers.TryGetConstant<string>(FipsSymbolHelpers.ArgumentAt(invocation.Arguments, 0)?.Value, out var name) &&
+            IsForbiddenAlgorithmName(name))
+        {
+            ReportForbidden(context, syntax, name);
+        }
+    }
+
+    /// <summary>
+    ///     Reads a forbidden hash from a string, object or Type argument of a legacy signing member: a constant
+    ///     name (<c lang="csharp">"SHA1"</c>), an object identifier (<c lang="csharp">"1.3.14.3.2.26"</c>) or <c lang="csharp">typeof(SHA1)</c>.
+    /// </summary>
+    private static bool TryGetLegacyHashName(ImmutableArray<IArgumentOperation> arguments, out string name)
+    {
+        foreach (var argument in arguments)
+        {
+            var parameterType = argument.Parameter?.Type.SpecialType;
+            if (parameterType is not (SpecialType.System_String or SpecialType.System_Object))
             {
+                continue;
+            }
+
+            var value = FipsSymbolHelpers.Unwrap(argument.Value);
+
+            // CryptoConfig.MapNameToOID("SHA1") is the object identifier of the named algorithm.
+            if (value is IInvocationOperation { TargetMethod: { IsStatic: true, Name: "MapNameToOID" } mapper } map &&
+                FipsSymbolHelpers.IsCryptographyType(mapper.ContainingType, "CryptoConfig"))
+            {
+                value = FipsSymbolHelpers.Unwrap(FipsSymbolHelpers.ArgumentAt(map.Arguments, 0)?.Value);
+            }
+
+            if (FipsSymbolHelpers.TryGetConstant<string>(value, out var constantName) &&
+                (IsForbiddenAlgorithmName(constantName) || ForbiddenHashOids.Contains(constantName.Trim())))
+            {
+                name = constantName;
+
+                return true;
+            }
+
+            if (value is ITypeOfOperation typeOf &&
+                FipsSymbolHelpers.IsCryptographyType(typeOf.TypeOperand, FipsForbiddenTypes.All))
+            {
+                name = typeOf.TypeOperand.Name;
                 return true;
             }
         }
 
+        name = string.Empty;
         return false;
     }
+
+    private static void ReportHashNameArguments(
+        OperationAnalysisContext context,
+        IMethodSymbol? method,
+        ImmutableArray<IArgumentOperation> arguments,
+        SyntaxNode syntax) =>
+        FipsSymbolHelpers.ReportHashNameArguments(
+            method,
+            arguments,
+            syntax,
+            context,
+            static (_, name) => IsForbiddenAlgorithmName(name),
+            static (state, node, name) => ReportForbidden(state, node, name));
+
+    /// <summary>
+    ///     Reports a forbidden hash name stored in a mutable field, a property or a reassigned target, because a
+    ///     later read of that target cannot be followed.
+    /// </summary>
+    private static void AnalyzeHashNameStore(OperationAnalysisContext context) =>
+        FipsSymbolHelpers.ReportHashNameStores(
+            context.Operation,
+            context,
+            static (_, name) => IsForbiddenAlgorithmName(name),
+            static (state, node, name) => ReportForbidden(state, node, name));
+
+    private static void ReportForbidden(OperationAnalysisContext context, SyntaxNode syntax, string algorithm) =>
+        context.ReportDiagnostic(Diagnostic.Create(Rule, syntax.GetLocation(), algorithm));
+
+    /// <summary>
+    ///     Reports a forbidden type or a type that derives from one. Returns true when either type rule owns the
+    ///     type, so E128148 alone reports a cipher that matches by name.
+    /// </summary>
+    private static bool InspectType(OperationAnalysisContext context, ITypeSymbol? type, SyntaxNode syntax)
+    {
+        if (type is null)
+        {
+            return false;
+        }
+
+        var verdict = FipsTypeClassifier.GetVerdict(context.Compilation, type);
+        if (verdict == TypeVerdict.ForbiddenType)
+        {
+            ReportForbidden(context, syntax, FipsSymbolHelpers.FullName(type) ?? type.Name);
+        }
+
+        return verdict != TypeVerdict.Allowed;
+    }
+
+    private static bool IsForbiddenAlgorithmName(string name) =>
+        ForbiddenAlgorithmNames.Contains(NormalizeName(name));
+
+    private static string NormalizeName(string name)
+    {
+        name = name.Trim();
+
+        const string prefix = FipsSymbolHelpers.Cryptography + ".";
+        if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
+        {
+            name = name.Substring(prefix.Length);
+        }
+
+        return name;
+    }
 }
diff --git a/src/E128.Analyzers/Security/InsecureRandomInCryptoContextAnalyzer.cs b/src/E128.Analyzers/Security/InsecureRandomInCryptoContextAnalyzer.cs
index c837d5a..e350b9d 100644
--- a/src/E128.Analyzers/Security/InsecureRandomInCryptoContextAnalyzer.cs
+++ b/src/E128.Analyzers/Security/InsecureRandomInCryptoContextAnalyzer.cs
@@ -1,25 +1,98 @@
 using System;
+using System.Collections.Concurrent;
 using System.Collections.Immutable;
-using System.Linq;
+using System.Threading;
 using Microsoft.CodeAnalysis;
-using Microsoft.CodeAnalysis.CSharp;
 using Microsoft.CodeAnalysis.CSharp.Syntax;
 using Microsoft.CodeAnalysis.Diagnostics;
+using Microsoft.CodeAnalysis.Operations;
 
 namespace E128.Analyzers.Security;
 
+/// <summary>
+///     E128075: Reports <c lang="csharp">System.Random</c> and any class derived from it in two cases. First, a value from
+///     Random (a new instance, <c lang="csharp">Random.Shared</c> or a stored instance) flows into a variable, field,
+///     property or buffer with a security-sensitive name such as <c lang="csharp">token</c> or
+///     <c lang="csharp">salt</c>. Second, a new instance or <c lang="csharp">Random.Shared</c> appears in a file that
+///     references a type from <c lang="csharp">System.Security.Cryptography</c> or
+///     <c lang="csharp">Microsoft.AspNetCore.Cryptography</c>.
+/// </summary>
+/// <remarks>
+///     Both checks are heuristics. The naming check judges only the name that receives the value, so it can miss
+///     a sensitive value with an ordinary name and report an ordinary value with a sensitive name. The file check
+///     reports every Random creation in a cryptography file, including a jitter or a shuffle. It does not report a
+///     stored Random that the file did not create, and it does not look at other files. Each site produces one
+///     diagnostic, and the naming message wins over the file message.
+/// </remarks>
 [DiagnosticAnalyzer(LanguageNames.CSharp)]
 public sealed class InsecureRandomInCryptoContextAnalyzer : DiagnosticAnalyzer
 {
     internal const string DiagnosticId = "E128075";
 
+    private const string CryptoFileMessage =
+        "System.Random is not cryptographically secure and this file uses cryptography APIs; use RandomNumberGenerator for any security-sensitive value";
+
+    // Distinctive fragments matched as case-insensitive substrings, so an all-lowercase compound such as
+    // "authtoken" still matches. Each is long enough that it rarely sits inside an unrelated word.
+    private static readonly ImmutableArray<string> SensitiveSubstrings = ImmutableArray.Create(
+        "token",
+        "nonce",
+        "password",
+        "passwd",
+        "credential",
+        "apikey",
+        "privatekey");
+
+    // Fragments that collide with ordinary words (salt in basalt, secret in secretary, signature in a design
+    // name, key in monkey, session in possession) match only as whole words in camelCase or snake_case names. A
+    // bare "session" is absent on purpose: sessionCount and sessionTimeout are not secrets, while sessionId and
+    // session_id are.
+    private static readonly ImmutableArray<string> SensitiveWords = ImmutableArray.Create(
+        "secret",
+        "salt",
+        "challenge",
+        "signature",
+        "sessionid",
+        "session_id",
+        "cookie",
+        "authcode",
+        "verifier",
+        "entropy",
+        "iv",
+        "key",
+        "seed",
+        "mac",
+        "hmac",
+        "auth",
+        "sig",
+        "pin",
+        "pwd",
+        "otp");
+
+    // .NET 8 members that draw random values without a Next prefix.
+    private static readonly ImmutableHashSet<string> StoredRandomMembers = ImmutableHashSet.Create(
+        StringComparer.Ordinal,
+        "GetItems",
+        "GetHexString",
+        "GetString");
+
+    // The namespace trees whose types mark a file as one that uses cryptography.
+    private static readonly string[][] CryptographyNamespaces =
+    [
+        ["System", "Security", "Cryptography"],
+        ["Microsoft", "AspNetCore", "Cryptography"]
+    ];
+
     private static readonly DiagnosticDescriptor Rule = new(
         DiagnosticId,
         "Use RandomNumberGenerator instead of Random in security-adjacent code",
-        "Non-cryptographic Random detected in a file that uses System.Security.Cryptography — use RandomNumberGenerator instead",
+        "{0}",
         "Security",
-        DiagnosticSeverity.Warning,
-        true);
+        DiagnosticSeverity.Error,
+        true,
+        "System.Random is not a cryptographically secure generator. Generate keys, tokens, nonces, IVs and " +
+        "salts with RandomNumberGenerator. Both checks are heuristics, so the rule can report an ordinary " +
+        "value and can miss a sensitive value.");
 
     public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];
 
@@ -30,74 +103,273 @@ public sealed class InsecureRandomInCryptoContextAnalyzer : DiagnosticAnalyzer
             throw new ArgumentNullException(nameof(context));
         }
 
-        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
+        // A generated file can create Random for a security-sensitive value, and organization policy covers
+        // that file, so the rule reads and reports generated code.
+        context.ConfigureGeneratedCodeAnalysis(
+            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
         context.EnableConcurrentExecution();
 
-        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
-        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
+        context.RegisterCompilationStartAction(start =>
+        {
+            var cryptography = new CryptographyUse(start.Compilation);
+            start.RegisterOperationAction(operation => AnalyzeObjectCreation(operation, cryptography), OperationKind.ObjectCreation);
+            start.RegisterOperationAction(operation => AnalyzePropertyReference(operation, cryptography), OperationKind.PropertyReference);
+            start.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
+        });
     }
 
-    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
+    private static void AnalyzeObjectCreation(OperationAnalysisContext context, CryptographyUse cryptography)
     {
-        var creation = (ObjectCreationExpressionSyntax)context.Node;
+        var creation = (IObjectCreationOperation)context.Operation;
+        if (IsRandomType(creation.Type))
+        {
+            ReportSite(context, creation, cryptography);
+        }
+    }
 
-        var typeInfo = context.SemanticModel.GetTypeInfo(creation, context.CancellationToken);
-        if (!IsSystemRandom(typeInfo.Type))
+    /// <summary>Returns true when the type is <c lang="csharp">System.Random</c> or derives from it.</summary>
+    private static bool IsRandomType(ITypeSymbol? type)
+    {
+        for (var current = type; current is not null; current = current.BaseType)
         {
-            return;
+            if (FipsSymbolHelpers.IsType(current, "Random", "System"))
+            {
+                return true;
+            }
+        }
+
+        return false;
+    }
+
+    private static void AnalyzePropertyReference(OperationAnalysisContext context, CryptographyUse cryptography)
+    {
+        var reference = (IPropertyReferenceOperation)context.Operation;
+        var property = reference.Property;
+
+        if (property.IsStatic &&
+            string.Equals(property.Name, "Shared", StringComparison.Ordinal) &&
+            IsRandomType(property.ContainingType))
+        {
+            ReportSite(context, reference, cryptography);
         }
+    }
 
-        if (!HasCryptoUsingDirective(context))
+    /// <summary>
+    ///     Reports a call on a stored <c lang="csharp">Random</c> (a field or local that the file created elsewhere) when the
+    ///     value or buffer has a sensitive name. A call on a new instance or <c lang="csharp">Random.Shared</c> belongs to the
+    ///     creation site, which this analyzer reports for every Random type it recognizes.
+    /// </summary>
+    private static void AnalyzeInvocation(OperationAnalysisContext context)
+    {
+        var invocation = (IInvocationOperation)context.Operation;
+        var method = invocation.TargetMethod;
+        if (method.IsStatic ||
+            !(method.Name.StartsWith("Next", StringComparison.Ordinal) || StoredRandomMembers.Contains(method.Name)) ||
+            !IsRandomType(method.ContainingType) ||
+            IsCreationSite(invocation.Instance))
         {
             return;
         }
 
-        context.ReportDiagnostic(Diagnostic.Create(Rule, creation.GetLocation()));
+        var name = GetSensitiveName(invocation);
+        if (name is not null)
+        {
+            context.ReportDiagnostic(Diagnostic.Create(
+                Rule,
+                invocation.Syntax.GetLocation(),
+                SensitiveMessage(name)));
+        }
     }
 
-    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
+    /// <summary>
+    ///     Returns true when the receiver is a site that <see cref="AnalyzeObjectCreation" /> or
+    ///     <see cref="AnalyzePropertyReference" /> reports itself. Any other receiver, such as a creation of a type
+    ///     parameter, is not a site, so the call must report on its own.
+    /// </summary>
+    private static bool IsCreationSite(IOperation? instance)
     {
-        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
+        return FipsSymbolHelpers.Unwrap(instance) switch
+        {
+            IObjectCreationOperation creation => IsRandomType(creation.Type),
+            IPropertyReferenceOperation { Property: { IsStatic: true, Name: "Shared" } property } => IsRandomType(property.ContainingType),
+            _ => false
+        };
+    }
 
-        if (!string.Equals(memberAccess.Name.Identifier.ValueText, "Shared", StringComparison.Ordinal))
+    private static void ReportSite(
+        OperationAnalysisContext context,
+        IOperation site,
+        CryptographyUse cryptography)
+    {
+        string? message = null;
+        var name = GetSensitiveName(site);
+        if (name is not null)
         {
-            return;
+            message = SensitiveMessage(name);
+        }
+        else if (context.Operation.SemanticModel is { } model &&
+                 cryptography.IsReferencedBy(model, site.Syntax.SyntaxTree, context.CancellationToken))
+        {
+            message = CryptoFileMessage;
         }
 
-        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken);
-        if (symbolInfo.Symbol is not IPropertySymbol property)
+        if (message is null)
         {
             return;
         }
 
-        if (!IsSystemRandom(property.ContainingType))
+        context.ReportDiagnostic(Diagnostic.Create(Rule, site.Syntax.GetLocation(), message));
+    }
+
+    private static string SensitiveMessage(string name)
+    {
+        return $"'{name}' appears to be security-sensitive but is sourced from System.Random; use RandomNumberGenerator instead";
+    }
+
+    /// <summary>
+    ///     Returns the sensitive name that receives the value of <paramref name="site" /> (a random call, a new
+    ///     instance or <c lang="csharp">Random.Shared</c>), or null. A <c lang="csharp">NextBytes</c> call is judged by its
+    ///     buffer, and any other use by the destination of its result.
+    /// </summary>
+    private static string? GetSensitiveName(IOperation site)
+    {
+        var name = FindNextBytesCall(site) is { } nextBytes
+            ? nextBytes.Arguments.Length > 0 ? GetBufferName(nextBytes.Arguments[0].Value) : null
+            : GetDestinationName(site);
+
+        return name is not null && IsSensitive(name) ? name : null;
+    }
+
+    /// <summary>
+    ///     Returns the <c lang="csharp">NextBytes</c> call that is <paramref name="site" /> or that
+    ///     <paramref name="site" /> receives.
+    /// </summary>
+    private static IInvocationOperation? FindNextBytesCall(IOperation site)
+    {
+        if (site is IInvocationOperation { TargetMethod.Name: "NextBytes" } call)
         {
-            return;
+            return call;
         }
 
-        if (!HasCryptoUsingDirective(context))
+        var parent = site.Parent;
+        while (parent is IConversionOperation)
         {
-            return;
+            parent = parent.Parent;
         }
 
-        context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation()));
+        return parent is IInvocationOperation { TargetMethod.Name: "NextBytes" } receiver &&
+               FipsSymbolHelpers.Unwrap(receiver.Instance) == site
+            ? receiver
+            : null;
     }
 
-    private static bool IsSystemRandom(ITypeSymbol? type)
+    private static string? GetBufferName(IOperation? buffer)
     {
-        return type is not null
-               && string.Equals(type.Name, "Random", StringComparison.Ordinal)
-               && string.Equals(type.ContainingNamespace?.ToDisplayString(), "System", StringComparison.Ordinal);
+        while (true)
+        {
+            switch (FipsSymbolHelpers.Unwrap(buffer))
+            {
+                case IInvocationOperation { Instance: { } instance }:
+                    buffer = instance;
+                    break;
+                case IInvocationOperation { TargetMethod.IsExtensionMethod: true, Arguments.Length: > 0 } extension:
+                    buffer = extension.Arguments[0].Value;
+                    break;
+                case { } operation:
+                    return TargetSymbolName(operation);
+                default:
+                    return null;
+            }
+        }
     }
 
-    private static bool HasCryptoUsingDirective(SyntaxNodeAnalysisContext context)
+    /// <summary>
+    ///     Walks up from the random value to the variable, field or property that receives it, so the naming
+    ///     heuristic can judge it. The walk passes through conversions, operators, string interpolation, member
+    ///     access on the value (<c lang="csharp">new Random().Next().ToString()</c>) and arguments of a call that returns a
+    ///     plain value (<c lang="csharp">BitConverter.GetBytes(random.Next())</c>). It stops at a lambda, a local
+    ///     function, a call that returns another type and any statement, so the search stays inside the expression.
+    /// </summary>
+    private static string? GetDestinationName(IOperation operation)
     {
-        var root = context.Node.SyntaxTree.GetRoot(context.CancellationToken);
+        var child = operation;
+        for (var current = operation.Parent; current is not null; child = current, current = current.Parent)
+        {
+            switch (current)
+            {
+                case IVariableDeclaratorOperation declarator:
+                    return declarator.Symbol.Name;
+                case IFieldInitializerOperation { InitializedFields.Length: > 0 } fieldInitializer:
+                    return fieldInitializer.InitializedFields[0].Name;
+                case IPropertyInitializerOperation { InitializedProperties.Length: > 0 } propertyInitializer:
+                    return propertyInitializer.InitializedProperties[0].Name;
+                case IAssignmentOperation assignment:
+                    return assignment.Value == child ? TargetSymbolName(assignment.Target) : null;
+                case IInvocationOperation invocation:
+                    if (invocation.Instance == child || (child is IArgumentOperation && IsValueLike(invocation.Type)))
+                    {
+                        continue;
+                    }
 
-        // A using can sit inside a block namespace as well as at the top of the file.
-        foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
+                    return null;
+                case IPropertyReferenceOperation property when property.Instance == child:
+                case IFieldReferenceOperation field when field.Instance == child:
+                case IArgumentOperation:
+                case IVariableInitializerOperation:
+                case IConversionOperation:
+                case IParenthesizedOperation:
+                case IBinaryOperation:
+                case IUnaryOperation:
+                case IConditionalOperation:
+                case ICoalesceOperation:
+                case IInterpolatedStringOperation:
+                case IInterpolatedStringContentOperation:
+                    continue;
+                default:
+                    return null;
+            }
+        }
+
+        return null;
+    }
+
+    /// <summary>A plain value: a primitive, a string, or an array of them.</summary>
+    private static bool IsValueLike(ITypeSymbol? type)
+    {
+        return type switch
         {
-            if (string.Equals(directive.Name?.ToString(), "System.Security.Cryptography", StringComparison.Ordinal))
+            null => false,
+            IArrayTypeSymbol array => IsValueLike(array.ElementType),
+            _ => type.SpecialType == SpecialType.System_String ||
+                 (type.IsValueType && type.SpecialType != SpecialType.None && type.SpecialType != SpecialType.System_Void)
+        };
+    }
+
+    private static string? TargetSymbolName(IOperation target)
+    {
+        return target switch
+        {
+            ILocalReferenceOperation local => local.Local.Name,
+            IFieldReferenceOperation field => field.Field.Name,
+            IPropertyReferenceOperation property => property.Property.Name,
+            IParameterReferenceOperation parameter => parameter.Parameter.Name,
+            _ => null
+        };
+    }
+
+    private static bool IsSensitive(string name)
+    {
+        foreach (var fragment in SensitiveSubstrings)
+        {
+            if (name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
+            {
+                return true;
+            }
+        }
+
+        foreach (var word in SensitiveWords)
+        {
+            if (FipsSymbolHelpers.ContainsWord(name, word))
             {
                 return true;
             }
@@ -105,4 +377,179 @@ public sealed class InsecureRandomInCryptoContextAnalyzer : DiagnosticAnalyzer
 
         return false;
     }
+
+    /// <summary>
+    ///     Answers, for one compilation, whether a file references a type or member of a cryptography namespace. The
+    ///     answer is cached per syntax tree and computed only when the file creates a Random.
+    /// </summary>
+    private sealed class CryptographyUse
+    {
+        private readonly ConcurrentDictionary<SyntaxTree, bool> _files = new();
+        private readonly Lazy<ImmutableHashSet<string>> _candidateNames;
+
+        public CryptographyUse(Compilation compilation)
+        {
+            _candidateNames = new Lazy<ImmutableHashSet<string>>(() => CollectCandidateNames(compilation));
+        }
+
+        /// <summary>
+        ///     Returns true when an identifier in the file resolves to a type or member of a cryptography namespace.
+        ///     Only a known type, namespace, or member name, plus <c lang="csharp">var</c>, is bound, because every other
+        ///     identifier cannot resolve to one. A concurrent scan of the same file computes the same answer and the
+        ///     first stored answer wins.
+        /// </summary>
+        public bool IsReferencedBy(SemanticModel model, SyntaxTree tree, CancellationToken cancellationToken)
+        {
+            if (_files.TryGetValue(tree, out var cached))
+            {
+                return cached;
+            }
+
+            // A canceled scan throws before it stores a result, so a partial answer is never cached.
+            var found = Scan(model, tree, cancellationToken);
+            _files.TryAdd(tree, found);
+
+            return found;
+        }
+
+        private bool Scan(SemanticModel model, SyntaxTree tree, CancellationToken cancellationToken)
+        {
+            var names = _candidateNames.Value;
+            if (names.IsEmpty)
+            {
+                return false;
+            }
+
+            foreach (var node in tree.GetRoot(cancellationToken).DescendantNodes())
+            {
+                cancellationToken.ThrowIfCancellationRequested();
+
+                if (node is not SimpleNameSyntax name)
+                {
+                    continue;
+                }
+
+                // A 'var' declaration takes the type of its initializer, so it can name a cryptography type
+                // that the file never spells out.
+                var text = name.Identifier.ValueText;
+                if (!string.Equals(text, "var", StringComparison.Ordinal) && !names.Contains(text))
+                {
+                    continue;
+                }
+
+                var symbol = model.GetSymbolInfo(name, cancellationToken).Symbol;
+                var type = symbol as ITypeSymbol ?? symbol?.ContainingType;
+                if (type is not null && IsCryptographyNamespace(type))
+                {
+                    return true;
+                }
+            }
+
+            return false;
+        }
+
+        /// <summary>
+        ///     Collects the namespace, type, and member names of the cryptography namespace trees that the compilation
+        ///     can see, from its references and its own source. Names include referenced and source symbols. A name for
+        ///     an attribute class also appears without the <c lang="csharp">Attribute</c> suffix, because source can omit
+        ///     the suffix.
+        /// </summary>
+        private static ImmutableHashSet<string> CollectCandidateNames(Compilation compilation)
+        {
+            var names = ImmutableHashSet.CreateBuilder(StringComparer.Ordinal);
+            AddAssemblyNames(compilation.Assembly.GlobalNamespace, names);
+            foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
+            {
+                AddAssemblyNames(assembly.GlobalNamespace, names);
+            }
+
+            return names.ToImmutable();
+        }
+
+        private static void AddAssemblyNames(INamespaceSymbol root, ImmutableHashSet<string>.Builder names)
+        {
+            foreach (var path in CryptographyNamespaces)
+            {
+                var ns = root;
+                foreach (var part in path)
+                {
+                    ns = FindChild(ns, part);
+                    if (ns is null)
+                    {
+                        break;
+                    }
+                }
+
+                if (ns is not null)
+                {
+                    AddNamespaceNames(ns, names);
+                }
+            }
+        }
+
+        private static INamespaceSymbol? FindChild(INamespaceSymbol parent, string name)
+        {
+            foreach (var child in parent.GetNamespaceMembers())
+            {
+                if (string.Equals(child.Name, name, StringComparison.Ordinal))
+                {
+                    return child;
+                }
+            }
+
+            return null;
+        }
+
+        private static void AddNamespaceNames(INamespaceSymbol ns, ImmutableHashSet<string>.Builder names)
+        {
+            names.Add(ns.Name);
+            foreach (var type in ns.GetTypeMembers())
+            {
+                AddTypeNames(type, names);
+            }
+
+            foreach (var child in ns.GetNamespaceMembers())
+            {
+                AddNamespaceNames(child, names);
+            }
+        }
+
+        private static void AddTypeNames(INamedTypeSymbol type, ImmutableHashSet<string>.Builder names)
+        {
+            names.Add(type.Name);
+            if (type.Name.EndsWith("Attribute", StringComparison.Ordinal) && type.Name.Length > "Attribute".Length)
+            {
+                names.Add(type.Name.Substring(0, type.Name.Length - "Attribute".Length));
+            }
+
+            // A member reached through a value from another file can mark this file without spelling its type.
+            foreach (var member in type.GetMembers())
+            {
+                names.Add(member.Name);
+            }
+
+            foreach (var nested in type.GetTypeMembers())
+            {
+                AddTypeNames(nested, names);
+            }
+        }
+
+        private static bool IsCryptographyNamespace(ITypeSymbol type)
+        {
+            if (FipsSymbolHelpers.IsInCryptographyNamespaceTree(type))
+            {
+                return true;
+            }
+
+            for (var ns = type.ContainingNamespace; ns is not null && !ns.IsGlobalNamespace; ns = ns.ContainingNamespace)
+            {
+                if (FipsSymbolHelpers.NamespaceEquals(ns, "Microsoft.AspNetCore.Cryptography"))
+                {
+                    return true;
+                }
+            }
+
+            return false;
+        }
+    }
 }
diff --git a/src/E128.Analyzers/Security/TypeVerdict.cs b/src/E128.Analyzers/Security/TypeVerdict.cs
new file mode 100644
index 0000000..cc9e86f
--- /dev/null
+++ b/src/E128.Analyzers/Security/TypeVerdict.cs
@@ -0,0 +1,12 @@
+namespace E128.Analyzers.Security;
+
+/// <summary>
+///     The result of classifying a type. The diagnostic text depends on the reporting rule, so the cache stores
+///     only which rule owns the type.
+/// </summary>
+internal enum TypeVerdict
+{
+    Allowed = 0,
+    ForbiddenName = 1,
+    ForbiddenType = 2,
+}
diff --git a/src/E128.Analyzers/Security/TypeVerdictCache.cs b/src/E128.Analyzers/Security/TypeVerdictCache.cs
new file mode 100644
index 0000000..ce9a63c
--- /dev/null
+++ b/src/E128.Analyzers/Security/TypeVerdictCache.cs
@@ -0,0 +1,17 @@
+using System.Collections.Concurrent;
+using System.Runtime.CompilerServices;
+using Microsoft.CodeAnalysis;
+
+namespace E128.Analyzers.Security;
+
+/// <summary>
+///     Holds the per-compilation verdict cache outside the analyzer type, which <c lang="csharp">RS1008</c> forbids.
+/// </summary>
+internal static class TypeVerdictCache
+{
+    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<ITypeSymbol, TypeVerdict>> Cache =
+        new();
+
+    public static ConcurrentDictionary<ITypeSymbol, TypeVerdict> For(Compilation compilation) =>
+        Cache.GetValue(compilation, static _ => new ConcurrentDictionary<ITypeSymbol, TypeVerdict>(SymbolEqualityComparer.Default));
+}
diff --git a/tests/E128.Analyzers.Tests/FipsAesGcmTagSizeAnalyzerTests.cs b/tests/E128.Analyzers.Tests/FipsAesGcmTagSizeAnalyzerTests.cs
new file mode 100644
index 0000000..da29862
--- /dev/null
+++ b/tests/E128.Analyzers.Tests/FipsAesGcmTagSizeAnalyzerTests.cs
@@ -0,0 +1,85 @@
+using System.Threading.Tasks;
+using E128.Analyzers.Security;
+using Microsoft.CodeAnalysis.CSharp.Testing;
+using Microsoft.CodeAnalysis.Testing;
+using Xunit;
+
+namespace E128.Analyzers.Tests;
+
+public sealed class FipsAesGcmTagSizeAnalyzerTests
+{
+    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
+    {
+        var test = new CSharpAnalyzerTest<FipsAesGcmTagSizeAnalyzer, DefaultVerifier>
+        {
+            TestCode = code,
+            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
+        };
+        test.ExpectedDiagnostics.AddRange(expected);
+        return test.RunAsync();
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task ShortTagCtor_Reports_E128149()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   using var aes = {|E128149:new AesGcm(new byte[16], 12)|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task FullTagCtor_NoReport()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   using var aes = new AesGcm(new byte[16], 16);
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task ShortTagBuffer_Reports_E128149()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M(AesGcm aes)
+                               {
+                                   {|E128149:aes.Encrypt(new byte[12], new byte[1], new byte[1], new byte[12])|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task FullTagBuffer_NoReport()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M(AesGcm aes)
+                               {
+                                   aes.Encrypt(new byte[12], new byte[1], new byte[1], new byte[16]);
+                               }
+                           }
+                           """);
+    }
+}
diff --git a/tests/E128.Analyzers.Tests/FipsCipherModeAnalyzerTests.cs b/tests/E128.Analyzers.Tests/FipsCipherModeAnalyzerTests.cs
new file mode 100644
index 0000000..90aeea2
--- /dev/null
+++ b/tests/E128.Analyzers.Tests/FipsCipherModeAnalyzerTests.cs
@@ -0,0 +1,87 @@
+using System.Threading.Tasks;
+using E128.Analyzers.Security;
+using Microsoft.CodeAnalysis.CSharp.Testing;
+using Microsoft.CodeAnalysis.Testing;
+using Xunit;
+
+namespace E128.Analyzers.Tests;
+
+public sealed class FipsCipherModeAnalyzerTests
+{
+    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
+    {
+        var test = new CSharpAnalyzerTest<FipsCipherModeAnalyzer, DefaultVerifier>
+        {
+            TestCode = code,
+            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
+        };
+        test.ExpectedDiagnostics.AddRange(expected);
+        return test.RunAsync();
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task EcbMember_Reports_E128144()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M(Aes aes)
+                               {
+                                   aes.Mode = {|E128144:CipherMode.ECB|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task EncryptEcb_Reports_E128144()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M(Aes aes)
+                               {
+                                   {|E128144:aes.EncryptEcb(new byte[16], new byte[16], PaddingMode.None)|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task CbcMember_NoReport()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M(Aes aes)
+                               {
+                                   aes.Mode = CipherMode.CBC;
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task ModeComparison_NoReport()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M(Aes aes)
+                               {
+                                   if (aes.Mode == CipherMode.ECB)
+                                   {
+                                   }
+                               }
+                           }
+                           """);
+    }
+}
diff --git a/tests/E128.Analyzers.Tests/FipsDeprecatedRngAnalyzerTests.cs b/tests/E128.Analyzers.Tests/FipsDeprecatedRngAnalyzerTests.cs
new file mode 100644
index 0000000..46507e6
--- /dev/null
+++ b/tests/E128.Analyzers.Tests/FipsDeprecatedRngAnalyzerTests.cs
@@ -0,0 +1,53 @@
+using System.Threading.Tasks;
+using E128.Analyzers.Security;
+using Microsoft.CodeAnalysis.CSharp.Testing;
+using Microsoft.CodeAnalysis.Testing;
+using Xunit;
+
+namespace E128.Analyzers.Tests;
+
+public sealed class FipsDeprecatedRngAnalyzerTests
+{
+    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
+    {
+        var test = new CSharpAnalyzerTest<FipsDeprecatedRngAnalyzer, DefaultVerifier>
+        {
+            TestCode = code,
+            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
+        };
+        test.ExpectedDiagnostics.AddRange(expected);
+        return test.RunAsync();
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task NewProvider_Reports_E128147()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   using var rng = {|E128147:new RNGCryptoServiceProvider()|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task StaticFactory_NoReport()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   using var rng = RandomNumberGenerator.Create();
+                               }
+                           }
+                           """);
+    }
+}
diff --git a/tests/E128.Analyzers.Tests/FipsDirectInstantiationAnalyzerTests.cs b/tests/E128.Analyzers.Tests/FipsDirectInstantiationAnalyzerTests.cs
new file mode 100644
index 0000000..deb9e5a
--- /dev/null
+++ b/tests/E128.Analyzers.Tests/FipsDirectInstantiationAnalyzerTests.cs
@@ -0,0 +1,69 @@
+using System.Threading.Tasks;
+using E128.Analyzers.Security;
+using Microsoft.CodeAnalysis.CSharp.Testing;
+using Microsoft.CodeAnalysis.Testing;
+using Xunit;
+
+namespace E128.Analyzers.Tests;
+
+public sealed class FipsDirectInstantiationAnalyzerTests
+{
+    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
+    {
+        var test = new CSharpAnalyzerTest<FipsDirectInstantiationAnalyzer, DefaultVerifier>
+        {
+            TestCode = code,
+            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
+        };
+        test.ExpectedDiagnostics.AddRange(expected);
+        return test.RunAsync();
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task ManagedType_Reports_E128142()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   using var h = {|E128142:new SHA256Managed()|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task RijndaelCreate_Reports_E128142()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   using var r = {|E128142:Rijndael.Create()|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task AbstractFactory_NoReport()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   using var h = SHA256.Create();
+                               }
+                           }
+                           """);
+    }
+}
diff --git a/tests/E128.Analyzers.Tests/FipsForbiddenAlgorithmNameAnalyzerTests.cs b/tests/E128.Analyzers.Tests/FipsForbiddenAlgorithmNameAnalyzerTests.cs
new file mode 100644
index 0000000..9ef294f
--- /dev/null
+++ b/tests/E128.Analyzers.Tests/FipsForbiddenAlgorithmNameAnalyzerTests.cs
@@ -0,0 +1,228 @@
+using System.Threading.Tasks;
+using E128.Analyzers.Security;
+using Microsoft.CodeAnalysis.CSharp.Testing;
+using Microsoft.CodeAnalysis.Testing;
+using Xunit;
+
+namespace E128.Analyzers.Tests;
+
+public sealed class FipsForbiddenAlgorithmNameAnalyzerTests
+{
+    private const string ThirdPartyCiphers = """
+                                              namespace Org.Ciphers
+                                              {
+                                                  class BlowfishEngine { }
+                                                  class RC4Engine { }
+                                                  class ArcfourEngine { }
+                                                  class Cast5Engine { }
+                                                  class Cast6Engine { }
+                                                  class IdeaEngine { }
+                                                  class SkipjackEngine { }
+                                                  class Ideal { }
+                                                  class Ideas { }
+                                                  class Broadcaster { }
+                                                  class Podcast5Service { }
+                                              }
+                                              """;
+
+    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
+    {
+        var test = new CSharpAnalyzerTest<FipsForbiddenAlgorithmNameAnalyzer, DefaultVerifier>
+        {
+            TestCode = code,
+            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
+        };
+        test.ExpectedDiagnostics.AddRange(expected);
+        return test.RunAsync();
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task BlowfishCreation_Reports_E128148()
+    {
+        return VerifyAsync("""
+                           class BlowfishEngine
+                           {
+                           }
+
+                           class C
+                           {
+                               void M()
+                               {
+                                   var cipher = {|E128148:new BlowfishEngine()|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Rc4Factory_Reports_E128148()
+    {
+        return VerifyAsync("""
+                           class RC4Engine
+                           {
+                               public static RC4Engine Create() => default;
+                           }
+
+                           class C
+                           {
+                               void M()
+                               {
+                                   var cipher = {|E128148:RC4Engine.Create()|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task IdeaEngine_Reports_E128148()
+    {
+        return VerifyAsync("""
+                           class IdeaEngine
+                           {
+                           }
+
+                           class C
+                           {
+                               void M()
+                               {
+                                   var cipher = {|E128148:new IdeaEngine()|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task PlainIdeaWord_NoReport()
+    {
+        return VerifyAsync("""
+                           class Idea
+                           {
+                           }
+
+                           class C
+                           {
+                               void M()
+                               {
+                                   var idea = new Idea();
+                               }
+                           }
+                           """);
+    }
+
+    [Theory]
+    [Trait("Category", "CI")]
+    [InlineData("RC4Engine")]
+    [InlineData("ArcfourEngine")]
+    [InlineData("Cast5Engine")]
+    [InlineData("Cast6Engine")]
+    [InlineData("SkipjackEngine")]
+    public Task NonApprovedCipherByName_Reports_E128148(string typeName)
+    {
+        var code = ThirdPartyCiphers + "\nclass C { void M() { var cipher = {|E128148:new Org.Ciphers." + typeName + "()|}; } }";
+
+        return VerifyAsync(code);
+    }
+
+    [Theory]
+    [Trait("Category", "CI")]
+    [InlineData("Ideal")]
+    [InlineData("Ideas")]
+    [InlineData("Broadcaster")]
+    [InlineData("Podcast5Service")]
+    public Task NameFragmentInsideWord_NoReport(string typeName)
+    {
+        var code = ThirdPartyCiphers + "\nclass C { void M() { var value = new Org.Ciphers." + typeName + "(); } }";
+
+        return VerifyAsync(code);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task IdeaRoleWordAtEnd_Reports_E128148()
+    {
+        return VerifyAsync("""
+                           namespace Org.Misc
+                           {
+                               class IdeaRankingAlgorithm { }
+                               class IdeaManagedService { }
+                               class IdeaEngine { }
+                               class IdeaCipher { }
+                               class IdeaManaged { }
+                               class IdeaCryptoServiceProvider { }
+                               class MyIdeaEngine { }
+                           }
+
+                           class C
+                           {
+                               void M()
+                               {
+                                   var a = new Org.Misc.IdeaRankingAlgorithm();
+                                   var b = new Org.Misc.IdeaManagedService();
+                                   var c = {|E128148:new Org.Misc.IdeaEngine()|};
+                                   var d = {|E128148:new Org.Misc.IdeaCipher()|};
+                                   var e = {|E128148:new Org.Misc.IdeaManaged()|};
+                                   var f = {|E128148:new Org.Misc.IdeaCryptoServiceProvider()|};
+                                   var g = {|E128148:new Org.Misc.MyIdeaEngine()|};
+                               }
+                           }
+                           """);
+    }
+
+    [Theory]
+    [Trait("Category", "CI")]
+    [InlineData("IdeaService")]
+    [InlineData("IdeaBoard")]
+    public Task OrdinaryIdeaNames_NoReport(string typeName)
+    {
+        var code = "namespace App.Planning { class " + typeName + " { } }\nclass C { void M() { var value = new App.Planning." + typeName + "(); } }";
+
+        return VerifyAsync(code);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task IdeaServiceInCryptographicNamespace_NoReport()
+    {
+        return VerifyAsync("""
+                           namespace Org.Ciphers { class IdeaService { } }
+
+                           class C
+                           {
+                               void M()
+                               {
+                                   var service = new Org.Ciphers.IdeaService();
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task CryptographicIdeaUses_Report_E128148()
+    {
+        return VerifyAsync("""
+                           namespace Org.Cryptography { class Idea { } }
+
+                           namespace Org.Misc
+                           {
+                               interface IBlockCipher { }
+                               class Idea : IBlockCipher { }
+                               class IdeaCipher { }
+                           }
+
+                           class C
+                           {
+                               void M()
+                               {
+                                   var a = {|E128148:new Org.Cryptography.Idea()|};
+                                   var b = {|E128148:new Org.Misc.Idea()|};
+                                   var c = {|E128148:new Org.Misc.IdeaCipher()|};
+                               }
+                           }
+                           """);
+    }
+}
diff --git a/tests/E128.Analyzers.Tests/FipsGeneratedCodeTests.cs b/tests/E128.Analyzers.Tests/FipsGeneratedCodeTests.cs
new file mode 100644
index 0000000..ce92403
--- /dev/null
+++ b/tests/E128.Analyzers.Tests/FipsGeneratedCodeTests.cs
@@ -0,0 +1,260 @@
+using System;
+using System.Collections.Generic;
+using System.Collections.Immutable;
+using System.Threading.Tasks;
+using E128.Analyzers.Security;
+using Microsoft.CodeAnalysis.CSharp.Testing;
+using Microsoft.CodeAnalysis.Diagnostics;
+using Microsoft.CodeAnalysis.Testing;
+using Xunit;
+
+namespace E128.Analyzers.Tests;
+
+public sealed class FipsGeneratedCodeTests
+{
+    private const string GeneratedFileName = "Widget.g.cs";
+
+    private const string HandWrittenFileName = "Widget.cs";
+
+    private const string AutoGeneratedHeader = "// <auto-generated/>\n";
+
+    private const string NoHeader = "";
+
+    /// <summary>
+    ///     Every FIPS cryptography analyzer in this repository, listed by name so a new rule is added here on purpose.
+    /// </summary>
+    private static readonly ImmutableArray<DiagnosticAnalyzer> AllAnalyzers =
+    [
+        new FipsUnapprovedHashAnalyzer(),
+        new InsecureRandomInCryptoContextAnalyzer(),
+        new FipsDirectInstantiationAnalyzer(),
+        new FipsRsaKeySizeAnalyzer(),
+        new FipsCipherModeAnalyzer(),
+        new FipsPbkdfIterationAnalyzer(),
+        new FipsTlsVersionAnalyzer(),
+        new FipsDeprecatedRngAnalyzer(),
+        new FipsForbiddenAlgorithmNameAnalyzer(),
+        new FipsAesGcmTagSizeAnalyzer(),
+        new FipsSha3Analyzer(),
+        new FipsHmacKeyLengthAnalyzer()
+    ];
+
+    [Theory]
+    [Trait("Category", "CI")]
+    [InlineData("E128071", GeneratedFileName, NoHeader)]
+    [InlineData("E128071", HandWrittenFileName, AutoGeneratedHeader)]
+    [InlineData("E128075", GeneratedFileName, NoHeader)]
+    [InlineData("E128075", HandWrittenFileName, AutoGeneratedHeader)]
+    [InlineData("E128142", GeneratedFileName, NoHeader)]
+    [InlineData("E128142", HandWrittenFileName, AutoGeneratedHeader)]
+    [InlineData("E128143", GeneratedFileName, NoHeader)]
+    [InlineData("E128143", HandWrittenFileName, AutoGeneratedHeader)]
+    [InlineData("E128144", GeneratedFileName, NoHeader)]
+    [InlineData("E128144", HandWrittenFileName, AutoGeneratedHeader)]
+    [InlineData("E128145", GeneratedFileName, NoHeader)]
+    [InlineData("E128145", HandWrittenFileName, AutoGeneratedHeader)]
+    [InlineData("E128146", GeneratedFileName, NoHeader)]
+    [InlineData("E128146", HandWrittenFileName, AutoGeneratedHeader)]
+    [InlineData("E128147", GeneratedFileName, NoHeader)]
+    [InlineData("E128147", HandWrittenFileName, AutoGeneratedHeader)]
+    [InlineData("E128148", GeneratedFileName, NoHeader)]
+    [InlineData("E128148", HandWrittenFileName, AutoGeneratedHeader)]
+    [InlineData("E128149", GeneratedFileName, NoHeader)]
+    [InlineData("E128149", HandWrittenFileName, AutoGeneratedHeader)]
+    [InlineData("E128150", GeneratedFileName, NoHeader)]
+    [InlineData("E128150", HandWrittenFileName, AutoGeneratedHeader)]
+    [InlineData("E128151", GeneratedFileName, NoHeader)]
+    [InlineData("E128151", HandWrittenFileName, AutoGeneratedHeader)]
+    public Task GeneratedCode_ReportsAtMarkedLocation(string id, string fileName, string header)
+    {
+        var code = header + RuleCase(id);
+
+        return VerifyFileAsync(fileName, code);
+    }
+
+    /// <summary>
+    ///     Returns one snippet per rule that reports exactly at the <c lang="csharp">{|E128NNN:...|}</c> span. Every
+    ///     analyzer runs over the snippet, so a second analyzer that reports on it fails the test.
+    /// </summary>
+    /// <exception cref="ArgumentOutOfRangeException">Thrown when the identifier has no snippet.</exception>
+    private static string RuleCase(string id) => id switch
+    {
+        "E128071" => UnapprovedHashCase(),
+        "E128075" => InsecureRandomCase(),
+        "E128142" => DirectInstantiationCase(),
+        "E128143" => RsaKeySizeCase(),
+        "E128144" => CipherModeCase(),
+        "E128145" => PbkdfIterationCase(),
+        "E128146" => TlsVersionCase(),
+        "E128147" => DeprecatedRngCase(),
+        "E128148" => ForbiddenAlgorithmNameCase(),
+        "E128149" => AesGcmTagSizeCase(),
+        "E128150" => Sha3Case(),
+        "E128151" => HmacKeyLengthCase(),
+        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "No generated-code case for this rule.")
+    };
+
+    private static string UnapprovedHashCase() => """
+                                                   using System.Security.Cryptography;
+
+                                                   class C
+                                                   {
+                                                       void M()
+                                                       {
+                                                           using var hash = {|E128071:MD5.Create()|};
+                                                       }
+                                                   }
+                                                   """;
+
+    private static string InsecureRandomCase() => """
+                                                   using System;
+
+                                                   class C
+                                                   {
+                                                       void M()
+                                                       {
+                                                           var token = {|E128075:new Random()|};
+                                                       }
+                                                   }
+                                                   """;
+
+    private static string DirectInstantiationCase() => """
+                                                        using System.Security.Cryptography;
+
+                                                        class C
+                                                        {
+                                                            void M()
+                                                            {
+                                                                var aes = {|E128142:new AesManaged()|};
+                                                            }
+                                                        }
+                                                        """;
+
+    private static string RsaKeySizeCase() => """
+                                              using System.Security.Cryptography;
+
+                                              class C
+                                              {
+                                                  void M()
+                                                  {
+                                                      var rsa = {|E128143:RSA.Create(1024)|};
+                                                  }
+                                              }
+                                              """;
+
+    private static string CipherModeCase() => """
+                                              using System.Security.Cryptography;
+
+                                              class C
+                                              {
+                                                  void M(Aes aes)
+                                                  {
+                                                      aes.Mode = {|E128144:CipherMode.ECB|};
+                                                  }
+                                              }
+                                              """;
+
+    private static string PbkdfIterationCase() => """
+                                                   using System.Security.Cryptography;
+
+                                                   class C
+                                                   {
+                                                       void M()
+                                                       {
+                                                           var parameters = {|E128145:new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)|};
+                                                       }
+                                                   }
+                                                   """;
+
+    private static string TlsVersionCase() => """
+                                              using System.Security.Authentication;
+
+                                              class C
+                                              {
+                                                  void M()
+                                                  {
+                                                      var protocols = {|E128146:SslProtocols.Tls11|};
+                                                  }
+                                              }
+                                              """;
+
+    private static string DeprecatedRngCase() => """
+                                                 using System.Security.Cryptography;
+
+                                                 class C
+                                                 {
+                                                     void M()
+                                                     {
+                                                         using var rng = {|E128147:new RNGCryptoServiceProvider()|};
+                                                     }
+                                                 }
+                                                 """;
+
+    private static string ForbiddenAlgorithmNameCase() => """
+                                                           namespace Org.Ciphers
+                                                           {
+                                                               class RC4Engine
+                                                               {
+                                                               }
+                                                           }
+
+                                                           class C
+                                                           {
+                                                               void M()
+                                                               {
+                                                                   var cipher = {|E128148:new Org.Ciphers.RC4Engine()|};
+                                                               }
+                                                           }
+                                                           """;
+
+    private static string AesGcmTagSizeCase() => """
+                                                 using System.Security.Cryptography;
+
+                                                 class C
+                                                 {
+                                                     void M(byte[] key)
+                                                     {
+                                                         var gcm = {|E128149:new AesGcm(key, 12)|};
+                                                     }
+                                                 }
+                                                 """;
+
+    private static string Sha3Case() => """
+                                        using System.Security.Cryptography;
+
+                                        class C
+                                        {
+                                            void M(byte[] data)
+                                            {
+                                                var hash = {|E128150:SHA3_256.HashData(data)|};
+                                            }
+                                        }
+                                        """;
+
+    private static string HmacKeyLengthCase() => """
+                                                 using System.Security.Cryptography;
+
+                                                 class C
+                                                 {
+                                                     void M()
+                                                     {
+                                                         using var hmac = {|E128151:new HMACSHA256(new byte[8])|};
+                                                     }
+                                                 }
+                                                 """;
+
+    private static Task VerifyFileAsync(string fileName, string code)
+    {
+        var test = new AllAnalyzersTest
+        {
+            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
+        };
+        test.TestState.Sources.Add((fileName, code));
+        return test.RunAsync();
+    }
+
+    private sealed class AllAnalyzersTest : CSharpAnalyzerTest<FipsUnapprovedHashAnalyzer, DefaultVerifier>
+    {
+        protected override IEnumerable<DiagnosticAnalyzer> GetDiagnosticAnalyzers() => AllAnalyzers;
+    }
+}
diff --git a/tests/E128.Analyzers.Tests/FipsHmacKeyLengthAnalyzerTests.cs b/tests/E128.Analyzers.Tests/FipsHmacKeyLengthAnalyzerTests.cs
new file mode 100644
index 0000000..dd4ec1b
--- /dev/null
+++ b/tests/E128.Analyzers.Tests/FipsHmacKeyLengthAnalyzerTests.cs
@@ -0,0 +1,119 @@
+using System.Threading.Tasks;
+using E128.Analyzers.Security;
+using Microsoft.CodeAnalysis.CSharp.Testing;
+using Microsoft.CodeAnalysis.Testing;
+using Xunit;
+
+namespace E128.Analyzers.Tests;
+
+public sealed class FipsHmacKeyLengthAnalyzerTests
+{
+    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
+    {
+        var test = new CSharpAnalyzerTest<FipsHmacKeyLengthAnalyzer, DefaultVerifier>
+        {
+            TestCode = code,
+            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
+        };
+        test.ExpectedDiagnostics.AddRange(expected);
+        return test.RunAsync();
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task ShortKeyCtor_Reports_E128151()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   using var hmac = {|E128151:new HMACSHA256(new byte[16])|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task FullKeyCtor_NoReport()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   using var hmac = new HMACSHA256(new byte[32]);
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task ShortStaticKey_Reports_E128151()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   var mac = {|E128151:HMACSHA512.HashData(new byte[16], new byte[1])|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task FullStaticKey_NoReport()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   var mac = HMACSHA512.HashData(new byte[64], new byte[1]);
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task EncodedStringKey_Reports_E128151()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           using System.Text;
+                           class C
+                           {
+                               void M()
+                               {
+                                   using var hmac = {|E128151:new HMACSHA256(Encoding.UTF8.GetBytes("short"))|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task HexStringKey_Reports_E128151()
+    {
+        return VerifyAsync("""
+                           using System;
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   using var hmac = {|E128151:new HMACSHA256(Convert.FromHexString("00112233445566778899AABBCCDDEEFF"))|};
+                               }
+                           }
+                           """);
+    }
+}
diff --git a/tests/E128.Analyzers.Tests/FipsPbkdfIterationAnalyzerTests.cs b/tests/E128.Analyzers.Tests/FipsPbkdfIterationAnalyzerTests.cs
new file mode 100644
index 0000000..1bf6660
--- /dev/null
+++ b/tests/E128.Analyzers.Tests/FipsPbkdfIterationAnalyzerTests.cs
@@ -0,0 +1,101 @@
+using System.Threading.Tasks;
+using E128.Analyzers.Security;
+using Microsoft.CodeAnalysis.CSharp.Testing;
+using Microsoft.CodeAnalysis.Testing;
+using Xunit;
+
+namespace E128.Analyzers.Tests;
+
+public sealed class FipsPbkdfIterationAnalyzerTests
+{
+    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
+    {
+        var test = new CSharpAnalyzerTest<FipsPbkdfIterationAnalyzer, DefaultVerifier>
+        {
+            TestCode = code,
+            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
+        };
+        test.ExpectedDiagnostics.AddRange(expected);
+        return test.RunAsync();
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task LowIterationCount_Reports_E128145()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   using var k = {|E128145:new Rfc2898DeriveBytes("pw", new byte[16], 1000, HashAlgorithmName.SHA256)|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task MinimumIterationCount_NoReport()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   using var k = new Rfc2898DeriveBytes("pw", new byte[16], 600_000, HashAlgorithmName.SHA256);
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task WeakPrf_Reports_E128145()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   using var k = {|E128145:new Rfc2898DeriveBytes("pw", new byte[16], 600_000, HashAlgorithmName.SHA1)|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task StaticPbkdf2LowCount_Reports_E128145()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   var k = {|E128145:Rfc2898DeriveBytes.Pbkdf2("pw", new byte[16], 1000, HashAlgorithmName.SHA256, 32)|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task StaticPbkdf2Strong_NoReport()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   var k = Rfc2898DeriveBytes.Pbkdf2("pw", new byte[16], 600_000, HashAlgorithmName.SHA256, 32);
+                               }
+                           }
+                           """);
+    }
+}
diff --git a/tests/E128.Analyzers.Tests/FipsRsaKeySizeAnalyzerTests.cs b/tests/E128.Analyzers.Tests/FipsRsaKeySizeAnalyzerTests.cs
new file mode 100644
index 0000000..112059c
--- /dev/null
+++ b/tests/E128.Analyzers.Tests/FipsRsaKeySizeAnalyzerTests.cs
@@ -0,0 +1,101 @@
+using System.Threading.Tasks;
+using E128.Analyzers.Security;
+using Microsoft.CodeAnalysis.CSharp.Testing;
+using Microsoft.CodeAnalysis.Testing;
+using Xunit;
+
+namespace E128.Analyzers.Tests;
+
+public sealed class FipsRsaKeySizeAnalyzerTests
+{
+    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
+    {
+        var test = new CSharpAnalyzerTest<FipsRsaKeySizeAnalyzer, DefaultVerifier>
+        {
+            TestCode = code,
+            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
+        };
+        test.ExpectedDiagnostics.AddRange(expected);
+        return test.RunAsync();
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task ShortRsaKey_Reports_E128143()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   using var rsa = {|E128143:RSA.Create(2048)|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task StrongRsaKey_NoReport()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   using var rsa = RSA.Create(3072);
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task ShortCurve_Reports_E128143()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   using var key = {|E128143:ECDsa.Create(ECCurve.NamedCurves.nistP256)|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task ApprovedCurve_NoReport()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   using var key = ECDsa.Create(ECCurve.NamedCurves.nistP384);
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task RsaCreatedByName_Reports_E128143()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   using var rsa = {|E128143:RSA.Create()|};
+                               }
+                           }
+                           """);
+    }
+}
diff --git a/tests/E128.Analyzers.Tests/FipsSha3AnalyzerTests.cs b/tests/E128.Analyzers.Tests/FipsSha3AnalyzerTests.cs
new file mode 100644
index 0000000..6b2132a
--- /dev/null
+++ b/tests/E128.Analyzers.Tests/FipsSha3AnalyzerTests.cs
@@ -0,0 +1,160 @@
+using System.Threading.Tasks;
+using E128.Analyzers.Security;
+using Microsoft.CodeAnalysis.CSharp.Testing;
+using Microsoft.CodeAnalysis.Testing;
+using Xunit;
+
+namespace E128.Analyzers.Tests;
+
+public sealed class FipsSha3AnalyzerTests
+{
+    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
+    {
+        var test = new CSharpAnalyzerTest<FipsSha3Analyzer, DefaultVerifier>
+        {
+            TestCode = code,
+            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
+        };
+        test.ExpectedDiagnostics.AddRange(expected);
+        return test.RunAsync();
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task UserDefinedProbeWithHashName_Reports_E128150()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+
+                           static class Probe
+                           {
+                               public static bool Contains(HashAlgorithmName left, HashAlgorithmName right) => false;
+                           }
+
+                           class C
+                           {
+                               void M()
+                               {
+                                   var found = {|E128150:Probe.Contains(HashAlgorithmName.SHA3_256, HashAlgorithmName.SHA256)|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task BclProbeWithHashName_NoDiagnostic()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+
+                           class C
+                           {
+                               void M()
+                               {
+                                   var same = HashAlgorithmName.SHA3_256.Equals(HashAlgorithmName.SHA256);
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task RepeatedHashNameInOneCall_ReportsOnce()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+
+                           class C
+                           {
+                               void Use(HashAlgorithmName first, HashAlgorithmName second)
+                               {
+                               }
+
+                               void M()
+                               {
+                                   {|E128150:Use(HashAlgorithmName.SHA3_256, HashAlgorithmName.SHA3_256)|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task ConditionalInReadonlyField_Reports_E128150()
+    {
+        return VerifyAsync("""
+                           using System;
+                           using System.Security.Cryptography;
+
+                           class C
+                           {
+                               private static readonly HashAlgorithmName Name;
+                               private static readonly HashAlgorithmName Other =
+                                   Environment.GetEnvironmentVariable("HASH") is null ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA3_256;
+
+                               static C()
+                               {
+                                   Name = HashAlgorithmName.SHA256;
+                               }
+
+                               void Use(HashAlgorithmName name)
+                               {
+                               }
+
+                               void M()
+                               {
+                                   {|E128150:Use(Other)|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Sha3HashData_Reports_E128150()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   var hash = {|E128150:SHA3_256.HashData(new byte[1])|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Sha256HashData_NoReport()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   var hash = SHA256.HashData(new byte[1]);
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task OaepSha3Padding_Reports_E128150()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   var padding = {|E128150:RSAEncryptionPadding.OaepSHA3_256|};
+                               }
+                           }
+                           """);
+    }
+}
diff --git a/tests/E128.Analyzers.Tests/FipsTlsVersionAnalyzerTests.cs b/tests/E128.Analyzers.Tests/FipsTlsVersionAnalyzerTests.cs
new file mode 100644
index 0000000..d1c1a96
--- /dev/null
+++ b/tests/E128.Analyzers.Tests/FipsTlsVersionAnalyzerTests.cs
@@ -0,0 +1,101 @@
+using System.Threading.Tasks;
+using E128.Analyzers.Security;
+using Microsoft.CodeAnalysis.CSharp.Testing;
+using Microsoft.CodeAnalysis.Testing;
+using Xunit;
+
+namespace E128.Analyzers.Tests;
+
+public sealed class FipsTlsVersionAnalyzerTests
+{
+    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
+    {
+        var test = new CSharpAnalyzerTest<FipsTlsVersionAnalyzer, DefaultVerifier>
+        {
+            TestCode = code,
+            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
+        };
+        test.ExpectedDiagnostics.AddRange(expected);
+        return test.RunAsync();
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Tls10Member_Reports_E128146()
+    {
+        return VerifyAsync("""
+                           using System.Security.Authentication;
+                           class C
+                           {
+                               void M()
+                               {
+                                   var p = {|E128146:SslProtocols.Tls|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Tls12Member_NoReport()
+    {
+        return VerifyAsync("""
+                           using System.Security.Authentication;
+                           class C
+                           {
+                               void M()
+                               {
+                                   var p = SslProtocols.Tls12;
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task NumericCast_Reports_E128146()
+    {
+        return VerifyAsync("""
+                           using System.Security.Authentication;
+                           class C
+                           {
+                               void M()
+                               {
+                                   var p = {|E128146:(SslProtocols)192|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task WeakCryptoSwitch_Reports_E128146()
+    {
+        return VerifyAsync("""
+                           using System;
+                           class C
+                           {
+                               void M()
+                               {
+                                   {|E128146:AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", true)|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task StrongCryptoSwitchFalse_NoReport()
+    {
+        return VerifyAsync("""
+                           using System;
+                           class C
+                           {
+                               void M()
+                               {
+                                   AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", false);
+                               }
+                           }
+                           """);
+    }
+}
diff --git a/tests/E128.Analyzers.Tests/FipsUnapprovedHashE128AnalyzerTests.cs b/tests/E128.Analyzers.Tests/FipsUnapprovedHashE128AnalyzerTests.cs
index cee0a25..2e49efe 100644
--- a/tests/E128.Analyzers.Tests/FipsUnapprovedHashE128AnalyzerTests.cs
+++ b/tests/E128.Analyzers.Tests/FipsUnapprovedHashE128AnalyzerTests.cs
@@ -341,4 +341,76 @@ public sealed class FipsUnapprovedHashE128AnalyzerTests
                            }
                            """);
     }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task RsaEncryptWithPkcs1Padding_Reports()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               byte[] M(RSA rsa)
+                               {
+                                   return rsa.Encrypt(new byte[1], {|E128071:RSAEncryptionPadding.Pkcs1|});
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task RsaSignWithPkcs1Padding_NoReport()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               byte[] M(RSA rsa, byte[] hash)
+                               {
+                                   return rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task CipherNamedTypeWithForbiddenFactoryName_NoReport()
+    {
+        // E128148 owns the type. Its verdict stops the factory-name analysis, so the call site gets one diagnostic.
+        return VerifyAsync("""
+                           namespace System.Security.Cryptography
+                           {
+                               class BlowfishEngine
+                               {
+                                   public static object Create(string name) => null;
+                               }
+                           }
+
+                           class C
+                           {
+                               void M()
+                               {
+                                   var cipher = System.Security.Cryptography.BlowfishEngine.Create("MD5");
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Pkcs1KeyExchangeFormatter_Reports()
+    {
+        return VerifyAsync("""
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M(RSA rsa)
+                               {
+                                   var formatter = {|E128071:new RSAPKCS1KeyExchangeFormatter(rsa)|};
+                               }
+                           }
+                           """);
+    }
 }
diff --git a/tests/E128.Analyzers.Tests/InsecureRandomInCryptoContextAnalyzerTests.cs b/tests/E128.Analyzers.Tests/InsecureRandomInCryptoContextAnalyzerTests.cs
index fe1d805..908c19b 100644
--- a/tests/E128.Analyzers.Tests/InsecureRandomInCryptoContextAnalyzerTests.cs
+++ b/tests/E128.Analyzers.Tests/InsecureRandomInCryptoContextAnalyzerTests.cs
@@ -19,6 +19,21 @@ public sealed class InsecureRandomInCryptoContextAnalyzerTests
         return test.RunAsync();
     }
 
+    private static Task VerifyAsync(string[] sources, params DiagnosticResult[] expected)
+    {
+        var test = new CSharpAnalyzerTest<InsecureRandomInCryptoContextAnalyzer, DefaultVerifier>
+        {
+            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
+        };
+        foreach (var source in sources)
+        {
+            test.TestState.Sources.Add(source);
+        }
+
+        test.ExpectedDiagnostics.AddRange(expected);
+        return test.RunAsync();
+    }
+
     [Fact]
     [Trait("Category", "CI")]
     public Task Reports_NewRandom_InCryptoContext()
@@ -30,6 +45,7 @@ public sealed class InsecureRandomInCryptoContextAnalyzerTests
                            {
                                void M()
                                {
+                                   var bytes = RandomNumberGenerator.GetBytes(1);
                                    var r = {|E128075:new Random()|};
                                }
                            }
@@ -45,7 +61,11 @@ public sealed class InsecureRandomInCryptoContextAnalyzerTests
                            using System.Security.Cryptography;
                            class C
                            {
-                               int M() => {|E128075:Random.Shared|}.Next();
+                               int M()
+                               {
+                                   var bytes = RandomNumberGenerator.GetBytes(1);
+                                   return {|E128075:Random.Shared|}.Next();
+                               }
                            }
                            """);
     }
@@ -94,6 +114,7 @@ public sealed class InsecureRandomInCryptoContextAnalyzerTests
                            {
                                void M()
                                {
+                                   var bytes = RandomNumberGenerator.GetBytes(1);
                                    var r = {|E128075:new Random()|};
                                    var n = {|E128075:Random.Shared|}.Next();
                                }
@@ -115,6 +136,7 @@ public sealed class InsecureRandomInCryptoContextAnalyzerTests
                                {
                                    void M()
                                    {
+                                       var bytes = RandomNumberGenerator.GetBytes(1);
                                        var r = {|E128075:new Random()|};
                                    }
                                }
@@ -139,6 +161,85 @@ public sealed class InsecureRandomInCryptoContextAnalyzerTests
                            """);
     }
 
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task NoReport_WhenCryptoNamespaceIsOnlyImported()
+    {
+        return VerifyAsync("""
+                           using System;
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M()
+                               {
+                                   var r = new Random();
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_DerivedRandomType_InCryptoContext()
+    {
+        return VerifyAsync("""
+                           using System;
+                           using System.Security.Cryptography;
+
+                           class MyRandom : Random
+                           {
+                           }
+
+                           class C
+                           {
+                               void M()
+                               {
+                                   var bytes = RandomNumberGenerator.GetBytes(1);
+                                   var r = {|E128075:new MyRandom()|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_SessionId_FromStoredRandom()
+    {
+        return VerifyAsync("""
+                           using System;
+                           class C
+                           {
+                               private readonly Random _random = new Random();
+
+                               int M()
+                               {
+                                   var sessionId = {|E128075:_random.Next()|};
+                                   return sessionId;
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task NoReport_WhenNameContainsOrdinaryWord()
+    {
+        return VerifyAsync("""
+                           using System;
+                           class C
+                           {
+                               private readonly Random _random = new Random();
+
+                               int M()
+                               {
+                                   var possessionCount = _random.Next();
+                                   var basaltWeight = _random.Next();
+                                   return possessionCount + basaltWeight;
+                               }
+                           }
+                           """);
+    }
+
     [Fact]
     [Trait("Category", "CI")]
     public Task Reports_NewRandom_WithSeed_InCryptoContext()
@@ -150,9 +251,617 @@ public sealed class InsecureRandomInCryptoContextAnalyzerTests
                            {
                                void M()
                                {
+                                   var bytes = RandomNumberGenerator.GetBytes(1);
                                    var r = {|E128075:new Random(42)|};
                                }
                            }
                            """);
     }
+
+    [Theory]
+    [Trait("Category", "CI")]
+    [InlineData("sessionKey")]
+    [InlineData("authToken")]
+    [InlineData("passwordSalt")]
+    [InlineData("nonceValue")]
+    [InlineData("apiKey")]
+    [InlineData("iv_bytes")]
+    [InlineData("IV_BYTES")]
+    [InlineData("SEED")]
+    [InlineData("otp")]
+    [InlineData("Seed")]
+    [InlineData("sessionId")]
+    [InlineData("session_id")]
+    [InlineData("SessionID")]
+    [InlineData("saltBytes")]
+    [InlineData("csrfToken")]
+    [InlineData("authtoken")]
+    [InlineData("csrfCookie")]
+    [InlineData("signatureBytes")]
+    [InlineData("challengeValue")]
+    [InlineData("secretKey")]
+    public Task Reports_SensitiveName_ForNewRandom(string name)
+    {
+        return VerifyAsync($$"""
+                            using System;
+                            class C
+                            {
+                                void M()
+                                {
+                                    var {{name}} = {|E128075:new Random()|};
+                                }
+                            }
+                            """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_SensitiveRandom_StoredInFieldPropertyAndAssignment()
+    {
+        return VerifyAsync("""
+                           using System;
+                           class C
+                           {
+                               private Random _secret = {|E128075:new Random()|};
+                               public Random Nonce { get; } = {|E128075:new Random()|};
+                               private Random token;
+
+                               void M()
+                               {
+                                   var sessionKey = {|E128075:new Random()|}.Next();
+                                   token = {|E128075:new Random()|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_RandomShared_AssignedToSensitiveLocal()
+    {
+        return VerifyAsync("""
+                           using System;
+                           class C
+                           {
+                               void M()
+                               {
+                                   var token = {|E128075:Random.Shared|};
+                               }
+                           }
+                           """);
+    }
+
+    [Theory]
+    [Trait("Category", "CI")]
+    [InlineData("driver")]
+    [InlineData("monkey")]
+    [InlineData("keyword")]
+    [InlineData("jitter")]
+    [InlineData("footprint")]
+    [InlineData("design")]
+    [InlineData("pinnacle")]
+    [InlineData("random")]
+    [InlineData("DRIVER")]
+    [InlineData("SHIPPING")]
+    [InlineData("MAPPING")]
+    [InlineData("possessionRoll")]
+    [InlineData("basalt")]
+    [InlineData("sessionCount")]
+    [InlineData("sessionTimeout")]
+    [InlineData("saltine")]
+    [InlineData("cookieless")]
+    [InlineData("secretary")]
+    [InlineData("signatured")]
+    [InlineData("topsecretdata")]
+    public Task NoReport_ForOrdinaryName(string name)
+    {
+        return VerifyAsync($$"""
+                            using System;
+                            class C
+                            {
+                                void M()
+                                {
+                                    var {{name}} = new Random();
+                                    var other = Random.Shared;
+                                }
+                            }
+                            """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task NoReport_ForUnrelatedRandomType()
+    {
+        return VerifyAsync("""
+                           namespace Other
+                           {
+                               class Random
+                               {
+                               }
+                           }
+
+                           class C
+                           {
+                               void M()
+                               {
+                                   var sessionKey = new Other.Random();
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_StoredRandom_ProducingSensitiveValues()
+    {
+        return VerifyAsync("""
+                           using System;
+                           class C
+                           {
+                               private readonly Random _rng = new Random();
+                               private double _cached;
+
+                               void M()
+                               {
+                                   var sessionKey = {|E128075:_rng.Next()|};
+                                   var authToken = {|E128075:_rng.NextInt64()|}.ToString();
+                                   var newSalt = BitConverter.GetBytes({|E128075:_rng.Next()|});
+                                   _cached = _rng.NextDouble();
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_StoredRandom_ProducingSensitiveField()
+    {
+        return VerifyAsync("""
+                           using System;
+                           class C
+                           {
+                               private readonly Random _rng = new Random();
+                               private int _nonce;
+
+                               void M()
+                               {
+                                   _nonce = {|E128075:_rng.Next()|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_NextBytes_IntoSensitiveBuffers()
+    {
+        return VerifyAsync("""
+                           using System;
+                           class C
+                           {
+                               private readonly Random _rng = new Random();
+
+                               void M(byte[] key, byte[] salt, byte[] nonce)
+                               {
+                                   {|E128075:_rng.NextBytes(key)|};
+                                   {|E128075:Random.Shared|}.NextBytes(salt);
+                                   {|E128075:new Random()|}.NextBytes(nonce);
+                                   Span<byte> iv = stackalloc byte[16];
+                                   {|E128075:_rng.NextBytes(iv)|};
+                                   {|E128075:_rng.NextBytes(key.AsSpan())|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task NoReport_ForStoredRandom_WithOrdinaryValues()
+    {
+        return VerifyAsync("""
+                           using System;
+                           class C
+                           {
+                               private readonly Random _rng = new Random();
+
+                               void M(byte[] data)
+                               {
+                                   var retryDelay = _rng.Next(100);
+                                   var jitter = Random.Shared.Next();
+                                   _rng.NextBytes(data);
+                                   Random.Shared.NextBytes(data);
+                                   var buffer = new byte[8];
+                                   _rng.NextBytes(buffer);
+                                   var order = BitConverter.GetBytes(_rng.Next());
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task NoReport_WhenRandomValueIsInsideNestedLambdaOrCall()
+    {
+        return VerifyAsync("""
+                           using System;
+                           class C
+                           {
+                               private readonly Random _rng = new Random();
+
+                               static int Retry(Func<int> action) => action();
+                               static object Build(Random random) => null;
+
+                               void M()
+                               {
+                                   var session = Retry(() => new Random().Next(10));
+                                   var sessionKeyHolder = Build(new Random());
+                                   var sessionRetry = Retry(() => _rng.Next(3));
+                                   var tokenCount = Retry(() => { var delay = new Random().Next(); return delay; });
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_RandomInCryptographyFile_OncePerSite()
+    {
+        return VerifyAsync("""
+                           using System;
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               void M(byte[] data)
+                               {
+                                   var hash = SHA256.HashData(data);
+                                   var delay = {|E128075:new Random()|}.Next(5);
+                                   var jitter = {|E128075:Random.Shared|}.Next();
+                                   var token = {|E128075:new Random()|};
+                                   var other = new Random[] { {|E128075:Random.Shared|} };
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_RandomInCryptographyFile_WhenAnotherTypeUsesCryptography()
+    {
+        return VerifyAsync("""
+                           using System;
+                           using System.Security.Cryptography;
+
+                           class Hasher
+                           {
+                               byte[] Hash(byte[] data) => SHA256.HashData(data);
+                           }
+
+                           class Jitter
+                           {
+                               int Next() => {|E128075:Random.Shared|}.Next(100);
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_QualifiedCryptographyReference_MarksTheFile()
+    {
+        return VerifyAsync("""
+                           class C
+                           {
+                               void M(byte[] data)
+                               {
+                                   var hash = System.Security.Cryptography.SHA256.HashData(data);
+                                   var random = {|E128075:new System.Random()|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_StoredRandomInCryptographyFile_OnlyAtCreation()
+    {
+        return VerifyAsync("""
+                           using System;
+                           using System.Security.Cryptography;
+                           class C
+                           {
+                               private readonly Random _rng = {|E128075:new Random()|};
+
+                               void M(byte[] data, Aes aes)
+                               {
+                                   var delay = _rng.Next(100);
+                                   _rng.NextBytes(data);
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task NoReport_ForSameNamedTypesOutsideCryptographyNamespace()
+    {
+        return VerifyAsync("""
+                           using System;
+
+                           class Aes
+                           {
+                           }
+
+                           class SHA256
+                           {
+                               public static byte[] HashData(byte[] data) => data;
+                           }
+
+                           class C
+                           {
+                               int M(byte[] data)
+                               {
+                                   SHA256.HashData(data);
+                                   return new Random().Next(10);
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_StoredRandomGetItemsHexString_ForSensitiveNames()
+    {
+        return VerifyAsync("""
+                           using System;
+                           class C
+                           {
+                               private readonly Random _rng = new Random();
+
+                               void M()
+                               {
+                                   var sessionKey = {|E128075:_rng.GetItems(new[] { 'a', 'b' }, 8)|};
+                                   var authToken = {|E128075:_rng.GetHexString(32)|};
+                                   var plain = _rng.GetHexString(4);
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_DerivedRandom_AssignedToSensitiveName()
+    {
+        return VerifyAsync("""
+                           using System;
+
+                           class AppRandom : Random
+                           {
+                           }
+
+                           class C
+                           {
+                               void M()
+                               {
+                                   var token = {|E128075:new AppRandom()|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_CallOnNewDerivedRandom_AtCreation()
+    {
+        return VerifyAsync("""
+                           using System;
+
+                           class AppRandom : Random
+                           {
+                           }
+
+                           class C
+                           {
+                               void M(byte[] salt)
+                               {
+                                   var sessionId = {|E128075:new AppRandom()|}.Next();
+                                   {|E128075:new AppRandom()|}.NextBytes(salt);
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_StoredDerivedRandom_AtTheCall()
+    {
+        return VerifyAsync("""
+                           using System;
+
+                           class AppRandom : Random
+                           {
+                           }
+
+                           class AppOverride : Random
+                           {
+                               public override int Next() => 4;
+                           }
+
+                           class C
+                           {
+                               private readonly AppOverride _rng = new AppOverride();
+                               private readonly AppRandom _inherited = new AppRandom();
+
+                               void M()
+                               {
+                                   var sessionKey = {|E128075:_rng.Next()|};
+                                   var authToken = {|E128075:_inherited.Next()|};
+                                   var jitter = _rng.Next();
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_CallOnTypeParameterCreation()
+    {
+        return VerifyAsync("""
+                           using System;
+
+                           class C
+                           {
+                               void M<T>() where T : Random, new()
+                               {
+                                   var token = {|E128075:new T().Next()|};
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task NoReport_ForDerivedRandom_WithOrdinaryValues()
+    {
+        return VerifyAsync("""
+                           using System;
+
+                           class AppRandom : Random
+                           {
+                           }
+
+                           class C
+                           {
+                               void M()
+                               {
+                                   var jitter = new AppRandom().Next();
+                                   var other = new AppRandom();
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_FileMarks_ForUsingAlias()
+    {
+        return VerifyAsync("""
+                           using System;
+                           using R = System.Security.Cryptography.RandomNumberGenerator;
+
+                           class C
+                           {
+                               void M(byte[] buffer)
+                               {
+                                   R.Fill(buffer);
+                                   var delay = {|E128075:new Random()|}.Next(5);
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_FileMarks_ForUsingStatic()
+    {
+        return VerifyAsync("""
+                           using System;
+                           using static System.Security.Cryptography.CryptographicOperations;
+
+                           class C
+                           {
+                               bool M(byte[] a, byte[] b)
+                               {
+                                   var delay = {|E128075:Random.Shared|}.Next(5);
+                                   return FixedTimeEquals(a, b);
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_FileMarks_ForTypeInCryptographyChildNamespace()
+    {
+        return VerifyAsync("""
+                           using System;
+                           using System.Security.Cryptography.X509Certificates;
+
+                           class C
+                           {
+                               void M(X509Certificate2 certificate)
+                               {
+                                   var delay = {|E128075:new Random()|}.Next(5);
+                               }
+                           }
+                           """);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_FileMarks_ForVarThatInfersCryptographyType()
+    {
+        return VerifyAsync(
+            [
+                """
+                static class Maker
+                {
+                    public static System.Security.Cryptography.SHA256 Make() => System.Security.Cryptography.SHA256.Create();
+                }
+                """,
+                """
+                class C
+                {
+                    void M()
+                    {
+                        var hasher = Maker.Make();
+                        var delay = {|E128075:new System.Random()|}.Next(5);
+                    }
+                }
+                """
+            ]);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task Reports_FileMarks_ForCryptographyMemberAccessWithoutTypeName()
+    {
+        return VerifyAsync(
+            [
+                """
+                static class Maker
+                {
+                    public static System.Security.Cryptography.SHA256 Make() => System.Security.Cryptography.SHA256.Create();
+                }
+                """,
+                """
+                class C
+                {
+                    void M(byte[] data)
+                    {
+                        byte[] hash = Maker.Make().ComputeHash(data);
+                        var delay = {|E128075:new System.Random()|}.Next(5);
+                    }
+                }
+                """
+            ]);
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task NoReport_ForFileWithNoCryptographyTypeName()
+    {
+        return VerifyAsync("""
+                           using System;
+
+                           class C
+                           {
+                               void M(byte[] data)
+                               {
+                                   var hasher = Make();
+                                   var delay = new Random().Next(5);
+                               }
+
+                               static object Make() => new object();
+                           }
+                           """);
+    }
 }
diff --git a/tests/E128.Analyzers.Tests/InsecureRandomInCryptoContextE128CodeFixTests.cs b/tests/E128.Analyzers.Tests/InsecureRandomInCryptoContextE128CodeFixTests.cs
index 1eb3ffc..15f1d36 100644
--- a/tests/E128.Analyzers.Tests/InsecureRandomInCryptoContextE128CodeFixTests.cs
+++ b/tests/E128.Analyzers.Tests/InsecureRandomInCryptoContextE128CodeFixTests.cs
@@ -7,11 +7,15 @@ namespace E128.Analyzers.Tests;
 
 public sealed class InsecureRandomInCryptoContextE128CodeFixTests
 {
-    private static Task VerifyFixAsync(string source, string fixedCode) =>
-        CodeFixTestRunner.RunAsync<InsecureRandomInCryptoContextAnalyzer, InsecureRandomInCryptoContextCodeFixProvider>(source, fixedCode);
+    private static Task VerifyFixAsync(string source, string fixedCode)
+    {
+        return CodeFixTestRunner.RunAsync<InsecureRandomInCryptoContextAnalyzer, InsecureRandomInCryptoContextCodeFixProvider>(source, fixedCode);
+    }
 
-    private static Task VerifyNoFixAsync(string source, params DiagnosticResult[] expected) =>
-        CodeFixTestRunner.NoFixAsync<InsecureRandomInCryptoContextAnalyzer, InsecureRandomInCryptoContextCodeFixProvider>(source, expected: expected);
+    private static Task VerifyNoFixAsync(string source, params DiagnosticResult[] expected)
+    {
+        return CodeFixTestRunner.NoFixAsync<InsecureRandomInCryptoContextAnalyzer, InsecureRandomInCryptoContextCodeFixProvider>(source, expected: expected);
+    }
 
     [Fact]
     [Trait("Category", "CI")]
@@ -25,6 +29,7 @@ public sealed class InsecureRandomInCryptoContextE128CodeFixTests
             {
                 void M()
                 {
+                    var bytes = RandomNumberGenerator.GetBytes(1);
                     var x = {|E128075:Random.Shared|}.Next(10);
                 }
             }
@@ -36,6 +41,7 @@ public sealed class InsecureRandomInCryptoContextE128CodeFixTests
             {
                 void M()
                 {
+                    var bytes = RandomNumberGenerator.GetBytes(1);
                     var x = RandomNumberGenerator.GetInt32(10);
                 }
             }
@@ -54,6 +60,7 @@ public sealed class InsecureRandomInCryptoContextE128CodeFixTests
             {
                 void M()
                 {
+                    var bytes = RandomNumberGenerator.GetBytes(1);
                     var x = {|E128075:Random.Shared|}.Next(1, 100);
                 }
             }
@@ -65,6 +72,7 @@ public sealed class InsecureRandomInCryptoContextE128CodeFixTests
             {
                 void M()
                 {
+                    var bytes = RandomNumberGenerator.GetBytes(1);
                     var x = RandomNumberGenerator.GetInt32(1, 100);
                 }
             }
@@ -83,6 +91,7 @@ public sealed class InsecureRandomInCryptoContextE128CodeFixTests
             {
                 void M()
                 {
+                    var bytes = RandomNumberGenerator.GetBytes(1);
                     var rng = {|E128075:new Random()|};
                 }
             }
@@ -101,9 +110,162 @@ public sealed class InsecureRandomInCryptoContextE128CodeFixTests
             {
                 void M()
                 {
+                    var bytes = RandomNumberGenerator.GetBytes(1);
                     var x = {|E128075:Random.Shared|}.NextDouble();
                 }
             }
             """);
     }
+
+    [Theory]
+    [Trait("Category", "CI")]
+    [InlineData("{|E128075:Random.Shared|}.Next()", "RandomNumberGenerator.GetInt32(int.MaxValue)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(1)", "RandomNumberGenerator.GetInt32(1)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(Max)", "RandomNumberGenerator.GetInt32(Max)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(B)", "RandomNumberGenerator.GetInt32(B)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(Ch)", "RandomNumberGenerator.GetInt32(Ch)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(5, 6)", "RandomNumberGenerator.GetInt32(5, 6)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(-10, -5)", "RandomNumberGenerator.GetInt32(-10, -5)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(-5, 5)", "RandomNumberGenerator.GetInt32(-5, 5)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(B, S)", "RandomNumberGenerator.GetInt32(B, S)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(Max, Max + 1)", "RandomNumberGenerator.GetInt32(Max, Max + 1)")]
+    [InlineData("{|E128075:System.Random.Shared|}.Next(1, 7)", "RandomNumberGenerator.GetInt32(1, 7)")]
+    public Task RandomSharedNext_FixedToGetInt32_ForConstantBounds(string original, string replacement)
+    {
+        return VerifyFixAsync(
+            Source(original, "using System;"),
+            Source(replacement, "using System;\nusing System.Security.Cryptography;"));
+    }
+
+    [Theory]
+    [Trait("Category", "CI")]
+    [InlineData("{|E128075:Random.Shared|}.Next(maxValue: 7)", "RandomNumberGenerator.GetInt32(7)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(minValue: 1, maxValue: 7)", "RandomNumberGenerator.GetInt32(1, 7)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(maxValue: 7, minValue: 1)", "RandomNumberGenerator.GetInt32(1, 7)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(1, maxValue: 7)", "RandomNumberGenerator.GetInt32(1, 7)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(maxValue: Max + 1, minValue: Max)", "RandomNumberGenerator.GetInt32(Max, Max + 1)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(maxValue: B, minValue: -B)", "RandomNumberGenerator.GetInt32(-B, B)")]
+    public Task RandomSharedNext_NamedArguments_FixedToGetInt32(string original, string replacement)
+    {
+        return VerifyFixAsync(
+            Source(original, "using System;"),
+            Source(replacement, "using System;\nusing System.Security.Cryptography;"));
+    }
+
+    [Theory]
+    [Trait("Category", "CI")]
+    [InlineData("{|E128075:Random.Shared|}.Next(0)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(2, 2)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(5, 5)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(10, 2)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(-3)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(Z)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(B, B)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(Max, Max)")]
+    public Task RandomSharedNext_NoFixOffered_ForEmptyOrNegativeRange(string expression)
+    {
+        return VerifyNoFixAsync(Source(expression, "using System;"));
+    }
+
+    [Theory]
+    [Trait("Category", "CI")]
+    [InlineData("{|E128075:Random.Shared|}.Next(n)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(n, n)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(n, n + 5)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(1, n)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(n, 10)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(n++)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(Get())")]
+    [InlineData("{|E128075:Random.Shared|}.Next(1, Get())")]
+    [InlineData("{|E128075:Random.Shared|}.Next(Extra)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(maxValue: n)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(maxValue: 7, minValue: n)")]
+    [InlineData("{|E128075:Random.Shared|}.Next(maxValue: Get())")]
+    public Task RandomSharedNext_NoFixOffered_ForNonConstantBounds(string expression)
+    {
+        return VerifyNoFixAsync(Source(expression, "using System;"));
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task NewRandomInstanceNext_NoFixOffered()
+    {
+        return VerifyNoFixAsync(Source("{|E128075:new Random()|}.Next(5)", "using System;"));
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task RandomSharedNextInt64_NoFixOffered()
+    {
+        return VerifyNoFixAsync(Source("{|E128075:Random.Shared|}.NextInt64(5)", "using System;"));
+    }
+
+    [Fact]
+    [Trait("Category", "CI")]
+    public Task FixIsNotCapturedByLocalNamedSystem()
+    {
+        return VerifyFixAsync(
+            """
+            using System;
+            class C
+            {
+                int M()
+                {
+                    var System = 1;
+                    var token = {|E128075:Random.Shared|}.Next(5);
+                    return System + token;
+                }
+            }
+            """,
+            """
+            using System;
+            using System.Security.Cryptography;
+            class C
+            {
+                int M()
+                {
+                    var System = 1;
+                    var token = RandomNumberGenerator.GetInt32(5);
+                    return System + token;
+                }
+            }
+            """);
+    }
+
+    [Theory]
+    [Trait("Category", "CI")]
+    [InlineData("{|E128075:Random.Shared|}.Next(5)", "class RandomNumberGenerator { }")]
+    [InlineData("{|E128075:Random.Shared|}.Next(5)", "class RandomNumberGenerator { public static int GetInt32(int toExclusive) => 0; }")]
+    [InlineData("{|E128075:Random.Shared|}.Next(maxValue: 7, minValue: 1)", "class RandomNumberGenerator { public static int GetInt32(int toExclusive) => 0; }")]
+    public Task ShadowedRandomNumberGenerator_NoFixOffered(string expression, string shadow)
+    {
+        var source = Source(expression, "using System;") +
+                     "\nnamespace System.Security.Cryptography\n{\n    " + shadow + "\n}\n";
+        return VerifyNoFixAsync(source);
+    }
+
+    private static string Source(string expression, string usings)
+    {
+        return $$"""
+                 {{usings}}
+
+                 class C
+                 {
+                     const byte B = 5;
+                     const byte Z = 0;
+                     const short S = 7;
+                     const char Ch = 'a';
+                     const int Max = 10;
+                     static int Extra = 3;
+                     static int Get() => 3;
+
+                     int M(int n)
+                     {
+                         var token = {{expression}};
+                         return 0;
+                     }
+                 }
+
+                 """;
+    }
 }
