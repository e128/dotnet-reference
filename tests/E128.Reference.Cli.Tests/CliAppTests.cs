using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace E128.Reference.Cli.Tests;

public sealed class CliAppTests
{
    [Fact]
    [Trait("Category", "CI")]
    public async Task Invoke_WithNoArgs_WritesHelloWorld()
    {
        await using var output = new StringWriter();
        var parseResult = CliApp.CreateRootCommand().Parse([]);
        parseResult.InvocationConfiguration.Output = output;

        var exitCode = await parseResult.InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal("Hello, World!", output.ToString().TrimEnd());
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task Invoke_WithNameOption_WritesPersonalizedGreeting()
    {
        await using var output = new StringWriter();
        var parseResult = CliApp.CreateRootCommand().Parse(["--name", "Claude"]);
        parseResult.InvocationConfiguration.Output = output;

        var exitCode = await parseResult.InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal("Hello, Claude!", output.ToString().TrimEnd());
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task Invoke_WithShortAlias_WritesPersonalizedGreeting()
    {
        await using var output = new StringWriter();
        var parseResult = CliApp.CreateRootCommand().Parse(["-n", "World"]);
        parseResult.InvocationConfiguration.Output = output;

        var exitCode = await parseResult.InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal("Hello, World!", output.ToString().TrimEnd());
    }
}
