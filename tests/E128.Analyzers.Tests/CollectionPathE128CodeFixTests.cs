using System.Threading.Tasks;
using E128.Analyzers.FileSystem;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CollectionPathE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<CollectionPathAnalyzer, CollectionPathCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListStringPathToFileInfo_Fixed()
    {
        const string source = """
                              using System.Collections.Generic;

                              public class Service
                              {
                                  public void Process(List<string> {|E128053:filePaths|}) { }
                              }
                              """;

        const string fixedCode = """
                                 using System.Collections.Generic;
                                 using System.IO;

                                 public class Service
                                 {
                                     public void Process(List<FileInfo> filePaths) { }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListStringDirToDirectoryInfo_Fixed()
    {
        const string source = """
                              using System.Collections.Generic;

                              public class Service
                              {
                                  public void Process(List<string> {|E128053:directories|}) { }
                              }
                              """;

        const string fixedCode = """
                                 using System.Collections.Generic;
                                 using System.IO;

                                 public class Service
                                 {
                                     public void Process(List<DirectoryInfo> directories) { }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
