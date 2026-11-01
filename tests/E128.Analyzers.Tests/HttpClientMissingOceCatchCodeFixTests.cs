using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpClientMissingOceCatchCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<HttpClientMissingOceCatchAnalyzer, HttpClientMissingOceCatchCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_InsertsOceCatch_WhenCatchHasNoIndentation()
    {
        const string source = """
                              using System;
                              using System.Net.Http;
                              using System.Threading.Tasks;
                              class C
                              {
                              async Task M(HttpClient client, string url)
                              {
                              try
                              {
                              await client.GetAsync(url);
                              }
                              {|E128051:catch (Exception)|}
                              {
                              }
                              }
                              }
                              """;

        const string fixedCode = """
                                 using System;
                                 using System.Net.Http;
                                 using System.Threading.Tasks;
                                 class C
                                 {
                                 async Task M(HttpClient client, string url)
                                 {
                                 try
                                 {
                                 await client.GetAsync(url);
                                 }
                                 catch (OperationCanceledException)
                                         {
                                             throw;
                                         }
                                 catch (Exception)
                                 {
                                 }
                                 }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_InsertsOceCatch_BeforeBroadCatchException()
    {
        const string source = """
                              using System;
                              using System.Net.Http;
                              using System.Threading.Tasks;

                              public class Service
                              {
                                  private readonly HttpClient _client = new();

                                  public async Task DoWork()
                                  {
                                      try
                                      {
                                          await _client.GetAsync("https://example.com");
                                      }
                                      {|E128051:catch (Exception)|}
                                      {
                                      }
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System;
                                 using System.Net.Http;
                                 using System.Threading.Tasks;

                                 public class Service
                                 {
                                     private readonly HttpClient _client = new();

                                     public async Task DoWork()
                                     {
                                         try
                                         {
                                             await _client.GetAsync("https://example.com");
                                         }
                                         catch (OperationCanceledException)
                                         {
                                             throw;
                                         }
                                         catch (Exception)
                                         {
                                         }
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_InsertsOceCatch_BeforeBareCatch()
    {
        const string source = """
                              using System;
                              using System.Net.Http;
                              using System.Threading.Tasks;

                              public class Service
                              {
                                  private readonly HttpClient _client = new();

                                  public async Task DoWork()
                                  {
                                      try
                                      {
                                          await _client.PostAsync("https://example.com", null);
                                      }
                                      {|E128051:catch|}
                                      {
                                      }
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System;
                                 using System.Net.Http;
                                 using System.Threading.Tasks;

                                 public class Service
                                 {
                                     private readonly HttpClient _client = new();

                                     public async Task DoWork()
                                     {
                                         try
                                         {
                                             await _client.PostAsync("https://example.com", null);
                                         }
                                         catch (OperationCanceledException)
                                         {
                                             throw;
                                         }
                                         catch
                                         {
                                         }
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
