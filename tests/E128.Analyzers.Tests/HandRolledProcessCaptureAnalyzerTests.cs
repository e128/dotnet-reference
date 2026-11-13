using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HandRolledProcessCaptureAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenBothStreamsAreReadBeforeTheWait()
    {
        return AnalyzerTestRunner.VerifyAsync<HandRolledProcessCaptureAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   var info = new ProcessStartInfo(file)
                                   {
                                       RedirectStandardOutput = true,
                                       RedirectStandardError = true,
                                       UseShellExecute = false,
                                   };
                                   using var process = Process.Start(info);
                                   string output = process.StandardOutput.ReadToEnd();
                                   string error = process.StandardError.ReadToEnd();
                                   {|E128223:process.WaitForExit()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenOneStreamIsReadAsynchronously()
    {
        return AnalyzerTestRunner.VerifyAsync<HandRolledProcessCaptureAnalyzer>("""
                           using System.Diagnostics;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M(string file)
                               {
                                   var process = Process.Start(new ProcessStartInfo(file)
                                   {
                                       RedirectStandardError = true,
                                       UseShellExecute = false,
                                   });
                                   string error = await process.StandardError.ReadToEndAsync();
                                   await {|E128223:process.WaitForExitAsync()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheRedirectIsSetOnTheProcessStartInfo()
    {
        return AnalyzerTestRunner.VerifyAsync<HandRolledProcessCaptureAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M()
                               {
                                   using var process = new Process();
                                   process.StartInfo.RedirectStandardOutput = true;
                                   process.Start();
                                   string output = process.StandardOutput.ReadToEnd();
                                   {|E128223:process.WaitForExit()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenNoStreamIsRedirected()
    {
        return AnalyzerTestRunner.VerifyAsync<HandRolledProcessCaptureAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   using var process = Process.Start(file);
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReadIsALineRead()
    {
        return AnalyzerTestRunner.VerifyAsync<HandRolledProcessCaptureAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   var info = new ProcessStartInfo(file) { RedirectStandardOutput = true };
                                   using var process = Process.Start(info);
                                   string first = process.StandardOutput.ReadLine();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenStandardInputIsRedirected()
    {
        return AnalyzerTestRunner.VerifyAsync<HandRolledProcessCaptureAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   var info = new ProcessStartInfo(file)
                                   {
                                       RedirectStandardOutput = true,
                                       RedirectStandardInput = true,
                                   };
                                   using var process = Process.Start(info);
                                   string output = process.StandardOutput.ReadToEnd();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStreamIsReadAfterTheWait()
    {
        return AnalyzerTestRunner.VerifyAsync<HandRolledProcessCaptureAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   var info = new ProcessStartInfo(file) { RedirectStandardOutput = true };
                                   using var process = Process.Start(info);
                                   process.WaitForExit();
                                   string output = process.StandardOutput.ReadToEnd();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStreamOfAnotherProcessIsRead()
    {
        return AnalyzerTestRunner.VerifyAsync<HandRolledProcessCaptureAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string file)
                               {
                                   var info = new ProcessStartInfo(file) { RedirectStandardOutput = true };
                                   using var process = Process.Start(info);
                                   using var other = Process.Start(info);
                                   string output = other.StandardOutput.ReadToEnd();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }
}
