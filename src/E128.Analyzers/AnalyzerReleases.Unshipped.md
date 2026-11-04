### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
E128154 | E128-Reliability | Warning | A singleton registration whose implementation type takes a scoped service in its constructor is reported. The singleton keeps the first scoped instance for the life of the application.
E128155 | E128-Reliability | Warning | A call in a foreach body that mutates the collection the loop enumerates is reported. The enumerator throws InvalidOperationException on the next step.
E128156 | E128-Security | Error | An HttpClientHandler certificate validation callback that accepts every certificate is reported. A callback that reads the chain or the policy errors is not reported.
E128157 | E128-Security | Warning | An equality, inequality, or SequenceEqual comparison over a secret-derived byte sequence is reported. Use CryptographicOperations.FixedTimeEquals instead.
E128159 | E128-Reliability | Warning | A TaskCompletionSource created without TaskCreationOptions.RunContinuationsAsynchronously is reported. A computed options value is not reported.
E128160 | E128-Reliability | Warning | A WaitForExit call in a method that redirects a process stream and never reads it is reported. The child blocks once the pipe buffer fills.
E128161 | E128-Reliability | Warning | A Path.Combine call with a constant rooted or parent segment is reported. A rooted segment discards every earlier segment.
E128162 | E128-Reliability | Warning | A read of a local after the same block calls Dispose on it is reported. The object throws ObjectDisposedException on the next member call that reads its state.
E128163 | E128-Reliability | Warning | An ImmutableArray field that no initializer and no assignment reaches is reported. The default array throws NullReferenceException on read.
E128164 | E128-Security | Warning | A string literal that starts with http:// or https:// and carries a host is reported outside a test compilation. Attribute arguments, namespace and schema hosts, and example hosts are not reported.
E128165 | E128-Security | Warning | A credential-named string literal of 8 or more characters, and a connection string that holds a password, are reported outside a test compilation. An empty value and a format placeholder are not reported.
E128166 | E128-Security | Warning | An ILogger call that passes a value, or an interpolated hole, named for a credential or personal-data word is reported outside a test compilation. Cancellation tokens and metadata names such as TokenCount are not reported.
E128167 | E128-Style | Warning | A TODO, FIXME, or HACK comment that does not match e128_tracked_ref_pattern is reported. The default pattern accepts AB#123 and #123.
E128168 | E128-Testing | Warning | A Fact or Theory Skip reason that does not match e128_tracked_ref_pattern is reported.
E128169 | E128-Style | Warning | An Obsolete attribute with no message, or an empty message, is reported.
E128170 | E128-Reliability | Warning | A constant index of 1 or more taken from a string.Split result with no Length read in the member is reported. Index 0 and from-end indexes are not reported.
E128171 | E128-Testing | Warning | An xUnit test whose whole body is one Assert.Fail call is reported.
E128172 | E128-Testing | Warning | A catch of Exception or XunitException with no rethrow, around a try block that calls Assert, is reported.
E128173 | E128-Style | Warning | A SuppressMessage or ExcludeFromCodeCoverage attribute with no Justification, or a blank one, is reported.
E128174 | E128-Design | Warning | A constructor or primary constructor with more class or interface parameters than e128_max_ctor_dependencies is reported. The default ceiling is 7.

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
E128001 | E128-Design | Warning | Design | Warning | The category gains the E128- prefix, so category-E128-Design selects the rules of one area.
E128002 | E128-Style | Warning | Style | Warning | The category gains the E128- prefix, so category-E128-Style selects the rules of one area.
E128003 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
E128004 | E128-Reliability | Warning | Reliability | Warning | The category gains the E128- prefix, so category-E128-Reliability selects the rules of one area.
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

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
E128022 | Design | Warning | Removed. Other rules cover this check.
