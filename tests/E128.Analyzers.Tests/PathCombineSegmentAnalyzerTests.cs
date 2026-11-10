using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PathCombineSegmentAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<PathCombineSegmentAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RootedSegment_Fires()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
