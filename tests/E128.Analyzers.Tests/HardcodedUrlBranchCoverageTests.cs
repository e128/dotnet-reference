using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HardcodedUrlBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<HardcodedUrlAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAFieldDeclaresOneFixedValue()
    {
        return VerifyAsync("""
            class C
            {
                const string A = "https://api.contoso.test/v1";

                static readonly string B = "https://api.contoso.test/v2";

                public static readonly string C2 = "https://api.contoso.test/v3";
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_FieldsAndPropertiesThatAreNotFixedDeclarations()
    {
        return VerifyAsync("""
            class C
            {
                readonly string a = {|E128164:"https://api.contoso.test/v1"|};

                static string b = {|E128164:"https://api.contoso.test/v2"|};

                string Property { get; } = {|E128164:"https://api.contoso.test/v3"|};

                string Arrow => {|E128164:"https://api.contoso.test/v4"|};

                string Method() => {|E128164:"https://api.contoso.test/v5"|};

                void Use()
                {
                    string Local() => {|E128164:"https://api.contoso.test/v6"|};
                    var x = {|E128164:"HTTPS://API.CONTOSO.TEST/v7"|};
                }
            }

            class ClientSettings
            {
                string Url { get; } = {|E128164:"https://api.contoso.test/v8"|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTextIsNotAnAbsoluteUrlWithAHost()
    {
        return VerifyAsync("""
            class C
            {
                void M()
                {
                    var a = "http://";
                    var b = "https://";
                    var c = "ftp://files.contoso.test";
                    var d = "not a url";
                    var e = "http://api.example.com/path";
                    var f = "https://EXAMPLE.org";
                    var g = "https://sub.w3.org/ns";
                    var h = "mailto:user@example.com";
                }
            }
            """);
    }
}
