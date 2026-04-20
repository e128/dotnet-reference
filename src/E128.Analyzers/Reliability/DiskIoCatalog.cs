using System;
using System.Collections.Generic;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Central catalog of recognized System.IO write/read patterns for E128064
///     (disk write-then-read round-trip). A single source of truth for Tiers A–D.
/// </summary>
internal static class DiskIoCatalog
{
    private static readonly HashSet<string> FileWriteValueMethods = new(StringComparer.Ordinal)
    {
        "WriteAllText",
        "WriteAllTextAsync",
        "WriteAllBytes",
        "WriteAllBytesAsync",
        "WriteAllLines",
        "WriteAllLinesAsync",
        "AppendAllText",
        "AppendAllTextAsync",
        "AppendAllLines",
        "AppendAllLinesAsync"
    };

    private static readonly HashSet<string> FileReadValueMethods = new(StringComparer.Ordinal)
    {
        "ReadAllText",
        "ReadAllTextAsync",
        "ReadAllBytes",
        "ReadAllBytesAsync",
        "ReadAllLines",
        "ReadAllLinesAsync"
    };

    // File.* factories that produce a Stream/StreamWriter targeting a path for WRITE.
    private static readonly HashSet<string> FileWriteFactories = new(StringComparer.Ordinal)
    {
        "Create",
        "CreateText",
        "OpenWrite",
        "AppendText"
    };

    // File.* factories that produce a Stream/StreamReader targeting a path for READ.
    private static readonly HashSet<string> FileReadFactories = new(StringComparer.Ordinal)
    {
        "OpenRead",
        "OpenText"
    };

    internal static IoKind FileMethodKind(string methodName)
    {
        return methodName switch
        {
            "WriteAllText" or "WriteAllTextAsync" or "AppendAllText" or "AppendAllTextAsync"
                or "ReadAllText" or "ReadAllTextAsync" => IoKind.Text,
            "WriteAllBytes" or "WriteAllBytesAsync" or "ReadAllBytes" or "ReadAllBytesAsync" => IoKind.Bytes,
            "WriteAllLines" or "WriteAllLinesAsync" or "AppendAllLines" or "AppendAllLinesAsync"
                or "ReadAllLines" or "ReadAllLinesAsync" => IoKind.Lines,
            "Create" or "OpenWrite" or "OpenRead" => IoKind.Stream,
            "CreateText" or "AppendText" or "OpenText" => IoKind.Writer,
            _ => IoKind.Unknown
        };
    }

    internal static bool IsFileWriteValueMethod(string name)
    {
        return FileWriteValueMethods.Contains(name);
    }

    internal static bool IsFileReadValueMethod(string name)
    {
        return FileReadValueMethods.Contains(name);
    }

    internal static bool IsFileWriteFactory(string name)
    {
        return FileWriteFactories.Contains(name);
    }

    internal static bool IsFileReadFactory(string name)
    {
        return FileReadFactories.Contains(name);
    }

    internal static bool IsAsyncName(string name)
    {
        return name.EndsWith("Async", StringComparison.Ordinal);
    }

    internal static bool IsWriterWriteMethod(string name)
    {
        return name is "Write" or "WriteAsync" or "WriteLine" or "WriteLineAsync" or "Flush" or "FlushAsync";
    }

    internal static bool IsReaderReadMethod(string name)
    {
        return name is "Read" or "ReadAsync" or "ReadToEnd" or "ReadToEndAsync"
            or "ReadLine" or "ReadLineAsync" or "ReadInt32" or "ReadInt64"
            or "ReadByte" or "ReadBytes" or "ReadString" or "ReadBoolean"
            or "ReadSingle" or "ReadDouble" or "ReadDecimal" or "ReadChar" or "ReadChars";
    }

    internal static string KindDescription(IoKind kind)
    {
        return kind switch
        {
            IoKind.Text => "text",
            IoKind.Bytes => "bytes",
            IoKind.Lines => "lines",
            IoKind.Stream => "stream",
            IoKind.Writer => "writer",
            IoKind.Reader => "reader",
            IoKind.Binary => "binary",
            IoKind.Unknown => "value",
            _ => "value"
        };
    }

    internal enum IoKind
    {
        Text = 0,
        Bytes = 1,
        Lines = 2,
        Stream = 3,
        Writer = 4,
        Reader = 5,
        Binary = 6,
        Unknown = 7
    }
}
