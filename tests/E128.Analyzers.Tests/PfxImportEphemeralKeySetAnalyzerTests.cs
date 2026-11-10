using System.Collections.Immutable;
using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PfxImportEphemeralKeySetAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        return AnalyzerTestRunner.VerifyAsync<PfxImportEphemeralKeySetAnalyzer>(code, expected);
    }

    /// <summary>
    ///     Removing every metadata reference makes the certificate types unresolvable, which is the
    ///     fail-closed path the analyzer takes when a type is absent.
    /// </summary>
    private static Task VerifyWithoutBclReferencesAsync(string code)
    {
        var test = new CSharpAnalyzerTest<PfxImportEphemeralKeySetAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            CompilerDiagnostics = CompilerDiagnostics.None
        };
        test.SolutionTransforms.Add(static (solution, projectId) =>
            solution.WithProjectMetadataReferences(projectId, ImmutableArray<MetadataReference>.Empty));
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorOmitsFlags_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               X509Certificate2 M()
                               {
                                   return {|E128191:new X509Certificate2("cert.pfx", "pw".AsSpan())|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorNamedArgumentsOmitFlags_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               X509Certificate2 M()
                               {
                                   return {|E128191:new X509Certificate2(fileName: "cert.pfx", password: "pw".AsSpan())|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorMachineKeySet_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               X509Certificate2 M()
                               {
                                   return {|E128191:new X509Certificate2("cert.pfx", "pw".AsSpan(), X509KeyStorageFlags.MachineKeySet)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorEphemeralKeySet_Silent()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               X509Certificate2 M()
                               {
                                   return new X509Certificate2("cert.pfx", "pw".AsSpan(), X509KeyStorageFlags.EphemeralKeySet);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorEphemeralKeySetWithExportable_Silent()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               X509Certificate2 M()
                               {
                                   return new X509Certificate2(
                                       "cert.pfx",
                                       "pw".AsSpan(),
                                       X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorNonConstantFlags_Silent()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               X509Certificate2 M(X509KeyStorageFlags flags)
                               {
                                   return new X509Certificate2("cert.pfx", "pw".AsSpan(), flags);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorNoFlagsParameter_Silent()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               X509Certificate2 M()
                               {
                                   return new X509Certificate2("cert.pfx", "pw");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorByteArray_Silent()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               X509Certificate2 M(byte[] data)
                               {
                                   return new X509Certificate2(data, "pw", X509KeyStorageFlags.MachineKeySet);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorReadOnlySpanByte_Silent()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               X509Certificate2 M(byte[] data)
                               {
                                   return new X509Certificate2(data.AsSpan(), "pw".AsSpan(), X509KeyStorageFlags.MachineKeySet);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorShadowedByUserType_Silent()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography.X509Certificates;

                           class X509Certificate2
                           {
                               public X509Certificate2(string fileName, string password, X509KeyStorageFlags flags)
                               {
                               }
                           }

                           class C
                           {
                               object M()
                               {
                                   return new X509Certificate2("cert.pfx", "pw", X509KeyStorageFlags.MachineKeySet);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LoadPkcs12OmitsFlags_Fires()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               X509Certificate2 M(byte[] data)
                               {
                                   return {|E128191:X509CertificateLoader.LoadPkcs12(data, "pw")|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LoadPkcs12FromFileOmitsFlags_Fires()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               X509Certificate2 M()
                               {
                                   return {|E128191:X509CertificateLoader.LoadPkcs12FromFile("cert.pfx", "pw")|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LoadPkcs12CollectionOmitsFlags_Fires()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               X509Certificate2Collection M(byte[] data)
                               {
                                   return {|E128191:X509CertificateLoader.LoadPkcs12Collection(data, "pw")|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LoadPkcs12CollectionFromFileOmitsFlags_Fires()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               X509Certificate2Collection M()
                               {
                                   return {|E128191:X509CertificateLoader.LoadPkcs12CollectionFromFile("cert.pfx", "pw")|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LoadPkcs12CollectionEphemeralKeySet_Silent()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               X509Certificate2Collection M(byte[] data)
                               {
                                   return X509CertificateLoader.LoadPkcs12Collection(
                                       data,
                                       "pw",
                                       X509KeyStorageFlags.EphemeralKeySet);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LoadPkcs12MachineKeySet_Fires()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               X509Certificate2 M(byte[] data)
                               {
                                   return {|E128191:X509CertificateLoader.LoadPkcs12(data, "pw", X509KeyStorageFlags.MachineKeySet)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LoadPkcs12EphemeralKeySet_Silent()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               X509Certificate2 M(byte[] data)
                               {
                                   return X509CertificateLoader.LoadPkcs12(data, "pw", X509KeyStorageFlags.EphemeralKeySet);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LoadPkcs12EphemeralKeySetWithExportable_Silent()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               X509Certificate2 M(byte[] data)
                               {
                                   return X509CertificateLoader.LoadPkcs12(
                                       data,
                                       "pw",
                                       X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LoadPkcs12NonConstantFlags_Silent()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               X509Certificate2 M(byte[] data, X509KeyStorageFlags flags)
                               {
                                   return X509CertificateLoader.LoadPkcs12(data, "pw", flags);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CertificateTypesAbsent_Silent()
    {
        return VerifyWithoutBclReferencesAsync("""
                                               class C
                                               {
                                               }
                                               """);
    }
}
