using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CommittedRedStubAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { References = Net100WithXunit };

    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("xunit.v3.core", "3.2.2"),
            new PackageIdentity("xunit.v3.assert", "3.2.2")
        ]);

    [Fact]
    [Trait("Category", "CI")]
    public Task BlockBodyStub_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<CommittedRedStubAnalyzer>("""
                           using Xunit;
                           public class T
                           {
                               [Fact]
                               public void A()
                               {
                                   {|E128171:Assert.Fail("RED: not implemented")|};
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExpressionBodyStub_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<CommittedRedStubAnalyzer>("""
                           using Xunit;
                           public class T
                           {
                               [Theory]
                               [InlineData(1)]
                               public void A(int x) => {|E128171:Assert.Fail("RED")|};
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FailAfterRealAssertions_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<CommittedRedStubAnalyzer>("""
                           using Xunit;
                           public class T
                           {
                               [Fact]
                               public void A(int x = 0)
                               {
                                   Assert.Equal(0, x);
                                   Assert.Fail("unreachable guard");
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FailInHelperWithoutTestAttribute_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<CommittedRedStubAnalyzer>("""
                           using Xunit;
                           public class T
                           {
                               void Helper()
                               {
                                   Assert.Fail("helper");
                               }
                           }
                           """, options: Options);
    }
}
