### New Rules

Rule ID  | Category    | Severity | Notes
---------|-------------|----------|-------
E128055  | Style       | Warning  | Unbalanced pragma warning disable — disable without matching restore
E128056  | Reliability | Warning  | FileInfo.Exists TOCTOU race — check then read without try/catch
E128057  | Reliability | Warning  | Unprotected cleanup in finally block — File/Directory.Delete without try/catch
E128058  | Design      | Warning  | List<T> returned as IReadOnlyList<T> without .AsReadOnly()
E128059  | Design      | Warning  | Interface method parameter unused in implementation
E128060  | Design      | Warning  | Dictionary<K,V> returned as IReadOnlyDictionary<K,V> without .AsReadOnly()
E128061  | Design      | Warning  | Static readonly array should be ImmutableArray<T>
E128062  | Testing     | Warning  | Test uses outdated ReferenceAssemblies — does not match project target framework

