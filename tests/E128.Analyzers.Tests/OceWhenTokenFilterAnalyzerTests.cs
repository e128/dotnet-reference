using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class OceWhenTokenFilterAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<OceWhenTokenFilterAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NegatedTokenFilter()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading;
                           class C
                           {
                               void M(CancellationToken token)
                               {
                                   try
                                   {
                                   }
                                   {|E128100:catch (OperationCanceledException) when (!token.IsCancellationRequested)|}
                                   {
                                       throw;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_PositiveTokenIdiom()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading;
                           class C
                           {
                               void M(CancellationToken token)
                               {
                                   try
                                   {
                                   }
                                   catch (OperationCanceledException) when (token.IsCancellationRequested)
                                   {
                                       throw;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_UnfilteredCatch()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   try
                                   {
                                   }
                                   catch (OperationCanceledException)
                                   {
                                       throw;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_UnrelatedFilter()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M(bool flag)
                               {
                                   try
                                   {
                                   }
                                   catch (OperationCanceledException) when (!flag)
                                   {
                                       throw;
                                   }
                               }
                           }
                           """);
    }
}
