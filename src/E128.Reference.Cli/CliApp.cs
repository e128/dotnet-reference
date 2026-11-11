using System;
using System.CommandLine;
using E128.Reference.Core;
using E128.Reference.Core.Models;
using E128.Reference.Core.Repositories;
using E128.Reference.Core.Services;

namespace E128.Reference.Cli;

internal static class CliApp
{
    internal const int SuccessExitCode = 0;
    internal const int ExpectedFailureExitCode = 1;
    internal const int UnknownResultExitCode = 2;

    internal static RootCommand CreateRootCommand()
    {
        var nameOption = new Option<string?>("--name") { Description = "The name to greet" };
        nameOption.Aliases.Add("-n");

        var rootCommand = new RootCommand("E128 Reference CLI — hello world with System.CommandLine") { nameOption };

        var service = new GreetingService(new Greeter(), new InMemoryGreetingRepository(), TimeProvider.System);

        // The result union closes the case list. Each case maps to its own exit code, and the
        // null arm is the empty case the compiler requires.
        rootCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var name = parseResult.GetValue(nameOption);
            var configuration = parseResult.InvocationConfiguration;
            var result = await service.GreetAsync(new GreetingRequest(name), cancellationToken);

            var (writer, text, exitCode) = result switch
            {
                Greeting greeting => (configuration.Output, greeting.Message, SuccessExitCode),
                GreetingError error => (configuration.Error, error.Message, ExpectedFailureExitCode),
                null => (configuration.Error, "Unknown greeting result.", UnknownResultExitCode)
            };

            await writer.WriteLineAsync(text, cancellationToken);
            return exitCode;
        });

        return rootCommand;
    }
}
