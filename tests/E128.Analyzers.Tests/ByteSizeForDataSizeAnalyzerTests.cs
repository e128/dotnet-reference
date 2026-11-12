using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ByteSizeForDataSizeAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnIntProperty_WithBytesSuffix()
    {
        return AnalyzerTestRunner.VerifyAsync<ByteSizeForDataSizeAnalyzer>("""
                           class C
                           {
                               public int {|E128080:MaxSizeBytes|} { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnLongProperty_WithMbSuffix()
    {
        return AnalyzerTestRunner.VerifyAsync<ByteSizeForDataSizeAnalyzer>("""
                           class C
                           {
                               public long {|E128080:CacheSizeMb|} { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnParameter_WithKbSuffix()
    {
        return AnalyzerTestRunner.VerifyAsync<ByteSizeForDataSizeAnalyzer>("""
                           class C
                           {
                               void Foo(int {|E128080:BufferSizeKb|}) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnNullableInt_WithBytesSuffix()
    {
        return AnalyzerTestRunner.VerifyAsync<ByteSizeForDataSizeAnalyzer>("""
                           class C
                           {
                               public int? {|E128080:MaxSizeBytes|} { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnConstField_WithBytesSuffix()
    {
        return AnalyzerTestRunner.VerifyAsync<ByteSizeForDataSizeAnalyzer>("""
                           class C
                           {
                               const int {|E128080:MaxImageSizeBytes|} = 50 * 1024 * 1024;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnLocalVariable_WithBytesSuffix()
    {
        return AnalyzerTestRunner.VerifyAsync<ByteSizeForDataSizeAnalyzer>("""
                           class C
                           {
                               void Foo()
                               {
                                   long {|E128080:maxSizeBytes|} = 100;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoDiagnostic_ForStringWithDataSizeSuffix()
    {
        return AnalyzerTestRunner.VerifyAsync<ByteSizeForDataSizeAnalyzer>("""
                           class C
                           {
                               public string MaxSizeBytes { get; set; } = string.Empty;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoDiagnostic_ForIntWithoutDataSizeSuffix()
    {
        return AnalyzerTestRunner.VerifyAsync<ByteSizeForDataSizeAnalyzer>("""
                           class C
                           {
                               public int Count { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PartialProperty_ReportsOnce()
    {
        // A partial member arrives as two symbol events. The rule reports at the
        // defining part and skips the implementing part.
        return AnalyzerTestRunner.VerifyAsync<ByteSizeForDataSizeAnalyzer>("""
                           partial class C
                           {
                               public partial int {|E128080:MaxSizeBytes|} { get; }

                               public partial int MaxSizeBytes { get => 1; }
                           }
                           """);
    }
}
