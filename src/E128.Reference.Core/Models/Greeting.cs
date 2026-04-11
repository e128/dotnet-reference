using System;

namespace E128.Reference.Core.Models;

/// <summary>
/// Immutable greeting value object.
/// </summary>
public sealed record Greeting(string Message, string RecipientName, DateTimeOffset CreatedAt);
