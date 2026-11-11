namespace E128.Reference.Core.Models;

/// <summary>
///     Expected failure for a greeting operation. An expected failure travels as a value, not as an
///     exception, so the caller cannot forget it.
/// </summary>
/// <param name="Message">Operator-facing reason.</param>
public sealed record GreetingError(string Message);
