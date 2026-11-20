# Roadmap
*Updated: 2026-11-20T17:17:34Z*

Unplanned work awaiting a plan.

An analyzer candidate carries the shape, the register score, and the code fix.
A coverage gap carries the shape, the gap, and the site. The open register lives
in [index.md](../lode/dotnet/analyzer-candidates/index.md), and every declined
shape lives in
[skipped-decisions.md](../lode/dotnet/analyzer-candidates/skipped-decisions.md).

## Active

No active roadmap item.

## Waiting

Work found by the 2026-11-20 comment sweep. Each item was an Excuse comment. The comment is gone and the gap stays. The rationale that survived lives in [Decision Records](../lode/decisions/summary.md).

### BareParseAnalyzer

**Shape**: `Enum.Parse<T>` reports while `Enum.TryParse<T>` exists.
**Gap**: The rule special-cases the generic call instead of modelling the `TryParse` shape.
**Site**: [BareParseAnalyzer.cs](../src/E128.Analyzers/Reliability/BareParseAnalyzer.cs)

### FileSystemPathAnalyzer

**Shape**: A local derived from another local in a longer chain escapes tracking.
**Gap**: Local derivation follows one extra hop only.
**Site**: [FileSystemPathAnalyzer.cs](../src/E128.Analyzers/FileSystem/FileSystemPathAnalyzer.cs)

### FipsRsaKeySizeAnalyzer

**Shape**: Two shapes escape the RSA key-size rule.
**Gap**: The rule stops tracking when a factory returns a non-RSA type. It also leaves an upcast local unresolved.
**Site**: [FipsRsaKeySizeAnalyzer.cs](../src/E128.Analyzers/Security/FipsRsaKeySizeAnalyzer.cs)

### HttpBodyReadWithoutSizeBoundAnalyzer

**Shape**: A body read reports nothing when `HttpContent` is unavailable.
**Gap**: The rule matches the member by symbol and gives up instead of matching by name.
**Site**: [HttpBodyReadWithoutSizeBoundAnalyzer.cs](../src/E128.Analyzers/Security/HttpBodyReadWithoutSizeBoundAnalyzer.cs)

### HttpClientCertificateBypassAnalyzer

**Shape**: Two chain shapes escape certificate bypass detection.
**Gap**: The rule follows one delegate hop and reads only a block with a single return.
**Site**: [HttpClientCertificateBypassAnalyzer.cs](../src/E128.Analyzers/Security/HttpClientCertificateBypassAnalyzer.cs)

### HandlerNoDecompressionAnalyzer

**Shape**: A shared handler with no decompression never reports.
**Gap**: Any non-local handler target counts as configured without an initializer check.
**Site**: [HandlerNoDecompressionAnalyzer.cs](../src/E128.Analyzers/Performance/HandlerNoDecompressionAnalyzer.cs)

### ParallelForEachAsyncCapAnalyzer

**Shape**: A variable or factory call that sets no degree cap never reports.
**Gap**: The rule gives up when the options argument is not an object creation.
**Site**: [ParallelForEachAsyncCapAnalyzer.cs](../src/E128.Analyzers/Performance/ParallelForEachAsyncCapAnalyzer.cs)

### ParallelForPerItemBodyAnalyzer

**Shape**: Four call forms stay silent.
**Gap**: The rule does not model an async lambda, named arguments, a method group, or any overload other than `(int, int, Action<int>)`.
**Site**: [ParallelForPerItemBodyAnalyzer.cs](../src/E128.Analyzers/Performance/ParallelForPerItemBodyAnalyzer.cs)

### ChainedStringReplaceCodeFixProvider

**Shape**: Every replace loop other than the doubled-character collapse stays diagnostic-only.
**Gap**: The fix handles one loop shape.
**Site**: [ChainedStringReplaceCodeFixProvider.cs](../src/E128.Analyzers.CodeFixers/Performance/ChainedStringReplaceCodeFixProvider.cs)

### TaskRunAsyncLambdaCodeFixProvider

**Shape**: A block-bodied lambda gets no fix.
**Gap**: The fix declines the body instead of rewriting its statements.
**Site**: [TaskRunAsyncLambdaCodeFixProvider.cs](../src/E128.Analyzers.CodeFixers/Design/TaskRunAsyncLambdaCodeFixProvider.cs)

### TaskFromResultSyncIoCodeFixProvider

**Shape**: The receiver check trusts identifier text.
**Gap**: A same-named receiver passes without a semantic bind.
**Site**: [TaskFromResultSyncIoCodeFixProvider.cs](../src/E128.Analyzers.CodeFixers/Reliability/TaskFromResultSyncIoCodeFixProvider.cs)

### DiskRoundtripCodeFixProvider

**Shape**: The fix can return the original expression.
**Gap**: The fix trusts the analyzer filter instead of proving the rewrite.
**Site**: [DiskRoundtripCodeFixProvider.cs](../src/E128.Analyzers.CodeFixers/Reliability/DiskRoundtripCodeFixProvider.cs)

### Code fixers with no working action

**Shape**: Three providers register, and every action returns unchanged.
**Gap**: `ConstructorNewDiRegisteredCodeFixProvider`, `JsonDocumentLifetimeCodeFixProvider`, and `UnboundedTaskWhenAllCodeFixProvider` leave their diagnostics unfixable.
**Site**: [ConstructorNewDiRegisteredCodeFixProvider.cs](../src/E128.Analyzers.CodeFixers/Reliability/ConstructorNewDiRegisteredCodeFixProvider.cs), [JsonDocumentLifetimeCodeFixProvider.cs](../src/E128.Analyzers.CodeFixers/Reliability/JsonDocumentLifetimeCodeFixProvider.cs), [UnboundedTaskWhenAllCodeFixProvider.cs](../src/E128.Analyzers.CodeFixers/Reliability/UnboundedTaskWhenAllCodeFixProvider.cs)

### GeneratedRegexNestedCodeFixProvider

**Shape**: The provider registers for a shape it cannot rewrite.
**Gap**: Every action returns without a change.
**Site**: [GeneratedRegexNestedCodeFixProvider.cs](../src/E128.Analyzers.CodeFixers/Reliability/GeneratedRegexNestedCodeFixProvider.cs)
