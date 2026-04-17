using System.Threading.Tasks;
using E128.Analyzers.FileSystem;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CollectionPathE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<CollectionPathAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListOfStringWithPathName_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           public class Service
                           {
                               public void Process(List<string> {|E128053:filePaths|}) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IReadOnlyListOfStringWithDirName_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           public class Service
                           {
                               public void Process(IReadOnlyList<string> {|E128053:directories|}) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IEnumerableOfStringWithPathName_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           public class Service
                           {
                               public void Process(IEnumerable<string> {|E128053:inputPaths|}) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListOfStringWithNonPathName_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           public class Service
                           {
                               public void Process(List<string> names) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListOfFileInfo_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.IO;

                           public class Service
                           {
                               public void Process(List<FileInfo> filePaths) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task XPathExclusion_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           public class Service
                           {
                               public void Process(List<string> xpaths) { }
                           }
                           """);
    }
}
