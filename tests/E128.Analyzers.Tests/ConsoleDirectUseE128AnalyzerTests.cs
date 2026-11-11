using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConsoleDirectUseE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task ConsoleWriteLine_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ConsoleDirectUseAnalyzer>("""
                           using System;

                           class C
                           {
                               void M()
                               {
                                   {|E128045:Console.WriteLine|}("hello");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConsoleReadLine_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ConsoleDirectUseAnalyzer>("""
                           using System;

                           class C
                           {
                               void M()
                               {
                                   var line = {|E128045:Console.ReadLine|}();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConsoleError_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ConsoleDirectUseAnalyzer>("""
                           using System;

                           class C
                           {
                               void M()
                               {
                                   var err = {|E128045:Console.Error|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonConsoleWriteLine_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<ConsoleDirectUseAnalyzer>("""
                           using System.Diagnostics;

                           class C
                           {
                               void M()
                               {
                                   Debug.WriteLine("hello");
                               }
                           }
                           """);
    }
}
