using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileStreamParallelCaptureAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALocalFileStreamIsCapturedByAForEachBody()
    {
        return AnalyzerTestRunner.VerifyAsync<FileStreamParallelCaptureAnalyzer>(
            """
            using System.IO;
            using System.Threading.Tasks;

            class C
            {
                void M(string path)
                {
                    using FileStream stream = File.OpenRead(path);
                    Parallel.ForEach(new long[] { 0, 1 }, offset => ReadAt({|E128234:stream|}, offset));
                }

                void ReadAt(FileStream stream, long offset)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALocalFileStreamIsCapturedByAForBody()
    {
        return AnalyzerTestRunner.VerifyAsync<FileStreamParallelCaptureAnalyzer>(
            """
            using System.IO;
            using System.Threading.Tasks;

            class C
            {
                void M(string path)
                {
                    using FileStream stream = File.OpenRead(path);
                    Parallel.For(0, 10, i => {|E128234:stream|}.ReadByte());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALocalFileStreamIsCapturedByAnInvokeAction()
    {
        return AnalyzerTestRunner.VerifyAsync<FileStreamParallelCaptureAnalyzer>(
            """
            using System.IO;
            using System.Threading.Tasks;

            class C
            {
                void M(string path)
                {
                    using FileStream stream = File.OpenRead(path);
                    Parallel.Invoke(
                        () => {|E128234:stream|}.ReadByte(),
                        () => stream.Seek(0, SeekOrigin.Begin));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAFieldOfTheEnclosingTypeIsCaptured()
    {
        return AnalyzerTestRunner.VerifyAsync<FileStreamParallelCaptureAnalyzer>(
            """
            using System.IO;
            using System.Threading.Tasks;

            class C
            {
                private readonly FileStream _stream;

                public C(FileStream stream)
                {
                    _stream = stream;
                }

                void M()
                {
                    Parallel.ForEach(new long[] { 0, 1 }, offset => {|E128234:_stream|}.ReadByte());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAMethodParameterIsCaptured()
    {
        return AnalyzerTestRunner.VerifyAsync<FileStreamParallelCaptureAnalyzer>(
            """
            using System.IO;
            using System.Threading.Tasks;

            class C
            {
                void M(FileStream stream)
                {
                    Parallel.ForEach(new long[] { 0, 1 }, offset => ReadAt({|E128234:stream|}, offset));
                }

                void ReadAt(FileStream stream, long offset)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheStreamTypeDerivesFromFileStream()
    {
        return AnalyzerTestRunner.VerifyAsync<FileStreamParallelCaptureAnalyzer>(
            """
            using System.IO;
            using System.Threading.Tasks;

            class TrackedStream : FileStream
            {
                public TrackedStream(string path) : base(path, FileMode.Open)
                {
                }
            }

            class C
            {
                void M(string path)
                {
                    using TrackedStream stream = new TrackedStream(path);
                    Parallel.ForEach(new long[] { 0, 1 }, offset => {|E128234:stream|}.ReadByte());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenANestedLambdaInsideTheBodyUsesTheStream()
    {
        return AnalyzerTestRunner.VerifyAsync<FileStreamParallelCaptureAnalyzer>(
            """
            using System.IO;
            using System.Threading.Tasks;

            class C
            {
                void M(string path)
                {
                    using FileStream stream = File.OpenRead(path);
                    Parallel.ForEach(new long[] { 0, 1 }, offset => Task.Run(() => {|E128234:stream|}.ReadByte()));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBodyDeclaresItsOwnStream()
    {
        return AnalyzerTestRunner.VerifyAsync<FileStreamParallelCaptureAnalyzer>(
            """
            using System.IO;
            using System.Threading.Tasks;

            class C
            {
                void M(string path)
                {
                    Parallel.ForEach(new long[] { 0, 1 }, offset =>
                    {
                        using FileStream perWorker = File.OpenRead(path);
                        perWorker.ReadByte();
                    });
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenParallelIsAUserDefinedType()
    {
        return AnalyzerTestRunner.VerifyAsync<FileStreamParallelCaptureAnalyzer>(
            """
            using System;
            using System.IO;

            static class Parallel
            {
                public static void For(int from, int to, Action<int> body)
                {
                }
            }

            class C
            {
                void M(string path)
                {
                    using FileStream stream = File.OpenRead(path);
                    Parallel.For(0, 10, i => stream.ReadByte());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenFileStreamIsAUserDefinedType()
    {
        return AnalyzerTestRunner.VerifyAsync<FileStreamParallelCaptureAnalyzer>(
            """
            using System.Threading.Tasks;

            public class FileStream
            {
                public int ReadByte()
                {
                    return 0;
                }
            }

            class C
            {
                void M()
                {
                    FileStream stream = new FileStream();
                    Parallel.ForEach(new long[] { 0, 1 }, offset => stream.ReadByte());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenACapturedValueIsNotAFileStream()
    {
        return AnalyzerTestRunner.VerifyAsync<FileStreamParallelCaptureAnalyzer>(
            """
            using System.IO;
            using System.Threading.Tasks;

            class C
            {
                void M()
                {
                    using StringReader reader = new StringReader("text");
                    Parallel.ForEach(new long[] { 0, 1 }, offset => reader.Read());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStreamIsPartOfTheLoopSource()
    {
        return AnalyzerTestRunner.VerifyAsync<FileStreamParallelCaptureAnalyzer>(
            """
            using System.IO;
            using System.Threading.Tasks;

            class C
            {
                void M(string path)
                {
                    using FileStream stream = File.OpenRead(path);
                    Parallel.ForEach(new FileStream[] { stream }, s => s.ReadByte());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStreamIsUsedInASiblingLambdaOutsideTheParallelCall()
    {
        return AnalyzerTestRunner.VerifyAsync<FileStreamParallelCaptureAnalyzer>(
            """
            using System;
            using System.IO;
            using System.Threading.Tasks;

            class C
            {
                void M(string path)
                {
                    using FileStream stream = File.OpenRead(path);
                    Action read = () => stream.ReadByte();
                    read();
                    Parallel.ForEach(new long[] { 0, 1 }, offset => DoWork(offset));
                }

                void DoWork(long offset)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenForEachAsyncCapturesTheStream()
    {
        return AnalyzerTestRunner.VerifyAsync<FileStreamParallelCaptureAnalyzer>(
            """
            using System.IO;
            using System.Threading.Tasks;

            class C
            {
                void M(string path)
                {
                    using FileStream stream = File.OpenRead(path);
                    Parallel.ForEachAsync(new long[] { 0, 1 }, (offset, token) =>
                    {
                        stream.ReadByte();
                        return ValueTask.CompletedTask;
                    });
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCompilationHasNoFrameworkParallelOrFileStreamType()
    {
        return AnalyzerTestRunner.VerifyAsync<FileStreamParallelCaptureAnalyzer>(
            """
            using System;
            using System.Collections.Generic;

            public delegate void Body<T>(T item);

            static class Parallel
            {
                public static void ForEach<T>(IEnumerable<T> source, Body<T> body)
                {
                }
            }

            public class FileStream
            {
                public int ReadByte()
                {
                    return 0;
                }
            }

            class C
            {
                void M()
                {
                    FileStream stream = new FileStream();
                    Parallel.ForEach(new[] { 1, 2 }, i => stream.ReadByte());
                }
            }
            """,
            options: new AnalyzerTestOptions { References = ReferenceAssemblies.NetStandard.NetStandard10 });
    }
}
