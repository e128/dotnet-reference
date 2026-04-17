using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConsoleDirectUseE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ConsoleDirectUseAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConsoleWriteLine_Fires()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
