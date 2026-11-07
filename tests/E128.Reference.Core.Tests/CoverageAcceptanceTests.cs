using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace E128.Reference.Core.Tests;

public sealed class CoverageAcceptanceTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    [Fact]
    [Trait("Category", "CI")]
    public async Task CoverageConfig_ExcludesTestAssemblies()
    {
        var config = await ReadCoverageConfigAsync();

        Assert.Contains(ReadExcludePatterns(config), pattern => IsMatch("E128.Reference.Core.Tests.dll", pattern));
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task CoverageConfig_MeasuresEveryAssemblyByDefault()
    {
        var config = await ReadCoverageConfigAsync();
        var excludes = ReadExcludePatterns(config);

        Assert.Null(config.Root?.Element("CodeCoverage")?.Element("ModulePaths")?.Element("Include"));
        Assert.DoesNotContain(excludes, pattern => IsMatch("E128.Analyzers.dll", pattern));
        Assert.DoesNotContain(excludes, pattern => IsMatch("E128.Reference.Core.dll", pattern));
        Assert.DoesNotContain(excludes, pattern => IsMatch("E128.Reference.Web.dll", pattern));
        Assert.DoesNotContain(excludes, pattern => IsMatch("BrandNew.Project.dll", pattern));
    }

    private static bool IsMatch(string assemblyName, string pattern)
    {
        return Regex.IsMatch(assemblyName, pattern, RegexOptions.None, MatchTimeout);
    }

    private static string[] ReadExcludePatterns(XDocument config)
    {
        return config.Root?
            .Element("CodeCoverage")?
            .Element("ModulePaths")?
            .Element("Exclude")?
            .Elements("ModulePath")
            .Select(element => element.Value)
            .ToArray() ?? [];
    }

    private static async Task<XDocument> ReadCoverageConfigAsync()
    {
        var content = await File.ReadAllTextAsync(FindCoverageConfig());

        return XDocument.Parse(content);
    }

    private static string FindCoverageConfig()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "coverage.config.xml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("coverage.config.xml not found in any parent directory of the test binary");
    }
}
