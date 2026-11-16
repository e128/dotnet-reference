using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Applies the init-to-set rewrite. The analyzer reports from a compilation start action, so
///     the verifier skips the local diagnostic check.
/// </summary>
public sealed class OptionsBindInitCodeFixBranchCoverageTests
{
    private const string Stubs = """
                                 namespace Microsoft.Extensions.Configuration
                                 {
                                     public interface IConfiguration
                                     {
                                         IConfigurationSection GetSection(string key);
                                     }

                                     public interface IConfigurationSection : IConfiguration
                                     {
                                     }
                                 }

                                 namespace Microsoft.Extensions.Options
                                 {
                                     public class OptionsBuilder<T> where T : class
                                     {
                                         public OptionsBuilder<T> Bind(Microsoft.Extensions.Configuration.IConfigurationSection section) => this;
                                     }
                                 }

                                 """;

    private const string Setup = """

                                 class Setup
                                 {
                                     void Configure(Microsoft.Extensions.Configuration.IConfiguration config)
                                     {
                                         new Microsoft.Extensions.Options.OptionsBuilder<MixedOptions>()
                                             .Bind(config.GetSection("Mixed"));
                                     }
                                 }
                                 """;

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesInitWithSet_ForAnAutoProperty()
    {
        return CodeFixTestRunner.RunAsync<OptionsBindInitAnalyzer, OptionsBindInitCodeFixProvider>(
            Stubs + """
                    public class MixedOptions
                    {
                        public string Mutable { get; set; } = string.Empty;

                        public string Immutable { get; {|E128033:init|}; }
                    }
                    """ + Setup,
            Stubs + """
                    public class MixedOptions
                    {
                        public string Mutable { get; set; } = string.Empty;

                        public string Immutable { get; set; }
                    }
                    """ + Setup,
            behaviors: CodeFixTestBehaviors.SkipLocalDiagnosticCheck);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesInitWithSet_AndKeepsTheBodyAndModifiers()
    {
        return CodeFixTestRunner.RunAsync<OptionsBindInitAnalyzer, OptionsBindInitCodeFixProvider>(
            Stubs + """
                    public class MixedOptions
                    {
                        private string _name = string.Empty;

                        public string Name
                        {
                            get => _name;
                            protected {|E128033:init|} { _name = value; }
                        }
                    }
                    """ + Setup,
            Stubs + """
                    public class MixedOptions
                    {
                        private string _name = string.Empty;

                        public string Name
                        {
                            get => _name;
                            protected set { _name = value; }
                        }
                    }
                    """ + Setup,
            behaviors: CodeFixTestBehaviors.SkipLocalDiagnosticCheck);
    }
}
