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
