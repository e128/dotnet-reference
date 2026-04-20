using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128064DiskRoundtripCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<DiskRoundtripAnalyzer, DiskRoundtripCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public void DiskRoundtrip_CodeFixProvider_IsRegistered()
    {
        var provider = new DiskRoundtripCodeFixProvider();
        Assert.Contains("E128064", provider.FixableDiagnosticIds);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_TierA_TextText_ReplacesReadWithSource()
    {
        const string source = """
                              using System.IO;
                              class C
                              {
                                  string M(string path, string content)
                                  {
                                      File.WriteAllText(path, content);
                                      return {|E128064:File.ReadAllText(path)|};
                                  }
                              }
                              """;
        const string fixedCode = """
                                 using System.IO;
                                 class C
                                 {
                                     string M(string path, string content)
                                     {
                                         File.WriteAllText(path, content);
                                         return content;
                                     }
                                 }
                                 """;
        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_TierA_BytesBytes_ReplacesReadWithSource()
    {
        const string source = """
                              using System.IO;
                              class C
                              {
                                  byte[] M(string path, byte[] bytes)
                                  {
                                      File.WriteAllBytes(path, bytes);
                                      return {|E128064:File.ReadAllBytes(path)|};
                                  }
                              }
                              """;
        const string fixedCode = """
                                 using System.IO;
                                 class C
                                 {
                                     byte[] M(string path, byte[] bytes)
                                     {
                                         File.WriteAllBytes(path, bytes);
                                         return bytes;
                                     }
                                 }
                                 """;
        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_TierA_CrossKind_TextBytes_WrapsWithEncoding()
    {
        const string source = """
                              using System.IO;
                              class C
                              {
                                  byte[] M(string path, string content)
                                  {
                                      File.WriteAllText(path, content);
                                      return {|E128064:File.ReadAllBytes(path)|};
                                  }
                              }
                              """;
        const string fixedCode = """
                                 using System.IO;
                                 using System.Text;

                                 class C
                                 {
                                     byte[] M(string path, string content)
                                     {
                                         File.WriteAllText(path, content);
                                         return Encoding.UTF8.GetBytes(content);
                                     }
                                 }
                                 """;
        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_TierA_Async_ReplacesAwaitedReadWithSource()
    {
        const string source = """
                              using System.IO;
                              using System.Threading.Tasks;
                              class C
                              {
                                  async Task<string> M(string path, string content)
                                  {
                                      await File.WriteAllTextAsync(path, content);
                                      return {|E128064:await File.ReadAllTextAsync(path)|};
                                  }
                              }
                              """;
        const string fixedCode = """
                                 using System.IO;
                                 using System.Threading.Tasks;
                                 class C
                                 {
                                     async Task<string> M(string path, string content)
                                     {
                                         await File.WriteAllTextAsync(path, content);
                                         return content;
                                     }
                                 }
                                 """;
        return VerifyFixAsync(source, fixedCode);
    }
}
