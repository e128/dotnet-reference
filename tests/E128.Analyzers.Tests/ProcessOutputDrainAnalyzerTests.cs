using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ProcessOutputDrainAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task RedirectedOutputNeverRead_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessOutputDrainAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(Process process)
                               {
                                   process.StartInfo.RedirectStandardOutput = true;
                                   process.Start();
                                   {|E128160:process.WaitForExit()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RedirectedErrorNeverRead_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessOutputDrainAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(Process process)
                               {
                                   process.StartInfo.RedirectStandardError = true;
                                   process.Start();
                                   {|E128160:process.WaitForExit()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReadToEndBeforeWait_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessOutputDrainAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(Process process)
                               {
                                   process.StartInfo.RedirectStandardOutput = true;
                                   process.Start();
                                   _ = process.StandardOutput.ReadToEnd();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EventHandlerBeforeWait_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessOutputDrainAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(Process process)
                               {
                                   process.StartInfo.RedirectStandardOutput = true;
                                   process.OutputDataReceived += (_, _) => { };
                                   process.Start();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReadAfterWait_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessOutputDrainAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(Process process)
                               {
                                   process.StartInfo.RedirectStandardOutput = true;
                                   process.Start();
                                   {|E128160:process.WaitForExit()|};
                                   _ = process.StandardOutput.ReadToEnd();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReadOnAnotherProcess_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessOutputDrainAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(Process process, Process other)
                               {
                                   process.StartInfo.RedirectStandardOutput = true;
                                   process.Start();
                                   _ = other.StandardOutput.ReadToEnd();
                                   {|E128160:process.WaitForExit()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoRedirectedStream_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessOutputDrainAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(Process process)
                               {
                                   process.StartInfo.CreateNoWindow = true;
                                   process.Start();
                                   process.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StartInfoLocalPassedToProcessStart_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessOutputDrainAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M()
                               {
                                   var psi = new ProcessStartInfo("tool") { RedirectStandardOutput = true };
                                   var process = Process.Start(psi);
                                   {|E128160:process.WaitForExit()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StartInfoLocalAssignedToProcess_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessOutputDrainAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(Process process)
                               {
                                   var psi = new ProcessStartInfo("tool");
                                   psi.RedirectStandardError = true;
                                   process.StartInfo = psi;
                                   process.Start();
                                   {|E128160:process.WaitForExit()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InitializerFlagOnOtherProcess_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessOutputDrainAnalyzer>("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(Process plain)
                               {
                                   var redirected = new Process
                                   {
                                       StartInfo = new ProcessStartInfo("tool") { RedirectStandardOutput = true }
                                   };
                                   plain.Start();
                                   plain.WaitForExit();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task HelperReceivesOutputStream_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<ProcessOutputDrainAnalyzer>("""
                           using System.Diagnostics;
                           using System.IO;
                           class C
                           {
                               static void Drain(StreamReader reader)
                               {
                                   _ = reader.ReadToEnd();
                               }

                               void M(Process process)
                               {
                                   process.StartInfo.RedirectStandardOutput = true;
                                   process.Start();
                                   Drain(process.StandardOutput);
                                   process.WaitForExit();
                               }
                           }
                           """);
    }
}
