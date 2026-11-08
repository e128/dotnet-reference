using System;

namespace E128.Reference.Core;

/// <summary>
///     Greeting service — demonstrates a simple injectable service pattern.
/// </summary>
public sealed class Greeter
{
    public Greeter(string defaultName = "World")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultName);
        DefaultName = defaultName;
    }

    internal string DefaultName { get; }

    public string Greet(string? name = null)
    {
        return $"Hello, {name ?? DefaultName}!";
    }
}
