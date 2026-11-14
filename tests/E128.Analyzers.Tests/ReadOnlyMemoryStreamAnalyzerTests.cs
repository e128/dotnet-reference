using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ReadOnlyMemoryStreamAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAStringIsWrappedForAStreamReader()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadOnlyMemoryStreamAnalyzer>("""
                           using System.IO;
                           using System.Text;

                           class C
                           {
                               void M(string text)
                               {
                                   using var stream = {|E128224:new MemoryStream(Encoding.UTF8.GetBytes(text))|};
                                   using var reader = new StreamReader(stream);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAByteArrayIsParsedAsJson()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadOnlyMemoryStreamAnalyzer>("""
                           using System.IO;
                           using System.Text.Json;

                           class C
                           {
                               void M(byte[] data)
                               {
                                   using var stream = {|E128224:new MemoryStream(data)|};
                                   using var document = JsonDocument.Parse(stream);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheStreamIsReadDirectly()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadOnlyMemoryStreamAnalyzer>("""
                           using System.IO;

                           class C
                           {
                               int M(byte[] data)
                               {
                                   using var stream = {|E128224:new MemoryStream(data)|};
                                   var buffer = new byte[stream.Length];
                                   return stream.Read(buffer, 0, buffer.Length);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStreamIsWritten()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadOnlyMemoryStreamAnalyzer>("""
                           using System.IO;

                           class C
                           {
                               void M(byte[] data)
                               {
                                   using var stream = new MemoryStream(data);
                                   stream.Write(data, 0, data.Length);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheConsumerIsUnknown()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadOnlyMemoryStreamAnalyzer>("""
                           using System.IO;

                           class C
                           {
                               void M(byte[] data)
                               {
                                   using var stream = new MemoryStream(data);
                                   Consume(stream);
                               }

                               static void Consume(Stream stream)
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStreamIsReturned()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadOnlyMemoryStreamAnalyzer>("""
                           using System.IO;

                           class C
                           {
                               Stream M(byte[] data)
                               {
                                   var stream = new MemoryStream(data);
                                   return stream;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStreamIsCreatedWithACapacity()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadOnlyMemoryStreamAnalyzer>("""
                           using System.IO;

                           class C
                           {
                               void M()
                               {
                                   using var empty = new MemoryStream();
                                   using var sized = new MemoryStream(16);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStreamIsWritable()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadOnlyMemoryStreamAnalyzer>("""
                           using System.IO;

                           class C
                           {
                               void M(byte[] data)
                               {
                                   using var stream = new MemoryStream(data, writable: true);
                                   stream.Write(data, 0, data.Length);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBufferIsReadThroughToArray()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadOnlyMemoryStreamAnalyzer>("""
                           using System.IO;

                           class C
                           {
                               byte[] M(byte[] data)
                               {
                                   using var stream = new MemoryStream(data);
                                   return stream.ToArray();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheStreamIsReadByADecompressingWrapper()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadOnlyMemoryStreamAnalyzer>("""
                           using System.IO;
                           using System.IO.Compression;

                           class C
                           {
                               void M(byte[] data)
                               {
                                   using var stream = {|E128224:new MemoryStream(data)|};
                                   using var wrapper = new GZipStream(stream, CompressionMode.Decompress);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStreamIsWrittenByACompressingWrapper()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadOnlyMemoryStreamAnalyzer>("""
                           using System.IO;
                           using System.IO.Compression;

                           class C
                           {
                               void M(byte[] data)
                               {
                                   using var stream = new MemoryStream(data);
                                   using var wrapper = new GZipStream(stream, CompressionMode.Compress);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCompressionWrapperTakesNoMode()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadOnlyMemoryStreamAnalyzer>("""
                           using System.IO;
                           using System.IO.Compression;

                           class C
                           {
                               void M(byte[] data)
                               {
                                   using var stream = new MemoryStream(data);
                                   using var wrapper = new GZipStream(stream, CompressionLevel.Fastest);
                               }
                           }
                           """);
    }
}
