### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|------
E128109 | Reliability | Warning  | stackalloc must be bounded and outside loops
E128110 | Reliability | Warning  | Zero a pooled buffer in a finally block before Return
E128111 | Reliability | Warning  | Pooled buffer used after Return
E128112 | Unsafe      | Warning  | Use Unsafe.BitCast instead of Unsafe.As
E128113 | Unsafe      | Warning  | Use the unaligned Unsafe read and write methods
E128114 | Unsafe      | Warning  | Index the array instead of bypassing its bounds check
E128115 | Unsafe      | Warning  | Do not reinterpret between bool and int
E128116 | Performance | Warning  | Do not use MethodImplOptions.AggressiveOptimization
E128117 | Performance | Warning  | Use LibraryImport instead of DllImport
E128118 | Unsafe      | Warning  | Do not reinterpret bytes as a non-blittable type
E128119 | Unsafe      | Warning  | Use an inline array instead of a fixed-size buffer
E128120 | Unsafe      | Warning  | Do not skip local initialization without a measured reason
E128121 | Performance | Warning  | Use SequenceEqual instead of an element-by-element loop
E128122 | Performance | Warning  | Guard an expensive log argument with IsEnabled
E128123 | Performance | Warning  | Use GeneratedRegex for a constant pattern
E128124 | Performance | Warning  | Cache SearchValues in a static readonly field
E128125 | Unsafe      | Warning  | Pin the object explicitly with fixed
E128126 | Unsafe      | Warning  | Do not write to a string through a fixed pointer
E128127 | Unsafe      | Warning  | Do not create a null managed reference
E128128 | Unsafe      | Warning  | Keep the delegate alive while native code holds its function pointer
E128129 | Unsafe      | Warning  | Do not bulk-copy a type that holds references
