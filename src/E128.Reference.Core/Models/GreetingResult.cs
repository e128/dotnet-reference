namespace E128.Reference.Core.Models;

/// <summary>
///     Result of a greeting operation. The union closes the case list, so a switch over it is
///     exhaustive.
/// </summary>
public readonly union GreetingResult(Greeting, GreetingError);
