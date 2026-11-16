using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PfxImportEphemeralKeySetAnalyzerBranchCoverageGapTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<PfxImportEphemeralKeySetAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForCreationsAndInvocationsThatAreNotConstructorCalls()
    {
        return VerifyAsync("""
            using System.Security.Cryptography.X509Certificates;

            struct Pair
            {
                public int First;
            }

            class C
            {
                T Make<T>() where T : X509Certificate2, new()
                {
                    return new T();
                }

                string Describe()
                {
                    var pair = new Pair();
                    var number = new int();
                    return nameof(X509Certificate2) + pair.First + number;
                }
            }
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("public enum X509KeyStorageFlags { Exportable = 4 }")]
    [InlineData("public enum X509KeyStorageFlags : long { EphemeralKeySet = 32 }")]
    [InlineData("public static class X509KeyStorageFlags { public static int EphemeralKeySet => 32; }")]
    [InlineData("public static class X509KeyStorageFlags { public static readonly int EphemeralKeySet = 32; }")]
    public Task Silent_WhenTheFlagTypeHasNoReadableEphemeralKeySetConstant(string declaration)
    {
        return AnalyzerTestRunner.VerifyAsync<PfxImportEphemeralKeySetAnalyzer>(
            $$"""
            namespace System.Security.Cryptography.X509Certificates
            {
                {{declaration}}
            }

            class C
            {
                object M(string path)
                {
                    return System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(path, "pw");
                }
            }
            """);
    }
}
