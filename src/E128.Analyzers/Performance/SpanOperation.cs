namespace E128.Analyzers.Performance;

/// <summary>
///     The reduction that an E128135 loop performs. The fix provider selects the TensorPrimitives
///     member from this value.
/// </summary>
internal enum SpanOperation
{
    Sum = 0,

    Max = 1,

    Min = 2
}
