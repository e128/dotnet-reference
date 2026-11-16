using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PfxImportEphemeralKeySetBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<PfxImportEphemeralKeySetAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_FileConstructorsWithoutTheEphemeralBit()
    {
        return VerifyAsync("""
            using System.Security.Cryptography.X509Certificates;
            class C
            {
                void M(string path, string password, X509KeyStorageFlags unknown)
                {
                    X509Certificate2 b = {|E128191:new(path, password, X509KeyStorageFlags.Exportable)|};
                    var c = {|E128191:new X509Certificate2(path, password, X509KeyStorageFlags.Exportable)|};
                    var d = {|E128191:new X509Certificate2(keyStorageFlags: X509KeyStorageFlags.PersistKeySet, password: password, fileName: path)|};
                    var e = new X509Certificate2(path, password, X509KeyStorageFlags.EphemeralKeySet);
                    var f = new X509Certificate2(path, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
                    var g = new X509Certificate2(path, password, unknown);
                    var h = {|E128191:new X509Certificate2(path, "pw", (X509KeyStorageFlags)0)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheConstructorHasNoFileNameOrNoFlags()
    {
        return VerifyAsync("""
            using System.Security.Cryptography.X509Certificates;
            class Other
            {
                public Other(string fileName) { }
            }

            class C
            {
                void M(byte[] data, string path, string password)
                {
                    var a = new X509Certificate2(data);
                    var b = new X509Certificate2(data, password);
                    var d = new Other(path);
                    var e = new System.Text.StringBuilder();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_EveryPkcs12LoaderWithoutTheEphemeralBit()
    {
        return VerifyAsync("""
            using System.Security.Cryptography.X509Certificates;
            class C
            {
                void M(byte[] data, string path, string password, X509KeyStorageFlags unknown)
                {
                    var a = {|E128191:X509CertificateLoader.LoadPkcs12(data, password)|};
                    var b = {|E128191:X509CertificateLoader.LoadPkcs12FromFile(path, password)|};
                    var c = {|E128191:X509CertificateLoader.LoadPkcs12Collection(data, password)|};
                    var d = {|E128191:X509CertificateLoader.LoadPkcs12CollectionFromFile(path, password)|};
                    var e = {|E128191:X509CertificateLoader.LoadPkcs12(data, password, X509KeyStorageFlags.Exportable)|};
                    var f = X509CertificateLoader.LoadPkcs12(data, password, X509KeyStorageFlags.EphemeralKeySet);
                    var g = X509CertificateLoader.LoadPkcs12(data, password, unknown);
                    var h = X509CertificateLoader.LoadCertificate(data);
                    var i = X509CertificateLoader.LoadCertificateFromFile(path);
                    var j = new Own().LoadPkcs12(data, password);
                }
            }

            class Own
            {
                public object LoadPkcs12(byte[] data, string password) => data;
            }
            """);
    }
}
