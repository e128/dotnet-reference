### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
E128154 | Reliability | Warning | A singleton registration whose implementation type takes a scoped service in its constructor is reported. The singleton keeps the first scoped instance for the life of the application.
E128155 | Reliability | Warning | A call in a foreach body that mutates the collection the loop enumerates is reported. The enumerator throws InvalidOperationException on the next step.
E128156 | Security | Error | An HttpClientHandler certificate validation callback that accepts every certificate is reported. A callback that reads the chain or the policy errors is not reported.
E128157 | Security | Warning | An equality, inequality, or SequenceEqual comparison over a secret-derived byte sequence is reported. Use CryptographicOperations.FixedTimeEquals instead.
E128159 | Reliability | Warning | A TaskCompletionSource created without TaskCreationOptions.RunContinuationsAsynchronously is reported. A computed options value is not reported.
E128160 | Reliability | Warning | A WaitForExit call in a method that redirects a process stream and never reads it is reported. The child blocks once the pipe buffer fills.
E128161 | Reliability | Warning | A Path.Combine call with a constant rooted or parent segment is reported. A rooted segment discards every earlier segment.
E128162 | Reliability | Warning | A read of a local after the same block calls Dispose on it is reported. The object throws ObjectDisposedException on the next member call that reads its state.
E128163 | Reliability | Warning | An ImmutableArray field that no initializer and no assignment reaches is reported. The default array throws NullReferenceException on read.

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
E128152 | Performance  | Warning      | Performance  | Warning      | A coordinate step adds no loop depth. An inner loop whose trip count reads an element the enclosing loop produced, such as a cell inside a row, no longer counts against that enclosing loop.
