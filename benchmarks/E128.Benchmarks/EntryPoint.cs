using System;
using System.IO;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace E128.Benchmarks;

/// <summary>
///     Starter benchmarks. A human must review them before anyone trusts a number.
///     Run in Release, ideally in a Linux container:
///     <c lang="bash">dotnet run -c Release --project benchmarks/E128.Benchmarks -- --filter '*'</c>.
///     The in-process toolchain keeps BenchmarkDotNet from building a generated project inside this repository tree.
///     The process starts in the output folder. WebApplicationFactory reads MvcTestingAppManifest.json from the
///     current folder, and BenchmarkDotNet writes its default artifacts there, inside the ignored artifacts tree.
///     The change also makes a relative path in a command-line argument resolve against the output folder.
/// </summary>
internal static class EntryPoint
{
    private static void Main(string[] args)
    {
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        var config = DefaultConfig.Instance
            .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));

        BenchmarkSwitcher.FromAssembly(typeof(EntryPoint).Assembly).Run(args, config);
    }
}
