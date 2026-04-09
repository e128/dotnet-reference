using System;
using System.CommandLine;
using System.Threading.Tasks;
using E128.Reference.Core;

var nameOption = new Option<string>("--name") { Description = "The name to greet" };
nameOption.Aliases.Add("-n");

var rootCommand = new RootCommand("E128 Reference CLI — hello world with System.CommandLine")
{
    nameOption,
};

rootCommand.SetAction((parseResult, _) =>
{
    var name = parseResult.GetValue(nameOption);
    var greeter = new Greeter();
    Console.WriteLine(greeter.Greet(name));
    return Task.CompletedTask;
});

return await rootCommand.Parse(args).InvokeAsync();
