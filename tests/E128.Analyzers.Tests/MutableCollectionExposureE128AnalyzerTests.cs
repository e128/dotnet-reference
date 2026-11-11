using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MutableCollectionExposureE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task PublicMethodReturningList_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableCollectionExposureAnalyzer>("""
                           using System.Collections.Generic;

                           public class Service
                           {
                               public List<string> {|E128052:GetNames|}() => new();
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PublicPropertyWithList_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableCollectionExposureAnalyzer>("""
                           using System.Collections.Generic;

                           public class Service
                           {
                               public List<int> {|E128052:Items|} { get; init; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PublicMethodReturningDictionary_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableCollectionExposureAnalyzer>("""
                           using System.Collections.Generic;

                           public class Service
                           {
                               public Dictionary<string, int> {|E128052:GetCounts|}() => new();
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrivateMethod_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableCollectionExposureAnalyzer>("""
                           using System.Collections.Generic;

                           public class Service
                           {
                               private List<string> GetNames() => new();
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyWithMutableSetter_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableCollectionExposureAnalyzer>("""
                           using System.Collections.Generic;

                           public class Service
                           {
                               public List<int> Items { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BuilderClass_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableCollectionExposureAnalyzer>("""
                           using System.Collections.Generic;

                           public class QueryBuilder
                           {
                               public List<string> GetParts() => new();
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OptionsClass_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableCollectionExposureAnalyzer>("""
                           using System.Collections.Generic;

                           public class AppOptions
                           {
                               public List<string> AllowedHosts { get; init; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InterfaceMethod_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableCollectionExposureAnalyzer>("""
                           using System.Collections.Generic;

                           public interface IService
                           {
                               List<string> GetNames();
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IReadOnlyListReturn_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableCollectionExposureAnalyzer>("""
                           using System.Collections.Generic;

                           public class Service
                           {
                               public IReadOnlyList<string> GetNames() => new List<string>();
                           }
                           """);
    }
}
