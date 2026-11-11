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

        var exitCode = await parseResult.InvokeAsync(cancellationToken: TestContext.Current.CancellationToken);

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

        var exitCode = await parseResult.InvokeAsync(cancellationToken: TestContext.Current.CancellationToken);

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

        var exitCode = await parseResult.InvokeAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal("Hello, World!", output.ToString().TrimEnd());
    }

    /// <summary>
    ///     A blank name is the expected failure case of the result union, so the command reports
    ///     that case through its own exit code and writes the reason to standard error.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public async Task Invoke_WithBlankName_ReturnsExpectedFailureExitCode()
    {
        await using var output = new StringWriter();
        await using var error = new StringWriter();
        var parseResult = CliApp.CreateRootCommand().Parse(["--name", " "]);
        parseResult.InvocationConfiguration.Output = output;
        parseResult.InvocationConfiguration.Error = error;

        var exitCode = await parseResult.InvokeAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Equal("A recipient name cannot be blank.", error.ToString().TrimEnd());
        Assert.Equal(string.Empty, output.ToString());
    }
}
