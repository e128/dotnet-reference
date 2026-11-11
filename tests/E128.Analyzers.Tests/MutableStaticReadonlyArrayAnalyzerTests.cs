using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MutableStaticReadonlyArrayAnalyzerTests
{
    #region Fires

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnStaticReadonlyStringArray_WithCollectionExpression()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableStaticReadonlyArrayAnalyzer>("""
                           using System;
                           class C
                           {
                               private static readonly {|E128061:string[] Names = ["a", "b"]|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnStaticReadonlyStringArray_WithArrayInitializer()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableStaticReadonlyArrayAnalyzer>("""
                           class C
                           {
                               private static readonly {|E128061:string[] Names = new[] { "a", "b" }|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnStaticReadonlyIntArray()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableStaticReadonlyArrayAnalyzer>("""
                           class C
                           {
                               private static readonly {|E128061:int[] Ports = [80, 443]|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnStaticReadonlyArray_WithExplicitTypeInNew()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableStaticReadonlyArrayAnalyzer>("""
                           class C
                           {
                               private static readonly {|E128061:string[] Suffixes = new string[] { "s_", "m_" }|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnInternalStaticReadonlyArray()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableStaticReadonlyArrayAnalyzer>("""
                           class C
                           {
                               internal static readonly {|E128061:string[] Labels = ["x", "y"]|};
                           }
                           """);
    }

    #endregion Fires

    #region Does not fire

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenAlreadyImmutableArray()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableStaticReadonlyArrayAnalyzer>("""
                           using System.Collections.Immutable;
                           class C
                           {
                               private static readonly ImmutableArray<string> Names = ImmutableArray.Create("a", "b");
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenInstanceReadonlyArray()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableStaticReadonlyArrayAnalyzer>("""
                           class C
                           {
                               private readonly string[] _items = ["a", "b"];
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenStaticButNotReadonly()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableStaticReadonlyArrayAnalyzer>("""
                           class C
                           {
                               private static string[] Names = ["a", "b"];
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenReadonlyButNotStatic()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableStaticReadonlyArrayAnalyzer>("""
                           class C
                           {
                               private readonly string[] Names = ["a", "b"];
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenNoInitializer()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableStaticReadonlyArrayAnalyzer>("""
                           class C
                           {
                               private static readonly string[] Names;
                               static C() { Names = ["a", "b"]; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenStaticReadonlyList()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableStaticReadonlyArrayAnalyzer>("""
                           using System.Collections.Generic;
                           class C
                           {
                               private static readonly List<string> Names = ["a", "b"];
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenPublicField()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableStaticReadonlyArrayAnalyzer>("""
                           class C
                           {
                               public static readonly string[] Names = ["a", "b"];
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenConstField()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableStaticReadonlyArrayAnalyzer>("""
                           class C
                           {
                               private const string Names = "a";
                           }
                           """);
    }

    #endregion Does not fire
}
