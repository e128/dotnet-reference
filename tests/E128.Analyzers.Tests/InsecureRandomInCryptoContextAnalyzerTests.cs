using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InsecureRandomInCryptoContextAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        return AnalyzerTestRunner.VerifyAsync<InsecureRandomInCryptoContextAnalyzer>(code, expected);
    }

    private static Task VerifyAsync(string[] sources, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<InsecureRandomInCryptoContextAnalyzer, DefaultVerifier> { ReferenceAssemblies = ReferenceAssemblies.Net.Net100 };
        foreach (var source in sources)
        {
            test.TestState.Sources.Add(source);
        }

        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NewRandom_InCryptoContext()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var bytes = RandomNumberGenerator.GetBytes(1);
                                   var r = {|E128075:new Random()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_RandomShared_InCryptoContext()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography;
                           class C
                           {
                               int M()
                               {
                                   var bytes = RandomNumberGenerator.GetBytes(1);
                                   return {|E128075:Random.Shared|}.Next();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WithoutCryptoImport()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   var r = new Random();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenUsingRandomNumberGenerator()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var bytes = RandomNumberGenerator.GetBytes(16);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_BothOccurrences_InCryptoContext()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var bytes = RandomNumberGenerator.GetBytes(1);
                                   var r = {|E128075:new Random()|};
                                   var n = {|E128075:Random.Shared|}.Next();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NewRandom_WhenCryptoNamespaceIsImportedInsideABlockNamespace()
    {
        return VerifyAsync("""
                           namespace N
                           {
                               using System;
                               using System.Security.Cryptography;

                               class C
                               {
                                   void M()
                                   {
                                       var bytes = RandomNumberGenerator.GetBytes(1);
                                       var r = {|E128075:new Random()|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenCryptoNamespaceInComment()
    {
        return VerifyAsync("""
                           using System;
                           // System.Security.Cryptography is not actually imported
                           class C
                           {
                               void M()
                               {
                                   var r = new Random();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenCryptoNamespaceIsOnlyImported()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var r = new Random();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_DerivedRandomType_InCryptoContext()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography;

                           class MyRandom : Random
                           {
                           }

                           class C
                           {
                               void M()
                               {
                                   var bytes = RandomNumberGenerator.GetBytes(1);
                                   var r = {|E128075:new MyRandom()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_SessionId_FromStoredRandom()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               private readonly Random _random = new Random();

                               int M()
                               {
                                   var sessionId = {|E128075:_random.Next()|};
                                   return sessionId;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenNameContainsOrdinaryWord()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               private readonly Random _random = new Random();

                               int M()
                               {
                                   var possessionCount = _random.Next();
                                   var basaltWeight = _random.Next();
                                   return possessionCount + basaltWeight;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NewRandom_WithSeed_InCryptoContext()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var bytes = RandomNumberGenerator.GetBytes(1);
                                   var r = {|E128075:new Random(42)|};
                               }
                           }
                           """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("sessionKey")]
    [InlineData("authToken")]
    [InlineData("passwordSalt")]
    [InlineData("nonceValue")]
    [InlineData("apiKey")]
    [InlineData("iv_bytes")]
    [InlineData("IV_BYTES")]
    [InlineData("SEED")]
    [InlineData("otp")]
    [InlineData("Seed")]
    [InlineData("sessionId")]
    [InlineData("session_id")]
    [InlineData("SessionID")]
    [InlineData("saltBytes")]
    [InlineData("csrfToken")]
    [InlineData("authtoken")]
    [InlineData("csrfCookie")]
    [InlineData("signatureBytes")]
    [InlineData("challengeValue")]
    [InlineData("secretKey")]
    public Task Reports_SensitiveName_ForNewRandom(string name)
    {
        return VerifyAsync($$"""
                            using System;
                            class C
                            {
                                void M()
                                {
                                    var {{name}} = {|E128075:new Random()|};
                                }
                            }
                            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_SensitiveRandom_StoredInFieldPropertyAndAssignment()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               private Random _secret = {|E128075:new Random()|};
                               public Random Nonce { get; } = {|E128075:new Random()|};
                               private Random token;

                               void M()
                               {
                                   var sessionKey = {|E128075:new Random()|}.Next();
                                   token = {|E128075:new Random()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_RandomShared_AssignedToSensitiveLocal()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   var token = {|E128075:Random.Shared|};
                               }
                           }
                           """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("driver")]
    [InlineData("monkey")]
    [InlineData("keyword")]
    [InlineData("jitter")]
    [InlineData("footprint")]
    [InlineData("design")]
    [InlineData("pinnacle")]
    [InlineData("random")]
    [InlineData("DRIVER")]
    [InlineData("SHIPPING")]
    [InlineData("MAPPING")]
    [InlineData("possessionRoll")]
    [InlineData("basalt")]
    [InlineData("sessionCount")]
    [InlineData("sessionTimeout")]
    [InlineData("saltine")]
    [InlineData("cookieless")]
    [InlineData("secretary")]
    [InlineData("signatured")]
    [InlineData("topsecretdata")]
    public Task NoReport_ForOrdinaryName(string name)
    {
        return VerifyAsync($$"""
                            using System;
                            class C
                            {
                                void M()
                                {
                                    var {{name}} = new Random();
                                    var other = Random.Shared;
                                }
                            }
                            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_ForUnrelatedRandomType()
    {
        return VerifyAsync("""
                           namespace Other
                           {
                               class Random
                               {
                               }
                           }

                           class C
                           {
                               void M()
                               {
                                   var sessionKey = new Other.Random();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_StoredRandom_ProducingSensitiveValues()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               private readonly Random _rng = new Random();
                               private double _cached;

                               void M()
                               {
                                   var sessionKey = {|E128075:_rng.Next()|};
                                   var authToken = {|E128075:_rng.NextInt64()|}.ToString();
                                   var newSalt = BitConverter.GetBytes({|E128075:_rng.Next()|});
                                   _cached = _rng.NextDouble();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_StoredRandom_ProducingSensitiveField()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               private readonly Random _rng = new Random();
                               private int _nonce;

                               void M()
                               {
                                   _nonce = {|E128075:_rng.Next()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NextBytes_IntoSensitiveBuffers()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               private readonly Random _rng = new Random();

                               void M(byte[] key, byte[] salt, byte[] nonce)
                               {
                                   {|E128075:_rng.NextBytes(key)|};
                                   {|E128075:Random.Shared|}.NextBytes(salt);
                                   {|E128075:new Random()|}.NextBytes(nonce);
                                   Span<byte> iv = stackalloc byte[16];
                                   {|E128075:_rng.NextBytes(iv)|};
                                   {|E128075:_rng.NextBytes(key.AsSpan())|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_ForStoredRandom_WithOrdinaryValues()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               private readonly Random _rng = new Random();

                               void M(byte[] data)
                               {
                                   var retryDelay = _rng.Next(100);
                                   var jitter = Random.Shared.Next();
                                   _rng.NextBytes(data);
                                   Random.Shared.NextBytes(data);
                                   var buffer = new byte[8];
                                   _rng.NextBytes(buffer);
                                   var order = BitConverter.GetBytes(_rng.Next());
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenRandomValueIsInsideNestedLambdaOrCall()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               private readonly Random _rng = new Random();

                               static int Retry(Func<int> action) => action();
                               static object Build(Random random) => null;

                               void M()
                               {
                                   var session = Retry(() => new Random().Next(10));
                                   var sessionKeyHolder = Build(new Random());
                                   var sessionRetry = Retry(() => _rng.Next(3));
                                   var tokenCount = Retry(() => { var delay = new Random().Next(); return delay; });
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_RandomInCryptographyFile_OncePerSite()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(byte[] data)
                               {
                                   var hash = SHA256.HashData(data);
                                   var delay = {|E128075:new Random()|}.Next(5);
                                   var jitter = {|E128075:Random.Shared|}.Next();
                                   var token = {|E128075:new Random()|};
                                   var other = new Random[] { {|E128075:Random.Shared|} };
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_RandomInCryptographyFile_WhenAnotherTypeUsesCryptography()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography;

                           class Hasher
                           {
                               byte[] Hash(byte[] data) => SHA256.HashData(data);
                           }

                           class Jitter
                           {
                               int Next() => {|E128075:Random.Shared|}.Next(100);
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_QualifiedCryptographyReference_MarksTheFile()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M(byte[] data)
                               {
                                   var hash = System.Security.Cryptography.SHA256.HashData(data);
                                   var random = {|E128075:new System.Random()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_StoredRandomInCryptographyFile_OnlyAtCreation()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography;
                           class C
                           {
                               private readonly Random _rng = {|E128075:new Random()|};

                               void M(byte[] data, Aes aes)
                               {
                                   var delay = _rng.Next(100);
                                   _rng.NextBytes(data);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_ForSameNamedTypesOutsideCryptographyNamespace()
    {
        return VerifyAsync("""
                           using System;

                           class Aes
                           {
                           }

                           class SHA256
                           {
                               public static byte[] HashData(byte[] data) => data;
                           }

                           class C
                           {
                               int M(byte[] data)
                               {
                                   SHA256.HashData(data);
                                   return new Random().Next(10);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_StoredRandomGetItemsHexString_ForSensitiveNames()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               private readonly Random _rng = new Random();

                               void M()
                               {
                                   var sessionKey = {|E128075:_rng.GetItems(new[] { 'a', 'b' }, 8)|};
                                   var authToken = {|E128075:_rng.GetHexString(32)|};
                                   var plain = _rng.GetHexString(4);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_DerivedRandom_AssignedToSensitiveName()
    {
        return VerifyAsync("""
                           using System;

                           class AppRandom : Random
                           {
                           }

                           class C
                           {
                               void M()
                               {
                                   var token = {|E128075:new AppRandom()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_CallOnNewDerivedRandom_AtCreation()
    {
        return VerifyAsync("""
                           using System;

                           class AppRandom : Random
                           {
                           }

                           class C
                           {
                               void M(byte[] salt)
                               {
                                   var sessionId = {|E128075:new AppRandom()|}.Next();
                                   {|E128075:new AppRandom()|}.NextBytes(salt);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_StoredDerivedRandom_AtTheCall()
    {
        return VerifyAsync("""
                           using System;

                           class AppRandom : Random
                           {
                           }

                           class AppOverride : Random
                           {
                               public override int Next() => 4;
                           }

                           class C
                           {
                               private readonly AppOverride _rng = new AppOverride();
                               private readonly AppRandom _inherited = new AppRandom();

                               void M()
                               {
                                   var sessionKey = {|E128075:_rng.Next()|};
                                   var authToken = {|E128075:_inherited.Next()|};
                                   var jitter = _rng.Next();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_CallOnTypeParameterCreation()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               void M<T>() where T : Random, new()
                               {
                                   var token = {|E128075:new T().Next()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_ForDerivedRandom_WithOrdinaryValues()
    {
        return VerifyAsync("""
                           using System;

                           class AppRandom : Random
                           {
                           }

                           class C
                           {
                               void M()
                               {
                                   var jitter = new AppRandom().Next();
                                   var other = new AppRandom();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_FileMarks_ForUsingAlias()
    {
        return VerifyAsync("""
                           using System;
                           using R = System.Security.Cryptography.RandomNumberGenerator;

                           class C
                           {
                               void M(byte[] buffer)
                               {
                                   R.Fill(buffer);
                                   var delay = {|E128075:new Random()|}.Next(5);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_FileMarks_ForUsingStatic()
    {
        return VerifyAsync("""
                           using System;
                           using static System.Security.Cryptography.CryptographicOperations;

                           class C
                           {
                               bool M(byte[] a, byte[] b)
                               {
                                   var delay = {|E128075:Random.Shared|}.Next(5);
                                   return FixedTimeEquals(a, b);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_FileMarks_ForTypeInCryptographyChildNamespace()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               void M(X509Certificate2 certificate)
                               {
                                   var delay = {|E128075:new Random()|}.Next(5);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_FileMarks_ForVarThatInfersCryptographyType()
    {
        return VerifyAsync(
            [
                """
                static class Maker
                {
                    public static System.Security.Cryptography.SHA256 Make() => System.Security.Cryptography.SHA256.Create();
                }
                """,
                """
                class C
                {
                    void M()
                    {
                        var hasher = Maker.Make();
                        var delay = {|E128075:new System.Random()|}.Next(5);
                    }
                }
                """
            ]);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_FileMarks_ForCryptographyMemberAccessWithoutTypeName()
    {
        return VerifyAsync(
            [
                """
                static class Maker
                {
                    public static System.Security.Cryptography.SHA256 Make() => System.Security.Cryptography.SHA256.Create();
                }
                """,
                """
                class C
                {
                    void M(byte[] data)
                    {
                        byte[] hash = Maker.Make().ComputeHash(data);
                        var delay = {|E128075:new System.Random()|}.Next(5);
                    }
                }
                """
            ]);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_ForFileWithNoCryptographyTypeName()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               void M(byte[] data)
                               {
                                   var hasher = Make();
                                   var delay = new Random().Next(5);
                               }

                               static object Make() => new object();
                           }
                           """);
    }
}
