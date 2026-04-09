using System;

namespace E128.Reference.Core;

/// <summary>
/// Greeting service — demonstrates a simple injectable service pattern.
/// </summary>
public sealed class Greeter
{
    private readonly string _defaultName;

    public Greeter(string defaultName = "World")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultName);
        _defaultName = defaultName;
    }

    public string Greet(string? name = null) =>
        $"Hello, {name ?? _defaultName}!";
}
