using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CommittedRedStubAnalyzerTests
{
    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("xunit.v3.core", "3.2.2"),
            new PackageIdentity("xunit.v3.assert", "3.2.2")
        ]);

    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<CommittedRedStubAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = Net100WithXunit
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BlockBodyStub_Fires()
    {
        return VerifyAsync("""
                           using Xunit;
                           public class T
                           {
                               [Fact]
                               public void A()
                               {
                                   {|E128171:Assert.Fail("RED: not implemented")|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExpressionBodyStub_Fires()
    {
        return VerifyAsync("""
                           using Xunit;
                           public class T
                           {
                               [Theory]
                               [InlineData(1)]
                               public void A(int x) => {|E128171:Assert.Fail("RED")|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FailAfterRealAssertions_Silent()
    {
        return VerifyAsync("""
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
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FailInHelperWithoutTestAttribute_Silent()
    {
        return VerifyAsync("""
                           using Xunit;
                           public class T
                           {
                               void Helper()
                               {
                                   Assert.Fail("helper");
                               }
                           }
                           """);
    }
}
