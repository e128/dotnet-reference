using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsHashNameResolutionBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FipsUnapprovedHashAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_BothArmsOfConditionalCoalesceAndSwitch()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                static void Use(HashAlgorithmName name) { }

                void M(bool flag, HashAlgorithmName? maybe, int kind)
                {
                    {|E128071:Use(flag ? HashAlgorithmName.MD5 : HashAlgorithmName.SHA256)|};
                    {|E128071:Use(flag ? HashAlgorithmName.SHA256 : HashAlgorithmName.MD5)|};
                    {|E128071:Use(maybe ?? HashAlgorithmName.MD5)|};
                    {|E128071:Use(kind switch { 0 => HashAlgorithmName.SHA256, 1 => HashAlgorithmName.MD5, _ => HashAlgorithmName.SHA384 })|};
                    Use(flag ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA512);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_AForbiddenNameOnlyOncePerCall()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                static void Use(HashAlgorithmName name) { }

                static void Two(HashAlgorithmName first, HashAlgorithmName second) { }

                void M(bool flag)
                {
                    {|E128071:Use(flag ? HashAlgorithmName.MD5 : HashAlgorithmName.MD5)|};
                    {|E128071:Two(HashAlgorithmName.MD5, HashAlgorithmName.MD5)|};
                    {|E128071:Two(second: HashAlgorithmName.MD5, first: HashAlgorithmName.SHA256)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NameHeldInUnreassignedLocalsUpToTheDepthLimit()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                static void Use(HashAlgorithmName name) { }

                void M()
                {
                    var l1 = HashAlgorithmName.MD5;
                    var l2 = l1;
                    var l3 = l2;
                    var l4 = l3;
                    var l5 = l4;
                    var l6 = l5;
                    {|E128071:Use(l4)|};
                    Use(l5);
                    Use(l6);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenALocalIsReassignedOrPassedByRef()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                static void Use(HashAlgorithmName name) { }

                static void Fill(out HashAlgorithmName name) { name = HashAlgorithmName.SHA256; }

                static void Bump(ref HashAlgorithmName name) { }

                void M()
                {
                    var reassigned = HashAlgorithmName.MD5;
                    reassigned = HashAlgorithmName.SHA256;
                    Use(reassigned);

                    var byRef = HashAlgorithmName.MD5;
                    Bump(ref byRef);
                    Use(byRef);

                    HashAlgorithmName viaOut = HashAlgorithmName.MD5;
                    Fill(out viaOut);
                    Use(viaOut);

                    HashAlgorithmName second;
                    var tupleTarget = HashAlgorithmName.MD5;
                    (tupleTarget, second) = (HashAlgorithmName.SHA256, HashAlgorithmName.SHA384);
                    Use(tupleTarget);

                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ReadonlyFieldNameUnlessAConstructorWritesIt()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                static readonly HashAlgorithmName Fixed = HashAlgorithmName.MD5;

                readonly HashAlgorithmName byConstructor = HashAlgorithmName.MD5;

                readonly HashAlgorithmName byInit = HashAlgorithmName.MD5;

                readonly HashAlgorithmName untouched = HashAlgorithmName.MD5;

                int Setter
                {
                    init { byInit = HashAlgorithmName.SHA256; }
                }

                C()
                {
                    byConstructor = HashAlgorithmName.SHA256;
                }

                static void Use(HashAlgorithmName name) { }

                void M()
                {
                    {|E128071:Use(Fixed)|};
                    {|E128071:Use(Fixed)|};
                    {|E128071:Use(untouched)|};
                    Use(byConstructor);
                    Use(byInit);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NameBuiltFromAStringOrAnObjectIdentifier()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                static void Use(HashAlgorithmName name) { }

                void M(string text, string oid)
                {
                    {|E128071:Use(new HashAlgorithmName("MD5"))|};
                    {|E128071:Use(HashAlgorithmName.FromOid("1.3.14.3.2.26"))|};
                    {|E128071:Use(HashAlgorithmName.FromOid(" 1.2.840.113549.2.5 "))|};
                    Use(new HashAlgorithmName(text));
                    Use(new HashAlgorithmName());
                    Use(new HashAlgorithmName("SHA256"));
                    Use(HashAlgorithmName.FromOid("9.9.9"));
                    Use(HashAlgorithmName.FromOid(oid));
                    Use(HashAlgorithmName.FromOid("2.16.840.1.101.3.4.2.1"));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NameStoredInAMutableFieldOrAPropertyButNotAReadonlyField()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                static readonly HashAlgorithmName ReadonlyBad = HashAlgorithmName.MD5;

                static HashAlgorithmName mutableBad = {|E128071:HashAlgorithmName.MD5|};

                static HashAlgorithmName PropertyBad { get; } = {|E128071:HashAlgorithmName.SHA1|};

                static HashAlgorithmName mutableGood = HashAlgorithmName.SHA256;

                static HashAlgorithmName PropertyGood { get; } = HashAlgorithmName.SHA512;

                static string text = "MD5";

                static int Number { get; } = 5;

                static int numberField = 5;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NameAssignedToAHashNameTarget()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                HashAlgorithmName field;

                HashAlgorithmName Property { get; set; }

                void M()
                {
                    {|E128071:field = HashAlgorithmName.MD5|};
                    {|E128071:Property = HashAlgorithmName.SHA1|};
                    field = HashAlgorithmName.SHA256;
                    int number;
                    number = 5;
                    HashAlgorithmName a, b;
                    (a, b) = ({|E128071:HashAlgorithmName.MD5|}, HashAlgorithmName.SHA256);
                    (a, number) = ({|E128071:HashAlgorithmName.SHA1|}, 5);
                }

                void Other(HashAlgorithmName pair)
                {
                    var (x, y) = (HashAlgorithmName.MD5, HashAlgorithmName.SHA1);
                    (field, Property) = Pair();
                }

                (HashAlgorithmName, HashAlgorithmName) Pair() => (HashAlgorithmName.SHA256, HashAlgorithmName.SHA256);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAHashNameProbeReadsTheNameWithoutSelectingIt()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using System.Security.Cryptography;
            class Registry
            {
                public bool Contains(HashAlgorithmName name) => false;
            }

            class C
            {
                void M(List<HashAlgorithmName> names, Dictionary<HashAlgorithmName, int> map, HashSet<HashAlgorithmName> set, Registry registry)
                {
                    var a = names.Contains(HashAlgorithmName.MD5);
                    var b = names.IndexOf(HashAlgorithmName.MD5);
                    var c = names.Remove(HashAlgorithmName.MD5);
                    var d = map.ContainsKey(HashAlgorithmName.MD5);
                    var e = map.TryGetValue(HashAlgorithmName.MD5, out _);
                    var f = HashAlgorithmName.MD5.Equals(HashAlgorithmName.SHA256);
                    var g = set.Contains(HashAlgorithmName.SHA1);
                    var h = {|E128071:registry.Contains(HashAlgorithmName.MD5)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAFieldReferenceIsAComparisonOfTheTripleDesPbeValue()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(PbeEncryptionAlgorithm algorithm, object boxed)
                {
                    var a = algorithm == PbeEncryptionAlgorithm.TripleDes3KeyPkcs12;
                    var b = algorithm != PbeEncryptionAlgorithm.TripleDes3KeyPkcs12;
                    var c = algorithm is PbeEncryptionAlgorithm.TripleDes3KeyPkcs12;
                    var d = algorithm.Equals(PbeEncryptionAlgorithm.TripleDes3KeyPkcs12);
                    var e = PbeEncryptionAlgorithm.TripleDes3KeyPkcs12.Equals(algorithm);
                    var f = algorithm.HasFlag(PbeEncryptionAlgorithm.TripleDes3KeyPkcs12);
                    var g = (int)PbeEncryptionAlgorithm.TripleDes3KeyPkcs12 == 3;
                    var h = boxed.Equals((object)PbeEncryptionAlgorithm.TripleDes3KeyPkcs12);
                    switch (algorithm)
                    {
                        case PbeEncryptionAlgorithm.TripleDes3KeyPkcs12:
                            break;
                    }

                    var chosen = {|E128071:PbeEncryptionAlgorithm.TripleDes3KeyPkcs12|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForbiddenTypesThroughAliasesAndBases()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            using Md = System.Security.Cryptography.MD5;
            class MyHash : HMACSHA1
            {
            }

            class C
            {
                void M()
                {
                    var a = {|E128071:Md.Create()|};
                    var b = {|E128071:new MyHash()|};
                }
            }
            """);
    }
}
