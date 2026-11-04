using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsForbiddenAlgorithmNameAnalyzerTests
{
    private const string ThirdPartyCiphers = """
                                              namespace Org.Ciphers
                                              {
                                                  class BlowfishEngine { }
                                                  class RC4Engine { }
                                                  class ArcfourEngine { }
                                                  class Cast5Engine { }
                                                  class Cast6Engine { }
                                                  class IdeaEngine { }
                                                  class SkipjackEngine { }
                                                  class Ideal { }
                                                  class Ideas { }
                                                  class Broadcaster { }
                                                  class Podcast5Service { }
                                              }
                                              """;

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<FipsForbiddenAlgorithmNameAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task VendoredCipherInSystemNamespace_Reports_E128148()
    {
        return VerifyAsync("""
                           namespace System.Vendor;
                           class BlowfishEngine
                           {
                           }

                           class C
                           {
                               void M()
                               {
                                   var cipher = {|E128148:new BlowfishEngine()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnsignedAssemblyNamedSystem_IsNotTreatedAsBcl()
    {
        var test = new CSharpAnalyzerTest<FipsForbiddenAlgorithmNameAnalyzer, DefaultVerifier>
        {
            TestCode = """
                       class C
                       {
                           void M()
                           {
                               var cipher = {|E128148:new System.Vendor.BlowfishEngine()|};
                           }
                       }
                       """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        var vendored = test.TestState.AdditionalProjects["System.Vendor"];
        vendored.ReferenceAssemblies = ReferenceAssemblies.Net.Net100;
        vendored.Sources.Add("""
                             namespace System.Vendor
                             {
                                 public class BlowfishEngine
                                 {
                                 }
                             }
                             """);
        test.TestState.AdditionalProjectReferences.Add("System.Vendor");

        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BlowfishCreation_Reports_E128148()
    {
        return VerifyAsync("""
                           class BlowfishEngine
                           {
                           }

                           class C
                           {
                               void M()
                               {
                                   var cipher = {|E128148:new BlowfishEngine()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Rc4Factory_Reports_E128148()
    {
        return VerifyAsync("""
                           class RC4Engine
                           {
                               public static RC4Engine Create() => default;
                           }

                           class C
                           {
                               void M()
                               {
                                   var cipher = {|E128148:RC4Engine.Create()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IdeaEngine_Reports_E128148()
    {
        return VerifyAsync("""
                           class IdeaEngine
                           {
                           }

                           class C
                           {
                               void M()
                               {
                                   var cipher = {|E128148:new IdeaEngine()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PlainIdeaWord_NoReport()
    {
        return VerifyAsync("""
                           class Idea
                           {
                           }

                           class C
                           {
                               void M()
                               {
                                   var idea = new Idea();
                               }
                           }
                           """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("RC4Engine")]
    [InlineData("ArcfourEngine")]
    [InlineData("Cast5Engine")]
    [InlineData("Cast6Engine")]
    [InlineData("SkipjackEngine")]
    public Task NonApprovedCipherByName_Reports_E128148(string typeName)
    {
        var code = ThirdPartyCiphers + "\nclass C { void M() { var cipher = {|E128148:new Org.Ciphers." + typeName + "()|}; } }";

        return VerifyAsync(code);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("Ideal")]
    [InlineData("Ideas")]
    [InlineData("Broadcaster")]
    [InlineData("Podcast5Service")]
    public Task NameFragmentInsideWord_NoReport(string typeName)
    {
        var code = ThirdPartyCiphers + "\nclass C { void M() { var value = new Org.Ciphers." + typeName + "(); } }";

        return VerifyAsync(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IdeaRoleWordAtEnd_Reports_E128148()
    {
        return VerifyAsync("""
                           namespace Org.Misc
                           {
                               class IdeaRankingAlgorithm { }
                               class IdeaManagedService { }
                               class IdeaEngine { }
                               class IdeaCipher { }
                               class IdeaManaged { }
                               class IdeaCryptoServiceProvider { }
                               class MyIdeaEngine { }
                           }

                           class C
                           {
                               void M()
                               {
                                   var a = new Org.Misc.IdeaRankingAlgorithm();
                                   var b = new Org.Misc.IdeaManagedService();
                                   var c = {|E128148:new Org.Misc.IdeaEngine()|};
                                   var d = {|E128148:new Org.Misc.IdeaCipher()|};
                                   var e = {|E128148:new Org.Misc.IdeaManaged()|};
                                   var f = {|E128148:new Org.Misc.IdeaCryptoServiceProvider()|};
                                   var g = {|E128148:new Org.Misc.MyIdeaEngine()|};
                               }
                           }
                           """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("IdeaService")]
    [InlineData("IdeaBoard")]
    public Task OrdinaryIdeaNames_NoReport(string typeName)
    {
        var code = "namespace App.Planning { class " + typeName + " { } }\nclass C { void M() { var value = new App.Planning." + typeName + "(); } }";

        return VerifyAsync(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IdeaServiceInCryptographicNamespace_NoReport()
    {
        return VerifyAsync("""
                           namespace Org.Ciphers { class IdeaService { } }

                           class C
                           {
                               void M()
                               {
                                   var service = new Org.Ciphers.IdeaService();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CryptographicIdeaUses_Report_E128148()
    {
        return VerifyAsync("""
                           namespace Org.Cryptography { class Idea { } }

                           namespace Org.Misc
                           {
                               interface IBlockCipher { }
                               class Idea : IBlockCipher { }
                               class IdeaCipher { }
                           }

                           class C
                           {
                               void M()
                               {
                                   var a = {|E128148:new Org.Cryptography.Idea()|};
                                   var b = {|E128148:new Org.Misc.Idea()|};
                                   var c = {|E128148:new Org.Misc.IdeaCipher()|};
                               }
                           }
                           """);
    }
}
