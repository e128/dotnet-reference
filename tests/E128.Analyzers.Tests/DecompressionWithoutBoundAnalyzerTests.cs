using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DecompressionWithoutBoundAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheLoopHasNoBound()
    {
        return AnalyzerTestRunner.VerifyAsync<DecompressionWithoutBoundAnalyzer>("""
                           using System.IO;
                           using System.IO.Compression;

                           class C
                           {
                               void Run(Stream source)
                               {
                                   var memory = new MemoryStream();
                                   var buffer = new byte[1024];
                                   var gzip = new GZipStream(source, CompressionMode.Decompress);
                                   [|while (gzip.Read(buffer, 0, buffer.Length) > 0)
                                   {
                                       memory.Write(buffer, 0, buffer.Length);
                                   }|]
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopChecksTheOutputLength()
    {
        return AnalyzerTestRunner.VerifyAsync<DecompressionWithoutBoundAnalyzer>("""
                           using System.IO;
                           using System.IO.Compression;

                           class C
                           {
                               void Run(Stream source)
                               {
                                   var memory = new MemoryStream();
                                   var buffer = new byte[1024];
                                   var gzip = new GZipStream(source, CompressionMode.Decompress);
                                   while (gzip.Read(buffer, 0, buffer.Length) > 0)
                                   {
                                       if (memory.Length > 1_000_000)
                                       {
                                           break;
                                       }

                                       memory.Write(buffer, 0, buffer.Length);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopDoesNotWriteMemory()
    {
        return AnalyzerTestRunner.VerifyAsync<DecompressionWithoutBoundAnalyzer>("""
                           using System.IO;
                           using System.IO.Compression;

                           class C
                           {
                               void Run(Stream source, Stream destination)
                               {
                                   var buffer = new byte[1024];
                                   var gzip = new GZipStream(source, CompressionMode.Decompress);
                                   while (gzip.Read(buffer, 0, buffer.Length) > 0)
                                   {
                                       destination.Write(buffer, 0, buffer.Length);
                                   }
                               }
                           }
                           """);
    }
}
