using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PathCombineSegmentAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task RootedSegment_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<PathCombineSegmentAnalyzer>("""
                           using System.IO;
                           class C
                           {
                               string M()
                               {
                                   return Path.Combine("base", {|E128161:"/etc/passwd"|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DriveLetterSegment_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<PathCombineSegmentAnalyzer>("""
                           using System.IO;
                           class C
                           {
                               string M()
                               {
                                   return Path.Combine("base", {|E128161:"C:\\windows\\system32"|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParentSegment_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<PathCombineSegmentAnalyzer>("""
                           using System.IO;
                           class C
                           {
                               string M()
                               {
                                   return Path.Combine("base", {|E128161:"../secrets"|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NestedParentClimbingAboveBase_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<PathCombineSegmentAnalyzer>("""
                           using System.IO;
                           class C
                           {
                               string M()
                               {
                                   return Path.Combine("base", {|E128161:"a/../../b"|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BareParentSegment_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<PathCombineSegmentAnalyzer>("""
                           using System.IO;
                           class C
                           {
                               string M()
                               {
                                   return Path.Combine("base", "..");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RelativeSegment_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<PathCombineSegmentAnalyzer>("""
                           using System.IO;
                           class C
                           {
                               string M()
                               {
                                   return Path.Combine("base", "sub/file.txt");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonSystemCombine_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<PathCombineSegmentAnalyzer>("""
                           class Path
                           {
                               internal static string Combine(string first, string second) => first + second;
                           }
                           class C
                           {
                               string M()
                               {
                                   return Path.Combine("base", "/etc/passwd");
                               }
                           }
                           """);
    }
}
