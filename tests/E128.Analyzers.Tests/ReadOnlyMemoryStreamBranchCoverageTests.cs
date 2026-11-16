using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ReadOnlyMemoryStreamBranchCoverageTests
{
    private const string Usings = """
        using System;
        using System.IO;
        using System.IO.Compression;
        using System.Net.Http;
        using System.Security.Cryptography;
        using System.Text;
        using System.Text.Json;
        using System.Xml;
        using System.Xml.Linq;
        using System.Xml.XPath;

        """;

    private static Task VerifyAsync(string members)
    {
        return AnalyzerTestRunner.VerifyAsync<ReadOnlyMemoryStreamAnalyzer>(Usings + members);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStreamIsAssignedAfterItsDeclaration()
    {
        return VerifyAsync("""
            class C
            {
                void M(byte[] data)
                {
                    MemoryStream stream;
                    stream = new MemoryStream(data);
                    _ = stream.Length;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStreamIsStoredInAFieldOrAProperty()
    {
        return VerifyAsync("""
            class C
            {
                private static readonly MemoryStream Shared = new MemoryStream(new byte[4]);

                private MemoryStream _stream = new MemoryStream(new byte[4]);

                public MemoryStream Stream { get; set; } = new MemoryStream(new byte[4]);

                void M(byte[] data)
                {
                    _stream = new MemoryStream(data);
                    this.Stream = new MemoryStream(data);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCreationIsNotAssignedToAnything()
    {
        return VerifyAsync("""
            class C
            {
                void M(byte[] data)
                {
                    using var reader = new StreamReader(new MemoryStream(data));
                    new MemoryStream(data).Dispose();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStreamHasNoArgumentsOrIsAnotherType()
    {
        return VerifyAsync("""
            class C
            {
                void M()
                {
                    using var empty = new MemoryStream();
                    MemoryStream implicitEmpty = new();
                    using var reader = new StringReader("text");
                    _ = empty.Length + implicitEmpty.Length + reader.Peek();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnotherTypeReturnsTheBytes()
    {
        return VerifyAsync("""
            class Source
            {
                public static byte[] GetBytes(string text)
                {
                    return new byte[4];
                }
            }

            class C
            {
                void M(string text)
                {
                    using var stream = {|E128224:new MemoryStream(Source.GetBytes(text))|};
                    _ = stream.Length;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenPositionIsIncrementedOrDecremented()
    {
        return VerifyAsync("""
            class C
            {
                void M(byte[] data)
                {
                    using var prefix = new MemoryStream(data);
                    ++prefix.Position;

                    using var postfix = new MemoryStream(data);
                    postfix.Position--;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALengthIsPassedAsAnArgument()
    {
        return VerifyAsync("""
            class C
            {
                static void Use(long length)
                {
                }

                void M(byte[] data)
                {
                    using var stream = {|E128224:new MemoryStream(data)|};
                    Use(stream.Length);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStreamIsCopiedIntoAnotherVariable()
    {
        return VerifyAsync("""
            class C
            {
                void M(byte[] data)
                {
                    using var stream = new MemoryStream(data);
                    var alias = stream;
                    _ = alias.Length;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenReadOnlyConsumersTakeTheStream()
    {
        return VerifyAsync("""
            class C
            {
                void M(byte[] data)
                {
                    using var forBinary = {|E128224:new MemoryStream(data)|};
                    using var binary = new BinaryReader(forBinary);

                    using var forContent = {|E128224:new MemoryStream(data)|};
                    using var content = new StreamContent(forContent);

                    using var forXPath = {|E128224:new MemoryStream(data)|};
                    var xpath = new XPathDocument(forXPath);

                    using var forXml = {|E128224:new MemoryStream(data)|};
                    var document = XDocument.Load(forXml);

                    using var forReader = {|E128224:new MemoryStream(data)|};
                    using var reader = XmlReader.Create(forReader);

                    using var forHash = {|E128224:new MemoryStream(data)|};
                    using var sha = SHA256.Create();
                    var hash = sha.ComputeHash(forHash);

                    using var forBrotli = {|E128224:new MemoryStream(data)|};
                    using var brotli = new BrotliStream(forBrotli, CompressionMode.Decompress);

                    using var forDeflate = {|E128224:new MemoryStream(data)|};
                    using var deflate = new DeflateStream(forDeflate, CompressionMode.Decompress);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAConsumerIsNotOnTheReadOnlyList()
    {
        return VerifyAsync("""
            class Parser
            {
                public static void Create(Stream stream)
                {
                }
            }

            class C
            {
                void M(byte[] data)
                {
                    using var buffered = new MemoryStream(data);
                    using var wrapper = new BufferedStream(buffered);

                    using var custom = new MemoryStream(data);
                    Parser.Create(custom);

                    using var other = new MemoryStream(data);
                    Console.WriteLine(other);

                    using var hashed = new MemoryStream(data);
                    using var sha = SHA256.Create();
                    _ = sha.ComputeHashAsync(hashed);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCompressionModeIsNotTheDecompressConstant()
    {
        return VerifyAsync("""
            class C
            {
                void M(byte[] data, CompressionMode mode)
                {
                    using var stream = new MemoryStream(data);
                    using var wrapper = new GZipStream(stream, mode);
                }
            }
            """);
    }
}
