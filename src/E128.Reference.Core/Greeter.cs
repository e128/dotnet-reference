using System;

namespace E128.Reference.Core;

/// <summary>
///     Greeting service — demonstrates a simple injectable service pattern.
/// </summary>
public sealed class Greeter
{
    /// <summary>
    ///     The one owner of the default greeting name. Every caller reads this property.
    /// </summary>
    public static string DefaultGreetingName => "World";

    private readonly string _defaultGreeting;

    public Greeter()
        : this(DefaultGreetingName)
    {
    }

    public Greeter(string defaultName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultName);
        DefaultName = defaultName;
        _defaultGreeting = $"Hello, {defaultName}!";
    }

    internal string DefaultName { get; }

    public string Greet()
    {
        return _defaultGreeting;
    }

    public string Greet(string? name)
    {
        return name is null ? _defaultGreeting : $"Hello, {name}!";
    }
}
