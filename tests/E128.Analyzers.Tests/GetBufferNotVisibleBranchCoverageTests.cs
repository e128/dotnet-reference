using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Covers how the GetBuffer rule traces a receiver back to the creation of its stream.
/// </summary>
public sealed class GetBufferNotVisibleBranchCoverageTests
{
    private const string Header = """
                                  using System.Collections.Generic;
                                  using System.IO;

                                  class DerivedStream : MemoryStream
                                  {
                                      public DerivedStream(byte[] buffer)
                                          : base(buffer)
                                      {
                                      }
                                  }

                                  """;

    private static Task VerifyAsync(string members, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<GetBufferNotVisibleAnalyzer>(
            Header + "class C\n{\n" + members + "\n}\n",
            expected: null,
            options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnotherAssignmentFollowsTheCall()
    {
        return VerifyAsync("""
                               void M(byte[] bytes)
                               {
                                   var ms = new MemoryStream(bytes);
                                   var buffer = {|E128192:ms.GetBuffer()|};
                                   ms = new MemoryStream(bytes, 0, 1, true, publiclyVisible: true);
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALaterAssignmentToAnotherVariableAndAnElementPrecedeTheCall()
    {
        return VerifyAsync("""
                               void M(byte[] bytes, MemoryStream[] streams)
                               {
                                   var ms = new MemoryStream(bytes);
                                   var other = new MemoryStream();
                                   other = new MemoryStream(8);
                                   streams[0] = new MemoryStream(8);
                                   var buffer = {|E128192:ms.GetBuffer()|};
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheNearestAssignmentIsTheOneBeforeTheCall()
    {
        return VerifyAsync("""
                               void M(byte[] bytes)
                               {
                                   MemoryStream ms = new MemoryStream();
                                   ms = new MemoryStream(bytes, writable: true);
                                   ms = new MemoryStream(bytes);
                                   var buffer = {|E128192:ms.GetBuffer()|};
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStreamIsBuiltWithAnInitializerOnly()
    {
        return VerifyAsync("""
                               void M()
                               {
                                   var ms = new MemoryStream { };
                                   var buffer = ms.GetBuffer();
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenACompoundAssignmentIsTheOnlyLaterWrite()
    {
        return VerifyAsync("""
                               void M(byte[] bytes)
                               {
                                   MemoryStream? ms = new MemoryStream();
                                   ms ??= new MemoryStream(bytes);
                                   var buffer = ms.GetBuffer();
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheParameterIsNeverAssigned()
    {
        return VerifyAsync("""
                               void M(MemoryStream ms)
                               {
                                   var buffer = ms.GetBuffer();
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverTypeDerivesFromMemoryStream()
    {
        return VerifyAsync("""
                               void M(byte[] bytes, DerivedStream parameter)
                               {
                                   var local = new DerivedStream(bytes);
                                   var first = local.GetBuffer();
                                   var second = parameter.GetBuffer();
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenADerivedStreamIsAssignedToAMemoryStreamLocal()
    {
        return VerifyAsync("""
                               void M(byte[] bytes)
                               {
                                   MemoryStream ms = new DerivedStream(bytes);
                                   var buffer = ms.GetBuffer();
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsALoopVariable()
    {
        return VerifyAsync("""
                               void M(IEnumerable<MemoryStream> streams)
                               {
                                   foreach (var ms in streams)
                                   {
                                       var buffer = ms.GetBuffer();
                                   }
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsACastExpression()
    {
        return VerifyAsync("""
                               void M(Stream stream)
                               {
                                   var buffer = ((MemoryStream)stream).GetBuffer();
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallIsInAnExpressionBody()
    {
        return VerifyAsync("""
                               byte[] M(MemoryStream ms) => ms.GetBuffer();
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalHasNoInitializer()
    {
        return VerifyAsync(
            """
                void M()
                {
                    MemoryStream ms;
                    var buffer = ms.GetBuffer();
                }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCreationAndTheCallDoNotBind()
    {
        return VerifyAsync(
            """
                void M()
                {
                    var ms = new MemoryStream(missing);
                    var buffer = ms.GetBuffer();
                    var other = unknown.GetBuffer();
                }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }
}
