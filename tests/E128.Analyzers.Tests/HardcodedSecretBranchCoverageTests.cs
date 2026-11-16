using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HardcodedSecretBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<HardcodedSecretAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_CredentialValuesInEachDeclarationShape()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            class Holder
            {
                public string Password { get; set; } = "";
            }

            class C
            {
                string field = "Sup3rS3cret!";

                string Secret { get; } = {|E128165:"Sup3rS3cret!"|};

                string Token => {|E128165:"Sup3rS3cret!"|};

                string ApiKey
                {
                    get => {|E128165:"Sup3rS3cret!"|};
                }

                string Credential
                {
                    get
                    {
                        return {|E128165:"Sup3rS3cret!"|};
                    }
                }

                void M(Holder holder, Dictionary<string, string> map, bool flag, string other)
                {
                    string password = {|E128165:"Sup3rS3cret!"|};
                    password = {|E128165:"Sup3rS3cret!"|};
                    holder.Password = {|E128165:"Sup3rS3cret!"|};
                    map["Password"] = {|E128165:"Sup3rS3cret!"|};
                    var copy = new Dictionary<string, string> { ["Secret"] = {|E128165:"Sup3rS3cret!"|} };
                    Use(secret: {|E128165:"Sup3rS3cret!"|});
                    string accessKey = other ?? {|E128165:"Sup3rS3cret!"|};
                    string token = flag ? {|E128165:"Sup3rS3cret!"|} : other;
                    string bearer = ({|E128165:"Sup3rS3cret!"|});
                }

                void Use(string secret) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLiteralIsNotTheValueOfACredentialTarget()
    {
        return VerifyAsync("""
            using System;
            using System.Collections.Generic;
            class C
            {
                string Secret
                {
                    set => Use("Sup3rS3cret!");
                }

                string Other()
                {
                    return "Sup3rS3cret!";
                }

                string Name { get; } = "Sup3rS3cret!";

                string Password
                {
                    get
                    {
                        Func<string> make = () => { return "Sup3rS3cret!"; };
                        string Local() { return "Sup3rS3cret!"; }
                        return make() + Local();
                    }
                }

                void M(Dictionary<string, string> map, bool flag, string key, string other)
                {
                    map[key] = "Sup3rS3cret!";
                    var title = "Sup3rS3cret!";
                    string password = flag ? other : "x";
                    string token = (flag ? "Sup3rS3cret!" : other) + other;
                    Use("Sup3rS3cret!");
                    string secret = Compose("Sup3rS3cret!");
                    string tokenWithCondition = ("Sup3rS3cret!" == other ? other : other);
                }

                void Use(string value) { }

                string Compose(string value) => value;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheNameOrTheValueShowsTheStringIsALabel()
    {
        return VerifyAsync("""
            class C
            {
                string passwordHeader = "Sup3rS3cret!";
                string tokenName = "Sup3rS3cret!";
                string secretHint = "Sup3rS3cret!";
                string CancellationToken = "Sup3rS3cret!";
                string cancellation_token = "Sup3rS3cret!";
                string password = "short";
                string secret = "has some spaces inside";
                string token = "X-Api-Key";
                string apiKey = "BRAVE_API_KEY";
                string credential = "Auth:Password";
                string bearer = "Some-Header-Name";
                string passwd = "some/path/value";
                string pwd = "dotted.name.value";
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ValuesThatOnlyResembleKeyNames()
    {
        return VerifyAsync("""
            class C
            {
                string a_password = {|E128165:"lowercase-hyphenated"|};
                string secret = {|E128165:"Title--Empty"|};
                string token = {|E128165:"Title-lower"|};
                string apikey = {|E128165:"LETMEINPLEASE"|};
                string accesskey = {|E128165:"Pass1-Word2"|};
                string privatekey = {|E128165:"-leading-hyphen"|};
                string api_key = {|E128165:"AbCdEfGh1"|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ConnectionStringsThatPairAServerWithAPassword()
    {
        return VerifyAsync("""
            class C
            {
                void M(string pwdValue)
                {
                    var a = {|E128165:"Server=db;Password=hunter2hunter2"|};
                    var b = {|E128165:"Data Source=db; Pwd = hunter2hunter2 ;Trusted=true"|};
                    var c = {|E128165:"Host=db;Database=x;Password=hunter2hunter2;"|};
                    var d = "Server=db;Password={0}";
                    var e = "Server=db;Password=";
                    var f = "Password=hunter2hunter2;Timeout=30";
                    var g = "Server=db;NoEqualsHere;Timeout=30";
                    var h = "Server=db;=orphan;Timeout=30";
                    var i = "https://example.test/?host=a&password=hunter2hunter2";
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_Utf8LiteralsAssignedToCredentialNames()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                ReadOnlySpan<byte> Password => {|E128165:"Sup3rS3cret!"u8|};

                void M()
                {
                    ReadOnlySpan<byte> token = {|E128165:"Sup3rS3cret!"u8|};
                    ReadOnlySpan<byte> title = "Sup3rS3cret!"u8;
                }
            }
            """);
    }
}
