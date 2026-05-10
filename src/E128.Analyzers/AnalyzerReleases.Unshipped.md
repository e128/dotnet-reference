### New Rules

Rule ID  | Category    | Severity | Notes
---------|-------------|----------|-------
E128066  | Performance | Warning  | Linear lookup inside loop creates O(n²) complexity
E128067  | Performance | Warning  | String concatenation in loop creates O(n²) allocations
E128068  | Performance | Warning  | Sort operation inside loop creates O(n² log n) complexity
E128069  | Performance | Warning  | List.Insert(0, ...) in loop creates O(n²) complexity
E128070  | Reliability | Warning  | Pool Rent() capacity must be bounded
E128071  | Security    | Warning  | Use a FIPS-approved hash algorithm
E128072  | Performance | Info     | Prefer SHA256.HashData() over SHA256.Create()
E128073  | Testing     | Warning  | Test method missing [Trait("Category", ...)] attribute
E128074  | Design      | Warning  | Readonly struct property should use init accessor
