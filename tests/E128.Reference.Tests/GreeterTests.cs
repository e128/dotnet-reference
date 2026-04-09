using System;
using E128.Reference.Core;
using Xunit;

namespace E128.Reference.Tests;

public sealed class GreeterTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void Greet_ReturnsGreeting_WithDefaultName()
    {
        var greeter = new Greeter();

        var result = greeter.Greet();

        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Greet_ReturnsPersonalizedGreeting_WithCustomName()
    {
        var greeter = new Greeter();

        var result = greeter.Greet("Claude");

        Assert.Equal("Hello, Claude!", result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Greet_ReturnsGreeting_WithCustomDefaultName()
    {
        var greeter = new Greeter("Universe");

        var result = greeter.Greet();

        Assert.Equal("Hello, Universe!", result);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Constructor_Throws_WhenDefaultNameIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Greeter(null!));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Throws_WhenDefaultNameIsWhiteSpace(string defaultName)
    {
        Assert.Throws<ArgumentException>(() => new Greeter(defaultName));
    }
}
