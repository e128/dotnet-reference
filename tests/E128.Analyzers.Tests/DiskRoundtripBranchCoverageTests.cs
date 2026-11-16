using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DiskRoundtripBranchCoverageTests
{
    private static Task VerifyAsync(string members)
    {
        return AnalyzerTestRunner.VerifyAsync<DiskRoundtripAnalyzer>(
            "using System;\nusing System.IO;\nusing System.Threading.Tasks;\nclass C\n{\n" + members + "}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_InAConstructor()
    {
        return VerifyAsync("""
                string text;
                C(string path, string content)
                {
                    File.WriteAllText(path, content);
                    text = {|E128064:File.ReadAllText(path)|};
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForALocalFunctionWithoutARoundTrip()
    {
        return VerifyAsync("""
                void M(string path)
                {
                    Local();

                    void Local() { File.Delete(path); }
                    string Expression() => File.ReadAllText(path);
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnAbstractMethodWithoutABody()
    {
        return AnalyzerTestRunner.VerifyAsync<DiskRoundtripAnalyzer>("""
            abstract class C
            {
                public abstract string M(string path);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnExpressionBodiedReadWithoutAWrite()
    {
        return VerifyAsync("string M(string path) => File.ReadAllText(path) + File.ReadAllText(path);\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheWriteSitsOutsideEveryStatement()
    {
        return VerifyAsync("""
                static string Use(Action write, Func<string> read) => read();

                string M(string path, string content) =>
                    Use(() => File.WriteAllText(path, content), () => { return File.ReadAllText(path); });

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForCallsThatAreNotMemberAccessOrNotFileOperations()
    {
        return VerifyAsync("""
                string M(string path, string content)
                {
                    Helper(path);
                    File.Delete(path);
                    var exists = File.Exists(path);
                    return exists ? File.ReadAllText(path) : content;
                }

                static void Helper(string path) { }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForFileInfoMethodsThatAreNotStreamFactories()
    {
        return VerifyAsync("""
                string M(FileInfo info, string content)
                {
                    using (var writer = info.CreateText())
                    {
                        writer.Write(content);
                    }

                    info.Refresh();
                    info.Delete();
                    return content;
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAUserDefinedFileInfoType()
    {
        return AnalyzerTestRunner.VerifyAsync<DiskRoundtripAnalyzer>("""
            class FileInfo
            {
                public object OpenWrite() => new object();
                public object OpenRead() => new object();
            }
            class C
            {
                void M(FileInfo info)
                {
                    info.OpenWrite();
                    info.OpenRead();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAFlushFollowsTheWriterWrite()
    {
        return VerifyAsync("""
                string M(string path, string content)
                {
                    using (var writer = File.CreateText(path))
                    {
                        writer.Write(content);
                        writer.Flush();
                        writer.WriteLine();
                    }

                    return {|E128064:File.ReadAllText(path)|};
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTwoWritesPrecedeTheRead()
    {
        return VerifyAsync("""
                string M(string path, string first, string second)
                {
                    using (var writer = File.CreateText(path))
                    {
                        writer.Write(first);
                        writer.Write(second);
                    }

                    return {|E128064:File.ReadAllText(path)|};
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForWriterCallsOnUntrackedReceivers()
    {
        return VerifyAsync("""
                StreamWriter field = StreamWriter.Null;

                string M(string path, string content, StreamWriter other)
                {
                    other.Write(content);
                    this.field.Write(content);
                    Make().Write(content);
                    return File.ReadAllText(path);
                }

                StreamWriter Make() => StreamWriter.Null;

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForObjectCreationsThatAreNotFileStreams()
    {
        return VerifyAsync("""
                string M(string path, string content)
                {
                    File.WriteAllText(path, content);
                    var plain = new object();
                    var sized = new System.Collections.Generic.List<int>(4);
                    var initialized = new System.Collections.Generic.List<int> { 1 };
                    var empty = new System.Collections.Generic.List<int>();
                    return content;
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForUserDefinedStreamTypes()
    {
        return AnalyzerTestRunner.VerifyAsync<DiskRoundtripAnalyzer>("""
            class StreamWriter { public StreamWriter(string path) { } }
            class StreamReader { public StreamReader(string path) { } }
            class C
            {
                void M(string path)
                {
                    var writer = new StreamWriter(path);
                    var reader = new StreamReader(path);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForABinaryWriterOverAFileCreateFollowedByARead()
    {
        return VerifyAsync("""
                byte[] M(string path)
                {
                    using (var writer = new BinaryWriter(File.Create(path)))
                    {
                        writer.Write(1);
                    }

                    return {|E128064:File.ReadAllBytes(path)|};
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForABinaryReaderOverAFileOpenReadAfterAWrite()
    {
        return VerifyAsync("""
                void M(string path, string content)
                {
                    File.WriteAllText(path, content);
                    using (var reader = {|E128064:new BinaryReader({|E128064:File.OpenRead(path)|})|})
                    {
                        reader.ReadInt32();
                    }
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForBinaryStreamsOverNonFileSources()
    {
        return VerifyAsync("""
                string M(string path, string content)
                {
                    File.WriteAllText(path, content);
                    using (var writer = new BinaryWriter(new MemoryStream()))
                    {
                        writer.Write(1);
                    }

                    using (var reader = new BinaryReader(new MemoryStream()))
                    {
                        reader.ReadInt32();
                    }

                    return content;
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAFileStreamWithTheModeAlone()
    {
        return VerifyAsync("""
                string M(string path)
                {
                    using (var stream = new FileStream(path, FileMode.Create)) { }
                    return {|E128064:File.ReadAllText(path)|};
                }

                string N(string path)
                {
                    using (var stream = new FileStream(path, FileMode.Open)) { }
                    File.WriteAllText(path, "x");
                    return {|E128064:File.ReadAllText(path)|};
                }

            """);
    }
}
