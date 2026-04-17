using System.CommandLine;
using E128.Reference.Core;

namespace E128.Reference.Cli;

internal static class CliApp
{
    internal static RootCommand CreateRootCommand()
    {
        var nameOption = new Option<string?>("--name") { Description = "The name to greet" };
        nameOption.Aliases.Add("-n");

        var rootCommand = new RootCommand("E128 Reference CLI — hello world with System.CommandLine")
        {
            nameOption
        };

        rootCommand.SetAction(async (parseResult, _) =>
        {
            var name = parseResult.GetValue(nameOption);
            var greeter = new Greeter();
            await parseResult.InvocationConfiguration.Output.WriteLineAsync(greeter.Greet(name));
        });

        return rootCommand;
    }
}
