using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HardcodedSecretAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<HardcodedSecretAnalyzer>(code);
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

                           class Tests
                           {
                               [Xunit.Fact]
                               public void RealTest()
                               {
                               }
                           }

                           class C
                           {
                               const string AdminPassword = "hunter2hunter2";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TokenName_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               const string AccessToken = {|E128165:"abcd1234efgh5678"|};
                               const string BearerValue = {|E128165:"abcd1234efgh5678"|};
                               const string DbCredential = {|E128165:"abcd1234efgh5678"|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PatternArnAndIdNames_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               const string SecretsManagerArnPattern = "arn:aws:secretsmanager:.*";
                               const string SecretArn = "placeholdervalue";
                               const string TokenId = "placeholdervalue";
                               string CancellationToken = "placeholdervalue";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CoalesceAndConditional_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               static string Lookup() => null;

                               string Password = Lookup() ?? {|E128165:"hunter2hunter2"|};
                               string ApiKey = (true ? {|E128165:"key-value-1234567890"|} : Lookup());
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConditionalCondition_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               string Password = "hunter2hunter2" == "x" ? "a" : "b";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IndexerKeyPassword_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           class C
                           {
                               void M(Dictionary<string, string> settings)
                               {
                                   settings["Password"] = {|E128165:"hunter2hunter2"|};
                                   settings["Region"] = "eu-west-1-primary";
                               }

                               Dictionary<string, string> Initial = new()
                               {
                                   ["ApiKey"] = {|E128165:"key-value-1234567890"|}
                               };
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyGetterReturn_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               string Password
                               {
                                   get { return {|E128165:"hunter2hunter2"|}; }
                               }

                               string Greeting
                               {
                                   get { return "hunter2hunter2"; }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReturnInsideMethod_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               string Password()
                               {
                                   return "hunter2hunter2";
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Utf8Literal_Fires()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               static ReadOnlySpan<byte> Password => {|E128165:"hunter2hunter2"u8|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UrlQueryString_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               string Url = "https://example.com/login?host=example.com&password=hunter2hunter2";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConnectionStringWithSpacesAroundEquals_Fires()
    {
        return VerifyAsync("""
                           class C
                           {
                               string Db = {|E128165:"Server = db.contoso.com; Database = app; Password = P@ssw0rd!"|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task HeaderAndConfigPathValues_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               const string ApiKey = "X-Api-Key";
                               const string PasswordKey = "Auth:Password";
                               const string SecretKey = "Some-Header-Name";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EnvironmentVariableNameValue_Silent()
    {
        return VerifyAsync("""
                           class C
                           {
                               public const string BraveApiKey = "BRAVE_API_KEY";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task TestCompilationWithTwoXunitReferences_Silent()
    {
        var references = await ReferenceAssemblies.Net.Net100.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var test = new CSharpAnalyzerTest<HardcodedSecretAnalyzer, DefaultVerifier>
        {
            TestCode = """
                       class FactAttribute : System.Attribute { }
                       class C
                       {
                           const string AdminPassword = "hunter2hunter2";

                           [Fact]
                           void T() { }
                       }
                       """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.TestState.AdditionalReferences.Add(EmitStub(
            "XunitStubOne",
            "namespace Xunit { public sealed class FactAttribute { } }",
            references));
        test.TestState.AdditionalReferences.Add(EmitStub(
            "XunitStubTwo",
            "namespace Xunit { public sealed class FactAttribute { } }",
            references));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Two references that both declare Xunit.FactAttribute make GetTypeByMetadataName return null.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public async Task TestCompilationWithNUnitReference_Silent()
    {
        var references = await ReferenceAssemblies.Net.Net100.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var test = new CSharpAnalyzerTest<HardcodedSecretAnalyzer, DefaultVerifier>
        {
            TestCode = """
                       class C
                       {
                           const string AdminPassword = "hunter2hunter2";

                           [NUnit.Framework.Test]
                           void T() { }
                       }
                       """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.TestState.AdditionalReferences.Add(EmitStub(
            "NUnitStub",
            "namespace NUnit.Framework { public sealed class TestAttribute : System.Attribute { } }",
            references));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task TestCompilationWithMsTestReference_Silent()
    {
        var references = await ReferenceAssemblies.Net.Net100.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var test = new CSharpAnalyzerTest<HardcodedSecretAnalyzer, DefaultVerifier>
        {
            TestCode = """
                       class C
                       {
                           const string AdminPassword = "hunter2hunter2";

                           [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
                           void T() { }
                       }
                       """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.TestState.AdditionalReferences.Add(EmitStub(
            "MsTestStub",
            "namespace Microsoft.VisualStudio.TestTools.UnitTesting { public sealed class TestMethodAttribute : System.Attribute { } }",
            references));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    private static PortableExecutableReference EmitStub(
        string assemblyName,
        string source,
        ImmutableArray<MetadataReference> references)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success);

        return MetadataReference.CreateFromImage(stream.ToArray());
    }
}
