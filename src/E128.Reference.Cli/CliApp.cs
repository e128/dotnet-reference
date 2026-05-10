using System.CommandLine;
using E128.Reference.Core;
using Microsoft.Extensions.DependencyInjection;

namespace E128.Reference.Cli;

internal static class CliApp
{
    internal static RootCommand CreateRootCommand()
    {
        return CreateRootCommand(ConfigureServices(new ServiceCollection()).BuildServiceProvider());
    }

    internal static RootCommand CreateRootCommand(ServiceProvider serviceProvider)
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
            var greeter = serviceProvider.GetRequiredService<Greeter>();
            await parseResult.InvocationConfiguration.Output.WriteLineAsync(greeter.Greet(name));
        });

        return rootCommand;
    }

    private static IServiceCollection ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<Greeter>();
        return services;
    }
}
