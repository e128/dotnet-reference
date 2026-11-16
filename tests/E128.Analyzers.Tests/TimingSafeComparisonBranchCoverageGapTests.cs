using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TimingSafeComparisonBranchCoverageGapTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForGenericAndUnresolvedInvocations()
    {
        return VerifyAsync("""
            class C
            {
                void M(byte[] data, byte[] tag)
                {
                    Local<int>();
                    SequenceEqual(tag);
                    SequenceEqual();
                    {|CS0103:Missing|}.SequenceEqual(data, tag);
                    {|CS0103:missing|}.Equals(tag);
                }

                void Local<T>()
                {
                }

                bool SequenceEqual(byte[] other) => true;

                bool SequenceEqual() => true;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForATwoArgumentInstanceMethodNamedSequenceEqual()
    {
        return VerifyAsync("""
            class Comparer
            {
                public bool SequenceEqual(byte[] left, byte[] right) => true;
            }

            class C
            {
                bool M(Comparer comparer, byte[] computedTag, byte[] expectedTag)
                {
                    return comparer.SequenceEqual(computedTag, expectedTag);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStringEqualsCallIsUnqualifiedOrBelongsToAnotherType()
    {
        return VerifyAsync("""
            class Other
            {
                public bool Equals(string left, string right) => true;
            }

            class C
            {
                bool M(string computedTag, string expectedTag, Other other)
                {
                    var a = Equals(computedTag);
                    var b = other.Equals(computedTag, expectedTag);
                    return a && b;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnOperandHasNoSymbolAndNoSecretName()
    {
        return VerifyAsync("""
            class C
            {
                bool M(bool flag, string left, string right, string other)
                {
                    var a = (flag ? left : right) == other;
                    var b = other == (flag ? left : right);
                    return a && b;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheWeakNameOperandIsAForeachOrUninitializedLocal()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            class C
            {
                bool M(List<string> tokens, string expectedToken)
                {
                    var found = false;
                    foreach (var token in tokens)
                    {
                        found = {|E128157:token == expectedToken|};
                    }

                    string digest;
                    digest = tokens[0];
                    return found && {|E128157:digest == expectedToken|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAStringInvocationDoesNotResolve()
    {
        return VerifyAsync("""
            class C
            {
                static string Compute(int value) => "x";

                bool M(string left)
                {
                    return left == Compute({|CS1503:"x"|});
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenHashMembersAreFieldsOrPlainProperties()
    {
        return VerifyAsync("""
            class Holder
            {
                public string Hash = "";
            }

            class Wrapper
            {
                public string Hash { get; set; } = "";
            }

            class C
            {
                bool M(Holder left, Holder right, Wrapper first, Wrapper second)
                {
                    var a = left.Hash == right.Hash;
                    var b = first.Hash == second.Hash;
                    return a && b;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSecretFieldIsInitializedInAnotherSyntaxTree()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>(
        [
            """
            using System;
            using System.Security.Cryptography;

            partial class C
            {
                private static readonly string Computed = Convert.ToHexString(new HMACSHA256(new byte[32]).ComputeHash(new byte[1]));
            }
            """,
            """
            partial class C
            {
                bool M(string other)
                {
                    return Computed == other;
                }
            }
            """
        ]);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheIncrementalHashLocalHasNoHmacCreation()
    {
        return VerifyAsync("""
            using System;
            using System.Collections.Generic;
            using System.Security.Cryptography;
            class C
            {
                private IncrementalHash field;

                bool M(List<IncrementalHash> hashes, string text)
                {
                    foreach (var each in hashes)
                    {
                        var a = Convert.ToHexString(each.GetHashAndReset()) == text;
                    }

                    IncrementalHash declared;
                    declared = hashes[0];
                    var b = Convert.ToHexString(declared.GetHashAndReset()) == text;
                    var c = Convert.ToHexString(field.GetHashAndReset()) == text;
                    return b && c;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheOperandIsAnUnqualifiedStringCall()
    {
        return VerifyAsync("""
            class C
            {
                static string Compute() => "x";

                bool M(string other)
                {
                    return Compute() == other;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnlyWhenTheEncoderChainStaysWithinTheDepthLimit()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;

            namespace System
            {
                public static class Convert
                {
                    public static string ToHexString(byte[] bytes) => string.Empty;

                    public static string ToHexString(string text) => text;
                }
            }

            class C
            {
                bool M(HMACSHA256 hmac, byte[] data, string other)
                {
                    var within = {|E128157:System.Convert.ToHexString(System.Convert.ToHexString(System.Convert.ToHexString(hmac.ComputeHash(data)))) == other|};
                    var beyond = System.Convert.ToHexString(System.Convert.ToHexString(System.Convert.ToHexString(System.Convert.ToHexString(hmac.ComputeHash(data))))) == other;
                    return within && beyond;
                }
            }
            """);
    }
}
