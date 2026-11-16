using System.Threading.Tasks;
using E128.Analyzers.FileSystem;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileSystemPathAnalyzerBranchCoverageTests
{
    private const string CliStubs = """
        using System;

        class Option<T>
        {
            public Option() { }
            public Option(string name) { }
        }

        class Option<TA, TB>
        {
            public Option(string name) { }
        }

        class Argument<T>
        {
            public Argument(string name) { }
        }

        namespace Q
        {
            class Option<T>
            {
                public Option(string name) { }
            }

            class Holder<T>
            {
                public Holder(string name) { }
            }
        }

        """;

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FileSystemPathAnalyzer>(
            code,
            options: new AnalyzerTestOptions { MarkupOptions = MarkupOptions.UseFirstDescriptor });
    }

    private static Task VerifyCliAsync(string statements)
    {
        return VerifyAsync(CliStubs + "class C { void M(string name) { " + statements + " } }");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAParameterThatReachesFileIoThroughTwoPathHops()
    {
        return VerifyAsync("""
            using System.IO;

            class C
            {
                string _external = "";

                void M(string {|E128001:root|})
                {
                    var other = Path.Combine(_external, "y");
                    var a = Path.Combine(root, "x");
                    var b = Path.GetFullPath(a);
                    File.ReadAllText(b);
                    File.Exists(other);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForCallsThatAreNotSystemIo()
    {
        return VerifyAsync("""
            static class File
            {
                public static string ReadAllText(string text) { return text; }
            }

            class FileInfo
            {
                public FileInfo() { }
                public FileInfo(string text) { }
            }

            class Reader
            {
                public string ReadAllText(string text) { return text; }
            }

            class C
            {
                Reader Get() { return new Reader(); }

                string Local(string text) { return text; }

                void M(string root, string tail)
                {
                    Local(root);
                    Get().ReadAllText(root);
                    File.ReadAllText(root);
                    var info = new FileInfo(root);
                    var empty = new FileInfo();
                    _ = info;
                    _ = empty;
                    _ = tail;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheIoArgumentIsNotAPlainName()
    {
        return VerifyAsync("""
            using System.IO;
            using System.Collections.Generic;
            using System.Text;

            class C
            {
                void M(string root)
                {
                    File.ReadAllText("lit");
                    File.ReadAllText(root + "x");
                    var stream = new FileStream("lit", FileMode.Open);
                    var other = new FileStream(root + "x", FileMode.Open);
                    var list = new List<string>(new[] { root });
                    var builder = new StringBuilder(root);
                    _ = stream;
                    _ = other;
                    _ = list;
                    _ = builder;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForQualifiedOptionTypes()
    {
        return VerifyCliAsync("""var option = new Q.Option<{|E128001:string|}>("--output");""");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForDirectoryAndFolderOptionNames()
    {
        return VerifyCliAsync("""
            var a = new Option<{|E128001:string|}>("--output-dir");
            var b = new Option<{|E128001:string|}>("--folder-path");
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnArgumentWithAFileName()
    {
        return VerifyCliAsync("""var a = new Argument<{|E128001:string|}>("filename");""");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForOptionShapesThatAreNotStringPaths()
    {
        return VerifyCliAsync("""
            var a = new Option<int>("--input");
            var b = new Option<String>("--input");
            var c = new Option<object>("--input");
            var d = new Option<string, string>("--input");
            var e = new Option<string>();
            var f = new Option<string>(name);
            var g = new Option<string>("--");
            var h = new Option<string> { };
            var i = new Q.Holder<string>("--input");
            var j = new Q.Option<int>("--input");
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("Path.Combine(a)", true)]
    [InlineData("Path.GetFullPath(a)", true)]
    [InlineData("path.Combine(a)", false)]
    [InlineData("Path.Missing(a)", false)]
    [InlineData("Combine(a)", false)]
    public void IsPathMethodCall_MatchesOnlyThePathClass(string text, bool expected)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(text);
        Assert.Equal(expected, FileSystemPathHelpers.IsPathMethodCall(invocation));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("Option<string>", "Option")]
    [InlineData("Argument<string>", "Argument")]
    [InlineData("System.CommandLine.Option<string>", "Option")]
    [InlineData("System.CommandLine.Argument<string>", "Argument")]
    [InlineData("Other<string>", null)]
    [InlineData("System.CommandLine.Other<string>", null)]
    [InlineData("System.CommandLine.Option", null)]
    [InlineData("string", null)]
    public void ExtractGenericCliType_FindsOptionAndArgument(string text, string? expectedName)
    {
        var result = FileSystemPathHelpers.ExtractGenericCliType(SyntaxFactory.ParseTypeName(text));
        Assert.Equal(expectedName, result?.Identifier.ValueText);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("Option<string>", true)]
    [InlineData("Option<int>", false)]
    [InlineData("Option<String>", false)]
    [InlineData("Option<string, string>", false)]
    public void GetCliStringTypeArg_AcceptsOnlyOneStringArgument(string text, bool expected)
    {
        var generic = (GenericNameSyntax)SyntaxFactory.ParseTypeName(text);
        Assert.Equal(expected, FileSystemPathHelpers.GetCliStringTypeArg(generic) is not null);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("(\"--output\")", true, "--output", "output")]
    [InlineData("(\"-p\")", false, "-p", "p")]
    [InlineData("(\"--\")", false, "--", "")]
    [InlineData("(name)", false, "", "")]
    [InlineData("(5)", false, "", "")]
    [InlineData("()", false, "", "")]
    public void TryGetPathCliName_ReadsTheFirstStringLiteral(string text, bool expected, string raw, string stripped)
    {
        var list = SyntaxFactory.ParseArgumentList(text);
        Assert.Equal(expected, FileSystemPathHelpers.TryGetPathCliName(list, out var rawName, out var strippedName));
        Assert.Equal(raw, rawName);
        Assert.Equal(stripped, strippedName);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetPathCliName_RejectsAMissingArgumentList()
    {
        Assert.False(FileSystemPathHelpers.TryGetPathCliName(null, out var rawName, out var strippedName));
        Assert.Empty(rawName);
        Assert.Empty(strippedName);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("output-dir", "directory path", "'Option<DirectoryInfo>'")]
    [InlineData("folder", "directory path", "'Option<DirectoryInfo>'")]
    [InlineData("input-file", "file path", "'Option<FileInfo>'")]
    [InlineData("path", "file system path", "'Option<FileInfo>' or 'Option<DirectoryInfo>'")]
    public void GetCliSuggestion_NamesTheTypeThatMatchesTheOptionName(string name, string description, string suggestion)
    {
        Assert.Equal((description, suggestion), FileSystemPathHelpers.GetCliSuggestion(name, "Option"));
    }
}
