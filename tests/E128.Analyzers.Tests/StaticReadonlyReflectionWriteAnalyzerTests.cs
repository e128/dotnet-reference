using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StaticReadonlyReflectionWriteAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<StaticReadonlyReflectionWriteAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheDirectChainWritesAStaticReadonlyField()
    {
        return VerifyAsync("""
                           using System.Reflection;

                           class C
                           {
                               private static readonly int Value = 1;

                               static void Set()
                               {
                                   {|E128254:typeof(C).GetField("Value", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 2)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGetFieldResultIsALocal()
    {
        return VerifyAsync("""
                           using System.Reflection;

                           class C
                           {
                               private static readonly int Value = 1;

                               static void Set()
                               {
                                   var field = typeof(C).GetField("Value", BindingFlags.NonPublic | BindingFlags.Static);
                                   {|E128254:field.SetValue(null, 2)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheFieldIsPublicAndNamedWithNameof()
    {
        return VerifyAsync("""
                           using System.Reflection;

                           class C
                           {
                               public static readonly int Value = 1;

                               static void Set()
                               {
                                   {|E128254:typeof(C).GetField(nameof(C.Value), BindingFlags.Public | BindingFlags.Static).SetValue(null, 2)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFieldIsMutable()
    {
        return VerifyAsync("""
                           using System.Reflection;

                           class C
                           {
                               private static int Value = 1;

                               static void Set()
                               {
                                   typeof(C).GetField("Value", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, 2);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFieldIsReadonlyButNotStatic()
    {
        return VerifyAsync("""
                           using System.Reflection;

                           class C
                           {
                               private readonly int _value = 1;

                               void Set()
                               {
                                   typeof(C).GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(this, 2);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsAFrameworkType()
    {
        return VerifyAsync("""
                           using System;
                           using System.Reflection;

                           class C
                           {
                               static void Set()
                               {
                                   typeof(string).GetField("Empty", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, "x");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFlagsExcludeStatic()
    {
        return VerifyAsync("""
                           using System.Reflection;

                           class C
                           {
                               private static readonly int Value = 1;

                               static void Set()
                               {
                                   typeof(C).GetField("Value", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(null, 2);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFlagsExcludeNonPublic()
    {
        return VerifyAsync("""
                           using System.Reflection;

                           class C
                           {
                               private static readonly int Value = 1;

                               static void Set()
                               {
                                   typeof(C).GetField("Value", BindingFlags.Static)?.SetValue(null, 2);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFlagsAreHeldInALocal()
    {
        return VerifyAsync("""
                           using System.Reflection;

                           class C
                           {
                               private static readonly int Value = 1;

                               static void Set()
                               {
                                   var flags = BindingFlags.NonPublic | BindingFlags.Static;
                                   typeof(C).GetField("Value", flags)?.SetValue(null, 2);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsATypeVariable()
    {
        return VerifyAsync("""
                           using System;
                           using System.Reflection;

                           class C
                           {
                               private static readonly int Value = 1;

                               static void Set(Type type)
                               {
                                   type.GetField("Value", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, 2);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheCallIsNullConditional()
    {
        return VerifyAsync("""
                           using System.Reflection;

                           class C
                           {
                               private static readonly int Value = 1;

                               static void Set()
                               {
                                   {|E128254:typeof(C).GetField("Value", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, 2)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheReceiverIsAGetTypeCall()
    {
        return VerifyAsync("""
                           using System.Reflection;

                           class C
                           {
                               private static readonly int Value = 1;

                               static void Set(C instance)
                               {
                                   {|E128254:instance.GetType().GetField("Value", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 2)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheTypeIsALocal()
    {
        return VerifyAsync("""
                           using System;
                           using System.Reflection;

                           class C
                           {
                               private static readonly int Value = 1;

                               static void Set()
                               {
                                   Type type = typeof(C);
                                   {|E128254:type.GetField("Value", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 2)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheFlagsAreAConstField()
    {
        return VerifyAsync("""
                           using System.Reflection;

                           class C
                           {
                               private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Static;
                               private static readonly int Value = 1;

                               static void Set()
                               {
                                   {|E128254:typeof(C).GetField("Value", Flags).SetValue(null, 2)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheFieldIsAssignedBeforeUse()
    {
        return VerifyAsync("""
                           using System.Reflection;

                           class C
                           {
                               private static readonly int Value = 1;

                               static void Set()
                               {
                                   FieldInfo field;
                                   field = typeof(C).GetField("Value", BindingFlags.NonPublic | BindingFlags.Static);
                                   {|E128254:field.SetValue(null, 2)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAssignmentFollowsTheUse()
    {
        return VerifyAsync("""
                           using System.Reflection;

                           class C
                           {
                               private static readonly int Value = 1;

                               static void Set()
                               {
                                   FieldInfo field = null;
                                   field.SetValue(null, 2);
                                   field = typeof(C).GetField("Value", BindingFlags.NonPublic | BindingFlags.Static);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenSetValueDirectWritesAStaticReadonlyField()
    {
        return VerifyAsync("""
                           using System.Reflection;

                           class C
                           {
                               private static readonly int Value = 1;

                               static void Set()
                               {
                                   var value = 2;
                                   {|E128254:typeof(C).GetField("Value", BindingFlags.NonPublic | BindingFlags.Static).SetValueDirect(__makeref(value), 2)|};
                               }
                           }
                           """);
    }
}
