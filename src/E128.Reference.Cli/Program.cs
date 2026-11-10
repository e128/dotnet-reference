using System.Threading;
using E128.Reference.Cli;

return await CliApp.CreateRootCommand().Parse(args).InvokeAsync(cancellationToken: CancellationToken.None);
