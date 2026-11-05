using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TaskCompletionSourceOptionsCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_AddsOptionsArgument()
    {
        const string source = """
                              using System.Threading.Tasks;
                              class C
                              {
                                  object M()
                                  {
                                      return {|E128159:new TaskCompletionSource<int>()|};
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System.Threading.Tasks;
                                 class C
                                 {
                                     object M()
                                     {
                                         return new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                                     }
                                 }
                                 """;

        return CodeFixTestRunner.RunAsync<TaskCompletionSourceOptionsAnalyzer,
            TaskCompletionSourceOptionsCodeFixProvider>(source, fixedCode,
            markupOptions: MarkupOptions.UseFirstDescriptor);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_AddsFlagToExistingOptions()
    {
        const string source = """
                              using System.Threading.Tasks;
                              class C
                              {
                                  object M()
                                  {
                                      return {|E128159:new TaskCompletionSource<int>(TaskCreationOptions.None)|};
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System.Threading.Tasks;
                                 class C
                                 {
                                     object M()
                                     {
                                         return new TaskCompletionSource<int>((TaskCreationOptions.None) | TaskCreationOptions.RunContinuationsAsynchronously);
                                     }
                                 }
                                 """;

        return CodeFixTestRunner.RunAsync<TaskCompletionSourceOptionsAnalyzer,
            TaskCompletionSourceOptionsCodeFixProvider>(source, fixedCode,
            markupOptions: MarkupOptions.UseFirstDescriptor);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesContinuationOptionsArgument()
    {
        const string source = """
                              using System.Threading.Tasks;
                              class C
                              {
                                  object M()
                                  {
                                      return {|E128159:new TaskCompletionSource<int>(TaskContinuationOptions.RunContinuationsAsynchronously)|};
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System.Threading.Tasks;
                                 class C
                                 {
                                     object M()
                                     {
                                         return new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                                     }
                                 }
                                 """;

        return CodeFixTestRunner.RunAsync<TaskCompletionSourceOptionsAnalyzer,
            TaskCompletionSourceOptionsCodeFixProvider>(source, fixedCode,
            markupOptions: MarkupOptions.UseFirstDescriptor);
    }
}
