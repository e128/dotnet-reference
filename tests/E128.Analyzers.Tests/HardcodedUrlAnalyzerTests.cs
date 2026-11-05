using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HardcodedUrlAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        var test = new CSharpAnalyzerTest<HardcodedUrlAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task HttpsLiteral_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               string Url = {|E128164:"https://api.contoso.com/v1"|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task HttpLiteralInArgument_Fires()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               Uri M() => new Uri({|E128164:"http://internal.contoso.com"|});
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InterpolatedStringWithLiteralHost_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               string M(int id) => $"{|E128164:https://api.contoso.com/items/|}{id}";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InterpolatedHost_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               string M(string host) => $"https://{host}/items";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task HostThatOnlyEndsWithAllowedSuffix_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               string A = {|E128164:"https://evilexample.com/x"|};
                               string B = {|E128164:"https://notw3.org/x"|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamespaceAndSchemaHosts_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               string A = "http://www.w3.org/2001/XMLSchema";
                               string B = "http://schemas.microsoft.com/winfx/2006/xaml";
                               string C2 = "https://example.com/docs";
                               string D = "https://www.example.com/docs";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AttributeArgument_Silent()
    {
        return VerifyAsync("""
                           using System;

                           [AttributeUsage(AttributeTargets.Class)]
                           sealed class LinkAttribute : Attribute
                           {
                               public LinkAttribute(string url) { }
                           }

                           [Link("https://api.contoso.com/docs")]
                           class C
                           {
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonUrlText_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               string A = "https://";
                               string B = "see https://api.contoso.com for details";
                               string C2 = "ftp://files.contoso.com";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TestCompilation_Silent()
    {
        return VerifyAsync("""
                           namespace Xunit
                           {
                               public sealed class FactAttribute : System.Attribute
                               {
                               }
                           }

                           class C
                           {
                               string Url = "https://api.contoso.com/v1";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OptionsClassInitializer_Silent()
    {
        return VerifyAsync("""
                           class OllamaOptions
                           {
                               string BaseUrl { get; init; } = "http://localhost:11434";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstFieldInitializer_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               private const string BaseUrl = "https://api.contoso.com";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LocalConstInitializer_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               string M()
                               {
                                   const string Url = {|E128164:"https://api.contoso.com"|};
                                   return Url;
                               }
                           }
                           """);
    }
}
