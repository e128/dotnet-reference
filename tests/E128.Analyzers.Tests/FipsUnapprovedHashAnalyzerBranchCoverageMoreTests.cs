using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsUnapprovedHashAnalyzerBranchCoverageMoreTests
{
    private const string PkcsStub = """
        namespace System.Security.Cryptography.Pkcs
        {
            public class CmsSigner { public object? DigestAlgorithm { get; set; } }
        }
        """;

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnOrdinaryTargetIsAssigned()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsUnapprovedHashAnalyzer>("""
            class C
            {
                string Label = "";

                void M()
                {
                    Label = "SHA1";
                    var local = 0;
                    local = 1;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCmsDigestOidHasNoArgument()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsUnapprovedHashAnalyzer>("""
            using System.Security.Cryptography;
            using System.Security.Cryptography.Pkcs;
            class C
            {
                void M(CmsSigner signer)
                {
                    signer.DigestAlgorithm = new Oid();
                }
            }

            """ + PkcsStub);
    }
}
