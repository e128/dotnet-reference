using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Architecture.Tests;

/// <summary>
///     Checks the ProjectReference items of each production project against the frozen direct
///     edges, and checks that every production project under src appears in the frozen graph.
///     Both tests read project files without MSBuild evaluation. A conditional reference or an
///     untracked project under src fails them until the map records it.
/// </summary>
public sealed class ProjectReferenceGraphTests
{
    private const string SolutionFileName = "E128.Reference.slnx";

    private static readonly FrozenDictionary<string, ImmutableArray<string>> DirectReferences =
        new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal)
        {
            ["E128.Analyzers"] = [],
            ["E128.Analyzers.CodeFixers"] = ["E128.Analyzers"],
            ["E128.Analyzers.Union"] = ["E128.Analyzers"],
            ["E128.Reference.Cli"] = ["E128.Reference.Core"],
            ["E128.Reference.Core"] = [],
            ["E128.Reference.Web"] = ["E128.Reference.Core"],
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly ImmutableArray<string> ExcludedProjects = [];

    public static TheoryData<string> Projects()
    {
        return [.. DirectReferences.Keys.Order(StringComparer.Ordinal)];
    }

    [Theory]
    [Trait("Category", "CI")]
    [MemberData(nameof(Projects), MemberType = typeof(ProjectReferenceGraphTests))]
    public void ProjectFileReferences_MatchTheFrozenGraph(string project)
    {
        string[] actual =
        [
            .. XDocument.Load(ProjectFile(project))
                .Descendants("ProjectReference")
                .Select(static item => Stem((string?)item.Attribute("Include") ?? string.Empty))
                .Order(StringComparer.Ordinal)
        ];

        Assert.Equal(DirectReferences[project].Order(StringComparer.Ordinal), actual, StringComparer.Ordinal);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void EveryProductionProject_IsInTheFrozenGraphOrExcluded()
    {
        string[] discovered =
        [
            .. Directory.EnumerateFiles(SourceRoot(), "*.csproj", SearchOption.AllDirectories)
                .Select(Stem)
                .Order(StringComparer.Ordinal)
        ];
        string[] expected = [.. DirectReferences.Keys.Concat(ExcludedProjects).Order(StringComparer.Ordinal)];

        Assert.Equal(expected, discovered, StringComparer.Ordinal);
    }

    private static string Stem(string include)
    {
        return Path.GetFileNameWithoutExtension(include.Replace('\\', '/').Split('/')[^1]);
    }

    private static string ProjectFile(string project)
    {
        return Directory.EnumerateFiles(SourceRoot(), project + ".csproj", SearchOption.AllDirectories).Single();
    }

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src");
    }
}
