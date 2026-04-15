using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128056FileInfoToctouAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<FileInfoToctouAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfoToctou_Reports_WhenExistsCheckFollowedByFileRead()
    {
        return VerifyAsync("""
            using System.IO;
            using System.Threading.Tasks;
            class Loader
            {
                async Task<byte[]?> LoadAsync(FileInfo fileInfo)
                {
                    if (!fileInfo.Exists) return null;
                    return await {|E128056:File.ReadAllBytesAsync(fileInfo.FullName)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfoToctou_NoReport_WhenReadIsGuardedWithTryCatch()
    {
        return VerifyAsync("""
            using System.IO;
            using System.Threading.Tasks;
            class Loader
            {
                async Task<byte[]?> LoadAsync(FileInfo fileInfo)
                {
                    if (!fileInfo.Exists) return null;
                    try
                    {
                        return await File.ReadAllBytesAsync(fileInfo.FullName);
                    }
                    catch (IOException)
                    {
                        return null;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfoToctou_NoReport_WhenNoExistsCheckPresent()
    {
        return VerifyAsync("""
            using System.IO;
            using System.Threading.Tasks;
            class Loader
            {
                async Task<byte[]?> LoadAsync(FileInfo fileInfo)
                {
                    try
                    {
                        return await File.ReadAllBytesAsync(fileInfo.FullName);
                    }
                    catch (IOException)
                    {
                        return null;
                    }
                }
            }
            """);
    }
}
