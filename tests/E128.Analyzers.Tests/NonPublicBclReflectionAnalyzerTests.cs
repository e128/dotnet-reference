using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class NonPublicBclReflectionAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<NonPublicBclReflectionAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAMethodLookupAsksForNonPublicMembers()
    {
        return VerifyAsync("""
                           using System.Reflection;
                           class C
                           {
                               object M()
                               {
                                   return {|E128137:typeof(string).GetMethod("Empty", BindingFlags.NonPublic | BindingFlags.Static)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAFieldLookupAsksForNonPublicMembers()
    {
        return VerifyAsync("""
                           using System.Reflection;
                           class C
                           {
                               object M()
                               {
                                   return {|E128137:typeof(System.Collections.Generic.List<int>)
                                       .GetField("_size", BindingFlags.NonPublic | BindingFlags.Instance)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsNotAFrameworkType()
    {
        return VerifyAsync("""
                           using System.Reflection;
                           class C
                           {
                               private void M()
                               {
                               }

                               object N()
                               {
                                   return typeof(C).GetMethod("M", BindingFlags.NonPublic | BindingFlags.Instance);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLookupAsksForPublicMembers()
    {
        return VerifyAsync("""
                           using System.Reflection;
                           class C
                           {
                               object M()
                               {
                                   return typeof(string).GetMethod("IsNullOrEmpty", BindingFlags.Public | BindingFlags.Static);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsNotATypeOfExpression()
    {
        return VerifyAsync("""
                           using System;
                           using System.Reflection;
                           class C
                           {
                               object M(Type type)
                               {
                                   return type.GetMethod("Empty", BindingFlags.NonPublic | BindingFlags.Static);
                               }
                           }
                           """);
    }
}
