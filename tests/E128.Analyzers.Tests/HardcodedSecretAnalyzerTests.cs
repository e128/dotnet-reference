using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HardcodedSecretAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        var test = new CSharpAnalyzerTest<HardcodedSecretAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FieldNamedPassword_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               const string AdminPassword = {|E128165:"hunter2hunter2"|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyInitializerNamedApiKey_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               public string ApiKey { get; set; } = {|E128165:"key-value-1234567890"|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AssignmentToSecret_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               string clientSecret;

                               void M()
                               {
                                   clientSecret = {|E128165:"abcdef123456"|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamedArgumentPassword_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               void Login(string user, string password) { }

                               void M()
                               {
                                   Login("bob", password: {|E128165:"correct-horse"|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConnectionStringWithPassword_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               string Db = {|E128165:"Server=db.contoso.com;Database=app;User Id=sa;Password=P@ssw0rd!;"|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConnectionStringWithoutPassword_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               string Db = "Server=db.contoso.com;Database=app;Integrated Security=true;";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConnectionStringFormatPlaceholder_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               string Db = "Server=db.contoso.com;Database=app;Password={0};";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LookupKeyAndHeaderNames_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               const string ConnectionStringName = "DefaultConnection";
                               const string PasswordHeader = "X-Password-Hint";
                               const string SecretPath = "/etc/secrets/token";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShortOrSpacedValue_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               string Password = "";
                               string Secret = "short";
                               string ApiKey = "enter your key here";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnrelatedName_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               string Greeting = "hello-world-12345";
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
                               const string AdminPassword = "hunter2hunter2";
                           }
                           """);
    }
}
