namespace E128.Reference.Core.Models;

/// <summary>
///     Inbound request to generate a greeting.
/// </summary>
public sealed record GreetingRequest(string? Name = null);
