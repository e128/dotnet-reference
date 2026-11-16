using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsForbiddenAlgorithmNameBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FipsForbiddenAlgorithmNameAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_CreationAndStaticFactoriesOfAnExcludedCipher()
    {
        return VerifyAsync("""
            using System;

            class BlowfishEngine
            {
                public static BlowfishEngine Create() => {|E128148:new BlowfishEngine()|};

                public static byte[] HashData(byte[] data) => data;

                public static void Reset() { }

                public void Run() { }
            }

            class C
            {
                void M(BlowfishEngine engine, byte[] data)
                {
                    var a = {|E128148:new BlowfishEngine()|};
                    var b = {|E128148:BlowfishEngine.Create()|};
                    var c = {|E128148:BlowfishEngine.HashData(data)|};
                    Func<BlowfishEngine> d = {|E128148:BlowfishEngine.Create|};
                    BlowfishEngine.Reset();
                    engine.Run();
                    Action e = BlowfishEngine.Reset;
                    Action f = engine.Run;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NamesThatMatchAWholeWordOnly()
    {
        return VerifyAsync("""
            class Rc4Transform { }

            class Arcfour { }

            class Cast5Engine { }

            class Cast6_Cipher { }

            class SkipjackEngine { }

            class RC4Thing { }

            class Brc4 { }

            class Forecast5 { }

            class C
            {
                void M()
                {
                    var a = {|E128148:new Rc4Transform()|};
                    var b = {|E128148:new Arcfour()|};
                    var c = {|E128148:new Cast5Engine()|};
                    var d = {|E128148:new Cast6_Cipher()|};
                    var e = {|E128148:new SkipjackEngine()|};
                    var f = {|E128148:new RC4Thing()|};
                    var g = new Brc4();
                    var h = new Forecast5();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_IdeaOnlyWhenTheTypeLooksLikeACipher()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;

            interface ICipherAlgorithm { }

            class CipherBase { }

            class IdeaEngine { }

            class IdeaCng { }

            class IdeaWithBase : CipherBase { }

            class IdeaWithInterface : ICipherAlgorithm { }

            class IdeaRankingAlgorithm { }

            class IdeaBoard { }

            abstract class IdeaSymmetric : SymmetricAlgorithm
            {
                public static byte[] HashData(byte[] data) => data;
            }

            class IDEA { }

            namespace Acme.Crypto
            {
                class IDEA { }
            }

            namespace Acme.Cipher
            {
                class IdeaOnly { }
            }

            class C
            {
                void M()
                {
                    var a = {|E128148:new IdeaEngine()|};
                    var b = {|E128148:new IdeaCng()|};
                    var c = {|E128148:new IdeaWithBase()|};
                    var d = {|E128148:new IdeaWithInterface()|};
                    var e = new IdeaRankingAlgorithm();
                    var f = new IdeaBoard();
                    var g = new IDEA();
                    var h = {|E128148:new Acme.Crypto.IDEA()|};
                    var i = new Acme.Cipher.IdeaOnly();
                    var j = {|E128148:IdeaSymmetric.HashData(new byte[1])|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsAFrameworkTypeOrNamedLikeAnAllowedAlgorithm()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] data)
                {
                    var a = new System.Text.StringBuilder();
                    var b = SHA256.HashData(data);
                    var c = Aes.Create();
                    var d = new System.Collections.Generic.List<int>();
                }
            }
            """);
    }
}
