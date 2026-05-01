using System.Net;
using System.Text;
using AzureMcp.Tools.Clients;
using AzureMcp.Tools.Configuration;
using AzureMcp.Tools.Tests.Fixtures;

namespace AzureMcp.Tools.Tests.WorkItems;

public sealed class AzureDevOpsPullRequestClientTests
{
    [Fact]
    public async Task ReadPullRequestAsync_ParsesStructuredResponse()
    {
        var payload = FixtureCatalog.ReadText("ApiResponses", "pull-request-33.json");

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        }));

        var client = new AzureDevOpsPullRequestClient(new StubRequestDispatcher(httpClient));
        var connection = new AzureDevOpsConnectionInfo("https://dev.azure.com/test-org", "secret-pat", null);

        var result = await client.ReadPullRequestAsync(
            connection,
            "11111111-1111-1111-1111-111111111111",
            "22222222-2222-2222-2222-222222222222",
            33);

        result.Error.IsNull();
        result.PullRequest.IsNotNull();
        result.PullRequest!.Id.Is(33);
        result.PullRequest.DescriptionText.Is("PR description text");
    }

    [Fact]
    public async Task ReadPullRequestAsync_ReturnsNotFoundError_WhenMissing()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        var client = new AzureDevOpsPullRequestClient(new StubRequestDispatcher(httpClient));
        var connection = new AzureDevOpsConnectionInfo("https://dev.azure.com/test-org", "secret-pat", null);

        var result = await client.ReadPullRequestAsync(
            connection,
            "11111111-1111-1111-1111-111111111111",
            "22222222-2222-2222-2222-222222222222",
            404);

        result.Error.IsNotNull();
        result.Error!.Message.ToLowerInvariant().IsContaining("not found");
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }

    private sealed class StubRequestDispatcher(HttpClient httpClient) : IAzureDevOpsRequestDispatcher
    {
        public Task<HttpResponseMessage> SendAsync(
            AzureDevOpsConnectionInfo connection,
            Func<HttpRequestMessage> requestFactory,
            CancellationToken cancellationToken = default)
            => httpClient.SendAsync(requestFactory(), cancellationToken);
    }
}
