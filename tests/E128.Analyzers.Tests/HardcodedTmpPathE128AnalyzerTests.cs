using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HardcodedTmpPathE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task ExactTmp_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<HardcodedTmpPathE128Analyzer>("""
                           class C
                           {
                               void M()
                               {
                                   var path = {|E128023:"/tmp"|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TmpPrefix_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<HardcodedTmpPathE128Analyzer>("""
                           class C
                           {
                               void M()
                               {
                                   var path = {|E128023:"/tmp/foo/cache.db"|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WindowsTemp_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<HardcodedTmpPathE128Analyzer>("""
                           class C
                           {
                               void M()
                               {
                                   var path = {|E128023:@"C:\Temp"|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WindowsWindowsTemp_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<HardcodedTmpPathE128Analyzer>("""
                           class C
                           {
                               void M()
                               {
                                   var path = {|E128023:@"C:\Windows\Temp\cache.db"|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PathGetTempPath_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<HardcodedTmpPathE128Analyzer>("""
                           class C
                           {
                               void M()
                               {
                                   var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cache.db");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TmpFooWithoutSlash_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<HardcodedTmpPathE128Analyzer>("""
                           class C
                           {
                               void M()
                               {
                                   var path = "/tmpfoo";
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TemporaryPath_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<HardcodedTmpPathE128Analyzer>("""
                           class C
                           {
                               void M()
                               {
                                   var path = "/temporary";
                               }
                           }
                           """);
    }
}
