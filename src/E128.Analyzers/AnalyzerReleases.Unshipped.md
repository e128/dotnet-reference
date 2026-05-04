### New Rules

Rule ID  | Category    | Severity | Notes
---------|-------------|----------|-------
E128066  | Performance | Warning  | Linear lookup inside loop creates O(n²) complexity
E128067  | Performance | Warning  | String concatenation in loop creates O(n²) allocations
E128068  | Performance | Warning  | Sort operation inside loop creates O(n² log n) complexity
E128069  | Performance | Warning  | List.Insert(0, ...) in loop creates O(n²) complexity
