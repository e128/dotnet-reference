using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ProcessStartInfoArgumentAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ProcessStartInfoArgumentAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_InterpolatedStringArguments_WithoutArgumentList()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string value)
                               {
                                   var startInfo = new ProcessStartInfo
                                   {
                                       FileName = "tool",
                                       {|E128091:Arguments = $"-x \"{value}\""|}
                                   };
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_EmptyStringArguments()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M()
                               {
                                   var startInfo = new ProcessStartInfo
                                   {
                                       FileName = "tool",
                                       Arguments = string.Empty
                                   };
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenArgumentListPopulated()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(string value)
                               {
                                   var startInfo = new ProcessStartInfo
                                   {
                                       FileName = "tool",
                                       ArgumentList = { "-x", value }
                                   };
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_BareIdentifierArguments_RealRegressionShape()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               string BuildArguments(string value) => $"-m \"{value}\"";

                               void M(string value)
                               {
                                   var arguments = BuildArguments(value);
                                   var startInfo = new ProcessStartInfo
                                   {
                                       FileName = "tool",
                                       {|E128091:Arguments = arguments|}
                                   };
                               }
                           }
                           """);
    }
}
