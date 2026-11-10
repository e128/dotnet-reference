using System;

namespace E128.Reference.Core;

/// <summary>
///     Greeting service — demonstrates a simple injectable service pattern.
/// </summary>
public sealed class Greeter
{
    private const string FallbackName = "World";

    public Greeter()
        : this(FallbackName)
    {
    }

    public Greeter(string defaultName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultName);
        DefaultName = defaultName;
    }

    internal string DefaultName { get; }

    public string Greet()
    {
        return Greet(null);
    }

    public string Greet(string? name)
    {
        return $"Hello, {name ?? DefaultName}!";
    }
}
